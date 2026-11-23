using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Recordum.Core.Abstractions;
using Recordum.Core.Enums;

namespace Recordum.Data.Sync;

/// <summary>
/// Unisce l'istantanea di un altro dispositivo nel database locale (spec §8.2–§8.4, <c>docs/formats/sync-file.md</c> §6).
/// Tutto in una transazione, con <see cref="SyncConnectionState.Applying"/> acceso (i trigger non lasciano lapidi né
/// cambiano <c>updated_at_ms</c>) e <c>defer_foreign_keys</c> (l'ordine degli insert non conta). SQL parametrico
/// costruito dal <see cref="SyncCatalog"/>, niente tracking EF.
/// <para>Ordine: 1. lapidi remote, figli prima (le FK RESTRICT scattano subito anche differite); 2. righe remote,
/// madri prima, campo per campo per ora di campo (Ruling 20); 3. figli rimasti senza madre (arrivati qui o spostati
/// sotto una madre cancellata) seguono la madre come farebbe la cascade; 4. le lapidi battute da una riga più recente
/// presente qui si tolgono, così tutti i dispositivi tengono le stesse.</para>
/// <para>Testo libero (regola B, <see cref="TextConflictBlock"/>): l'ora nei blocchi è nel fuso di
/// <see cref="IClock.LocalTimeZone"/> del dispositivo che unisce.</para>
/// </summary>
public static class SyncMerger
{
    /// <summary>Margine sul "visto" per una modifica fatta qui: la sua ora si prende in SavingChanges, prima del commit,
    /// quindi un file scritto poco dopo quell'ora può averla letta senza la modifica. La modifica conta come vista dal
    /// pari solo se il nostro file che ha unito è stato scritto almeno questo tempo dopo.</summary>
    // ponytail: un margine fisso, non una garanzia. Il rimedio vero è un cancello fra i salvataggi locali e la scrittura
    // del file d'unione. Se il margine non basta si sbaglia dalla parte sicura: niente "visto", vale la regola dei campi.
    internal const long SeenMarginMs = 5_000;

    public static Task<SyncMergeReport> MergeAsync(
        RecordumDbContext db, SyncSnapshot remote, string localDeviceId, bool receivePhotos, IClock clock, CancellationToken ct) =>
        MergeAsync(db, remote, localDeviceId, receivePhotos, clock, afterEntity: null, ct);

    /// <param name="afterEntity">Solo per i test: chiamato dopo le righe di ogni entità (un'eccezione lì prova il
    /// rollback).</param>
    internal static async Task<SyncMergeReport> MergeAsync(
        RecordumDbContext db, SyncSnapshot remote, string localDeviceId, bool receivePhotos, IClock clock,
        Action<string>? afterEntity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDeviceId);
        if (string.Equals(remote.Manifest.DeviceId, localDeviceId, StringComparison.Ordinal))
            throw new InvalidOperationException("A device cannot merge its own sync file.");

        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            // La connessione aperta da EF (passata dall'interceptor): le funzioni dei trigger leggono questo stato.
            var conn = (SqliteConnection)db.Database.GetDbConnection();
            var state = SyncConnectionState.For(conn);
            await using var efTx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            state.Applying = true;
            try
            {
                var run = new Run(db.Model, conn, (SqliteTransaction)efTx.GetDbTransaction(), remote, localDeviceId, receivePhotos, clock, afterEntity, ct);
                var report = await run.ExecuteAsync().ConfigureAwait(false);
                await efTx.CommitAsync(ct).ConfigureAwait(false);
                return report;
            }
            finally
            {
                state.Applying = false;
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Spareggio per campo (Ruling 20, formato §5 punto 3): confronto ordinale dei byte UTF-8 del testo
    /// canonico del valore.</summary>
    internal static int CompareCanonicalValue(JsonElement a, JsonElement b)
    {
        var ab = Encoding.UTF8.GetBytes(SyncRowCodec.CanonicalValue(a));
        var bb = Encoding.UTF8.GetBytes(SyncRowCodec.CanonicalValue(b));
        return ab.AsSpan().SequenceCompareTo(bb);
    }

    private sealed record Fk(string DepTable, string[] DepCols, string PrincipalTable, string[] PrincipalCols, bool SetNull);

#pragma warning disable CA2100 // SQL costruito solo da identificatori del catalogo e del modello EF; i valori sono parametri
    private sealed class Run(
        IModel model, SqliteConnection conn, SqliteTransaction tx, SyncSnapshot remote, string localDeviceId, bool receivePhotos,
        IClock clock, Action<string>? afterEntity, CancellationToken ct)
    {
        // Chiavi solo locali nella base (sync_base non viaggia), accanto alla riga remota: l'ora della riga di qui
        // dopo l'unione con quel pari e l'orologio di qui quando l'unione l'ha cambiata. Servono alla regola B
        // (via TextSeenAsync → RemoteSawAsync → LastChangeAsync) e a SalvageTextAsync (regola 4, Ruling 17):
        // capire se il pari aveva visto una nostra riga prima di cambiarla o cancellarla.
        private const string LocalMsKey = "_local_ms";
        private const string MergedAtKey = "_merged_at";

        // Madre precedente di habit_node prima di uno spostamento ricevuto: usata da
        // DomainInvariantRepair.ResolvePreviousParentAsync per la riparazione dei cicli.
        private const string PrevParentKey = "_prev_parent_id";

        private const string Ms = "updated_at_ms";
        private const string DeviceName = "last_edited_device_name";
        private const string SettingsEntity = "app_setting";
        private const string HabitNodeEntity = "habit_node";
        private const string NoteEntity = "note";
        private const string HistoryEntity = "note_history";

        private readonly SyncRowCodec _codec = new(model);
        private readonly string _peer = remote.Manifest.DeviceId;
        private readonly Dictionary<string, string[]> _pk = PrimaryKeys(model);
        private readonly List<Fk> _fks = ForeignKeys(model);
        private readonly Dictionary<string, SyncEntity> _entityByTable =
            SyncCatalog.Entities.ToDictionary(e => e.Table, StringComparer.Ordinal);
        private readonly Dictionary<(string Entity, string Id), string> _aliases = [];
        private readonly HashSet<string> _touched = new(StringComparer.Ordinal);
        private int _inserted, _updated, _deleted, _textConflicts, _repaired, _rescued;

        // merged_from del pari per noi: il written_at_ms (orologio di qui) dell'ultimo nostro file che ha unito.
        private readonly long? _seenUntil = remote.Manifest.MergedFrom.TryGetValue(localDeviceId, out var seen) ? seen : null;

        public async Task<SyncMergeReport> ExecuteAsync()
        {
            await ExecAsync("PRAGMA defer_foreign_keys = ON").ConfigureAwait(false);

            var index = SyncCatalog.Entities.Select((e, i) => (e, i)).ToDictionary(x => x.e.PublicName, x => x.i, StringComparer.Ordinal);
            var remoteById = remote.Rows.ToDictionary(
                kv => kv.Key, kv => kv.Value.ToDictionary(r => r.Id, StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var t in remote.Tombstones
                .Where(t => index.ContainsKey(t.Entity))
                .OrderBy(t => t.AliasOf is null ? 1 : 0)
                .ThenByDescending(t => index[t.Entity]))
                await ApplyTombstoneAsync(SyncCatalog.Entities[index[t.Entity]], t, remoteById[t.Entity]).ConfigureAwait(false);

            await ReloadAliasesAsync().ConfigureAwait(false);

            var droppedUniqueIndexes = new List<string>();
            foreach (var entity in SyncCatalog.Entities.Where(e => e.NaturalKey.Count > 0))
                droppedUniqueIndexes.AddRange(await NaturalKeyUnifier.DropUniqueIndexesAsync(conn, tx, entity.Table, ct).ConfigureAwait(false));

            foreach (var entity in SyncCatalog.Entities)
            {
                await MergeRowsAsync(entity).ConfigureAwait(false);
                if (entity.NaturalKey.Count > 0)
                {
                    var unified = await NaturalKeyUnifier.UnifyAsync(conn, tx, model, entity, ct).ConfigureAwait(false);
                    if (unified > 0)
                    {
                        _repaired += unified;
                        await ReloadAliasesAsync().ConfigureAwait(false);
                    }
                }
                afterEntity?.Invoke(entity.PublicName);
            }

            foreach (var entity in SyncCatalog.Entities.Where(e => e.NaturalKey.Count > 0))
            {
                var unified = await NaturalKeyUnifier.UnifyAsync(conn, tx, model, entity, ct).ConfigureAwait(false);
                if (unified > 0)
                {
                    _repaired += unified;
                    await ReloadAliasesAsync().ConfigureAwait(false);
                }
            }

            foreach (var sql in droppedUniqueIndexes)
                await ExecAsync(sql).ConfigureAwait(false);

            await SweepOrphansAsync().ConfigureAwait(false);
            foreach (var entity in SyncCatalog.Entities)
            {
                await ExecAsync(
                    $"DELETE FROM sync_tombstone WHERE entity = @e AND alias_of IS NULL AND EXISTS (SELECT 1 FROM {Q(entity.Table)} AS r " +
                    $"WHERE {Identity(entity, "r")} = sync_tombstone.entity_id AND r.updated_at_ms > sync_tombstone.deleted_at_ms)",
                    ("@e", entity.PublicName)).ConfigureAwait(false);
                await ExecAsync(
                    $"DELETE FROM sync_field_ms WHERE entity = @e AND NOT EXISTS (SELECT 1 FROM {Q(entity.Table)} AS r " +
                    $"WHERE {Identity(entity, "r")} = sync_field_ms.entity_id)",
                    ("@e", entity.PublicName)).ConfigureAwait(false);
            }

            _repaired += await DomainInvariantRepair.RepairAsync(conn, tx, clock, ct).ConfigureAwait(false);

            return new SyncMergeReport(_inserted, _updated, _deleted, _textConflicts, _repaired, _rescued);
        }

        // Spec §8.3 "cancellazione contro modifica": vince l'azione più recente; a parità la cancellazione.
        // La lapide si copia sempre (si tiene la più recente): se qui la riga vince, il passo 4 la toglie.
        // Una lapide con alias_of non è una cancellazione: la riga locale (e i suoi riferimenti) viene unificata da NaturalKeyUnifier.
        private async Task ApplyTombstoneAsync(SyncEntity entity, SyncTombstoneRow t, Dictionary<string, SyncRow> remoteRows)
        {
            if (!Travels(entity, t.EntityId)) return;
            if (t.AliasOf is not null)
            {
                // ponytail: una cancellazione di un id già unificato non cancella il vincitore (la cancellazione si
                // perde, la riga unificata resta su tutti i dispositivi).
                await ExecAsync(NaturalKeyUnifier.UpsertTombstoneSql,
                    ("@e", entity.PublicName), ("@id", t.EntityId), ("@at", t.DeletedAtMs), ("@dev", t.DeviceId), ("@alias", t.AliasOf)).ConfigureAwait(false);

                if (entity.KeyColumns is ["id"])
                {
                    var localExists = await ScalarAsync(
                        $"SELECT 1 FROM {Q(entity.Table)} WHERE \"id\" = @id", ("@id", t.EntityId)).ConfigureAwait(false) is not null;
                    if (localExists)
                    {
                        await ReloadAliasesAsync().ConfigureAwait(false);
                        var targetId = ResolveAlias(entity.PublicName, t.EntityId);
                        if (!string.Equals(targetId, t.EntityId, StringComparison.Ordinal))
                        {
                            var targetExists = await ScalarAsync(
                                $"SELECT 1 FROM {Q(entity.Table)} WHERE \"id\" = @id", ("@id", targetId)).ConfigureAwait(false) is not null;
                            if (!targetExists)
                                await NaturalKeyUnifier.RenameAsync(conn, tx, model, entity, t.EntityId, targetId, ct).ConfigureAwait(false);
                        }
                    }
                }
                return;
            }

            // Il pari ha cancellato e poi rifatto la riga: decide la riga, al passo 2.
            if (remoteRows.TryGetValue(t.EntityId, out var again) && RowMs(entity, again) > t.DeletedAtMs) return;

            var local = await ScalarAsync(
                $"SELECT updated_at_ms FROM {Q(entity.Table)} WHERE {Identity(entity, null)} = @id", ("@id", t.EntityId)).ConfigureAwait(false);
            // La riga di qui più recente resta, ma la lapide si copia lo stesso: se una madre cancellata poi la porta
            // via, tutti e due i dispositivi devono avere la stessa lapide.
            var rowWins = local is long ms && ms > t.DeletedAtMs;
            if (local is not null && !rowWins)
            {
                if (entity.Fields.Any(f => f.Kind == SyncFieldKind.Text)
                    && await LocalRowAsync(entity, t.EntityId).ConfigureAwait(false) is { } gone)
                    await SalvageTextAsync(entity, t.EntityId, gone, await BaseAsync(entity, t.EntityId).ConfigureAwait(false), t.DeletedAtMs, deletedHere: false).ConfigureAwait(false);
                foreach (var key in await KeysAsync(entity.Table, $"{Identity(entity, null)} = @id", ("@id", t.EntityId)).ConfigureAwait(false))
                    _deleted += await DeleteRowAsync(entity.Table, key, []).ConfigureAwait(false);
            }

            await ExecAsync(NaturalKeyUnifier.UpsertTombstoneSql,
                ("@e", entity.PublicName), ("@id", t.EntityId), ("@at", t.DeletedAtMs), ("@dev", t.DeviceId), ("@alias", t.AliasOf)).ConfigureAwait(false);
            if (rowWins) return;
            await ExecAsync("DELETE FROM sync_base WHERE peer_device_id = @p AND entity = @e AND entity_id = @id",
                ("@p", _peer), ("@e", entity.PublicName), ("@id", t.EntityId)).ConfigureAwait(false);
        }

        private async Task MergeRowsAsync(SyncEntity entity)
        {
            var rows = remote.Rows[entity.PublicName];
            if (rows.Count == 0) return;

            var local = (await SyncArchiveWriter.ReadEntityRowsAsync(conn, tx, _codec, entity, ct).ConfigureAwait(false))
                .ToDictionary(r => r.Id, StringComparer.Ordinal);
            var tombs = await TombstonesAsync(entity).ConfigureAwait(false);
            var bases = entity.IsHistory ? [] : await BasesAsync(entity).ConfigureAwait(false);

            foreach (var row in rows)
            {
                if (!Travels(entity, row.Id)) continue;
                var r = Known(entity, row);
                var rowId = RewriteAliases(entity, row.Id, r);
                var rms = RowMs(entity, row);
                if (tombs.TryGetValue(rowId, out var deletedAt) && deletedAt >= rms)
                {
                    await SalvageTextAsync(entity, rowId, r, bases.GetValueOrDefault(rowId), deletedAt, deletedHere: true).ConfigureAwait(false);
                    continue;
                }

                var newBase = new Dictionary<string, JsonElement>(r, StringComparer.Ordinal);
                var after = r;
                var produced = true; // questa unione ha dato alla riga la sua versione attuale
                Dictionary<string, JsonElement>? oldBase = null;
                if (!local.TryGetValue(rowId, out var l))
                {
                    await InsertAsync(entity, rowId, r).ConfigureAwait(false);
                    await MergePhotosAsync(entity, rowId, r, inserted: true, rms, row.FieldMs, mergedFieldMs: null, b: null, newBase).ConfigureAwait(false);
                    var insertedFieldMs = row.FieldMs
                        .Where(kv => kv.Value < rms)
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                    await WriteFieldMsAsync(entity.PublicName, rowId, insertedFieldMs).ConfigureAwait(false);
                    _inserted++;
                    _touched.Add(entity.Table);
                    local[rowId] = new SyncRow(rowId, after) { FieldMs = insertedFieldMs };
                }
                else
                {
                    if (entity.IsHistory) continue; // storici: insiemi, una riga che c'è già non cambia
                    var b = oldBase = bases.GetValueOrDefault(rowId);
                    // Un file vecchio dello stesso pari (la base ha già visto una versione più recente) non fa tornare
                    // indietro nulla.
                    if (b is not null && b.TryGetValue(Ms, out var bms) && bms.GetInt64() > rms) continue;

                    var lms = l.Fields[Ms].GetInt64();
                    var timedFields = SyncCatalog.TimedFields(entity);
                    var mergedFieldMs = new Dictionary<string, long>(timedFields.Count, StringComparer.Ordinal);
                    foreach (var tf in timedFields)
                        mergedFieldMs[tf.PublicName] = l.FieldMs.TryGetValue(tf.PublicName, out var lfm) ? lfm : lms;

                    var tookRemotePhoto = await MergePhotosAsync(entity, rowId, r, inserted: false, rms, row.FieldMs, mergedFieldMs, b, newBase).ConfigureAwait(false);
                    var seen = await TextSeenAsync(entity, rowId, l.Fields, r, lms).ConfigureAwait(false);
                    var (changes, newRowMs) = MergeFields(entity, l.Fields, r, row.FieldMs, b, lms, rms, mergedFieldMs, seen);
                    if (entity.PublicName == HabitNodeEntity && changes.ContainsKey("parent_id") && l.Fields.TryGetValue("parent_id", out var prevParent))
                        newBase[PrevParentKey] = prevParent;
                    if (changes.Count > 0) await UpdateAsync(entity, r, changes).ConfigureAwait(false);
                    var desiredFieldMs = mergedFieldMs.Where(kv => kv.Value < newRowMs).ToList();
                    if (FieldMsDiffers(l.FieldMs, desiredFieldMs))
                        await WriteFieldMsAsync(entity.PublicName, rowId, desiredFieldMs).ConfigureAwait(false);

                    produced = changes.Count > 0 || tookRemotePhoto;
                    if (produced)
                    {
                        _updated++;
                        _touched.Add(entity.Table);
                        // Le basi degli altri pari non descrivono più questa versione della riga.
                        await ExecAsync(
                            $"UPDATE sync_base SET row_json = json_remove(row_json, '$.{LocalMsKey}') " +
                            "WHERE entity = @e AND entity_id = @id AND peer_device_id <> @p",
                            ("@e", entity.PublicName), ("@id", rowId), ("@p", _peer)).ConfigureAwait(false);
                    }

                    var merged = new Dictionary<string, JsonElement>(l.Fields, StringComparer.Ordinal);
                    foreach (var (k, v) in changes) merged[k] = v;
                    after = merged;
                    local[rowId] = new SyncRow(rowId, after)
                    {
                        FieldMs = desiredFieldMs.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                    };
                }

                if (!entity.IsHistory)
                {
                    newBase[LocalMsKey] = after[Ms];
                    // L'ora d'unione solo se questa unione ha cambiato la riga; un'unione che la lascia com'è porta avanti
                    // quella di prima (se descrive ancora questa versione), altrimenti niente: l'ora di cambio di una
                    // modifica fatta qui resta la sua (LastChangeAsync, usata da SalvageTextAsync).
                    if (produced)
                        newBase[MergedAtKey] = JsonSerializer.SerializeToElement(clock.UtcNowMs);
                    else if (oldBase is not null && oldBase.TryGetValue(LocalMsKey, out var oldMs) && oldMs.GetInt64() == after[Ms].GetInt64()
                        && oldBase.TryGetValue(MergedAtKey, out var oldAt))
                        newBase[MergedAtKey] = oldAt;

                    await ExecAsync(
                        "INSERT OR REPLACE INTO sync_base (peer_device_id, entity, entity_id, row_json) VALUES (@p, @e, @id, @json)",
                        ("@p", _peer), ("@e", entity.PublicName), ("@id", rowId), ("@json", JsonSerializer.Serialize(newBase))).ConfigureAwait(false);
                    bases[rowId] = newBase;
                }
            }
        }

        /// <summary>Ruling 20: campo per campo con l'ora di ciascun campo (<c>sync_field_ms</c> / <c>_field_ms</c>); a parità
        /// vince il valore con la forma canonica minore. Per i campi <see cref="SyncFieldKind.Text"/> cambiati da tutte e
        /// due le parti rispetto alla base (o senza base) vale la regola B. <c>last_edited_device_name</c> segue il testo
        /// (valore e ora). L'ora della riga è il massimo delle ore dei suoi campi.</summary>
        private (Dictionary<string, JsonElement> Changes, long NewRowMs) MergeFields(
            SyncEntity entity, IReadOnlyDictionary<string, JsonElement> l, Dictionary<string, JsonElement> r,
            IReadOnlyDictionary<string, long> rFieldMs, Dictionary<string, JsonElement>? b,
            long lms, long rms, Dictionary<string, long> mergedFieldMs, TextSeen seen)
        {
            var changes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            bool? textFromRemote = null; // nel catalogo il campo Text viene prima di last_edited_device_name
            long textMs = 0;

            foreach (var field in entity.Fields)
            {
                var name = field.PublicName;
                if (field.Kind == SyncFieldKind.Photo || name == Ms || entity.KeyColumns.Contains(field.Column)
                    || !r.TryGetValue(name, out var rv))
                    continue;

                var lv = l[name];
                var lf = mergedFieldMs[name];
                var rf = rFieldMs.TryGetValue(name, out var rfm) ? rfm : rms;

                if (name == DeviceName && textFromRemote is bool fromRemote)
                {
                    mergedFieldMs[name] = textMs;
                    if (fromRemote && !JsonElement.DeepEquals(lv, rv))
                        changes[name] = rv;
                    continue;
                }

                if (JsonElement.DeepEquals(lv, rv))
                {
                    mergedFieldMs[name] = Math.Max(lf, rf);
                    continue;
                }

                if (field.Kind == SyncFieldKind.Text)
                {
                    // Regola B: vince il testo con l'ora di campo più recente. Il perdente si scarta solo se chi ha scritto
                    // il vincente l'aveva già visto (e l'ha tolto, per esempio i blocchi di un testo unito): ogni suo pezzo
                    // è nel vincente o nella base di questo pari; se il perdente è il nostro, il pari ha visto la nostra
                    // versione (merged_from); se è il suo, ogni pezzo sta in una nostra base. Se no va in coda: un testo
                    // non si perde mai, al più un blocco tolto nello stesso giro del conflitto torna una volta.
                    JsonElement bv = default;
                    var hasBase = b is not null && b.TryGetValue(name, out bv);
                    var remoteTextWins = rf > lf || (rf == lf && CompareCanonicalValue(rv, lv) < 0);
                    var (winnerText, loserText) = remoteTextWins ? (Str(rv), Str(lv)) : (Str(lv), Str(rv));
                    var discard = (hasBase && TextConflictBlock.Covers([winnerText, Str(bv)], loserText))
                        || (remoteTextWins ? seen.RemoteSawLocal : TextConflictBlock.Covers([winnerText, .. seen.BaseTexts], loserText));
                    if (!discard)
                    {
                        var (topFromRemote, merged) = MergeText(lv, rv, l, r, remoteTextWins, lf, rf);
                        textFromRemote = topFromRemote;
                        // Una delle due versioni (anche quella con l'ora più vecchia, se contiene l'altra) prende l'ora
                        // più recente: un'ora di campo non scende mai. Un testo nuovo batte tutte e due.
                        textMs = merged is null ? Math.Max(lf, rf) : Math.Max(lf, rf) + 1;
                        mergedFieldMs[name] = textMs;
                        if (merged is not null)
                            changes[name] = JsonSerializer.SerializeToElement(merged);
                        else if (topFromRemote)
                            changes[name] = rv;
                        continue;
                    }
                }

                var useRemote = rf > lf || (rf == lf && CompareCanonicalValue(rv, lv) < 0);
                var winMs = useRemote ? rf : lf;
                mergedFieldMs[name] = winMs;
                if (useRemote)
                    changes[name] = rv;
                if (field.Kind == SyncFieldKind.Text)
                {
                    textFromRemote = useRemote;
                    textMs = winMs;
                }
            }

            // Anche l'ora della riga remota: una riga può essere più recente di tutti i suoi campi (un UPDATE che non ne
            // cambia nessuno), e il massimo delle due ore è quello che tutti e due i dispositivi calcolano.
            var newRowMs = Math.Max(Math.Max(lms, rms), mergedFieldMs.Count > 0 ? mergedFieldMs.Values.Max() : 0);
            if (newRowMs != lms)
                changes[Ms] = JsonSerializer.SerializeToElement(newRowMs);
            return (changes, newRowMs);
        }

        private sealed record TextSeen(bool RemoteSawLocal, IReadOnlyList<string> BaseTexts);

        private static readonly TextSeen NothingSeen = new(false, []);

        /// <summary>Per la regola B, solo se il campo testo differisce: se il pari ha visto la nostra versione della riga
        /// e i testi di tutte le nostre basi per questa riga (di qualunque pari).</summary>
        private async Task<TextSeen> TextSeenAsync(
            SyncEntity entity, string id, IReadOnlyDictionary<string, JsonElement> l, Dictionary<string, JsonElement> r, long lms)
        {
            var text = entity.Fields.FirstOrDefault(f => f.Kind == SyncFieldKind.Text);
            if (text is null || !r.TryGetValue(text.PublicName, out var rv) || JsonElement.DeepEquals(l[text.PublicName], rv))
                return NothingSeen;
            return new TextSeen(
                await RemoteSawAsync(entity, id, lms).ConfigureAwait(false),
                await BaseTextsAsync(entity, id, text.PublicName).ConfigureAwait(false));
        }

        /// <summary>Vero se il pari ha unito un nostro file che conteneva già la nostra versione della riga: usata da
        /// <see cref="SalvageTextAsync"/> (regola 4) e dalla regola B. Senza <c>merged_from</c> per noi: falso.</summary>
        private async Task<bool> RemoteSawAsync(SyncEntity entity, string id, long lms) =>
            _seenUntil is long seen && await LastChangeAsync(entity, id, lms).ConfigureAwait(false) <= seen;

        /// <summary>Quando (orologio di qui) la riga ha preso la versione <paramref name="lms"/> (usata dalla regola B
        /// via <see cref="TextSeenAsync"/> → <see cref="RemoteSawAsync"/> e da <see cref="SalvageTextAsync"/>,
        /// regola 4): l'ora dell'unione che l'ha prodotta; altrimenti è una modifica fatta qui e la sua ora è
        /// <paramref name="lms"/> più <see cref="SeenMarginMs"/>.</summary>
        // ponytail: una riga presa da un pari la cui base poi sparisce torna a lms, l'ora di un altro orologio. Succede
        // solo dopo un ripristino, che rigenera device_id: allora merged_from per noi manca e questa ora non si usa.
        private async Task<long> LastChangeAsync(SyncEntity entity, string id, long lms) =>
            await ScalarAsync(
                $"SELECT MIN(json_extract(row_json, '$.{MergedAtKey}')) FROM sync_base " +
                $"WHERE entity = @e AND entity_id = @id AND json_extract(row_json, '$.{LocalMsKey}') = @ms",
                ("@e", entity.PublicName), ("@id", id), ("@ms", lms)).ConfigureAwait(false) is long at ? at : lms + SeenMarginMs;

        /// <summary>Regola B per un testo cambiato da tutte e due le parti (o senza base): il vincente con in coda il
        /// perdente. Se il risultato è già una delle due versioni (una contiene l'altra, anche come testo in cima a una
        /// voce già unita) torna da che parte stare e nessun testo nuovo.</summary>
        private (bool FromRemote, string? Merged) MergeText(
            JsonElement lv, JsonElement rv, IReadOnlyDictionary<string, JsonElement> l, Dictionary<string, JsonElement> r,
            bool remoteWins, long lf, long rf)
        {
            var (winner, loser) = remoteWins ? (Str(rv), Str(lv)) : (Str(lv), Str(rv));
            var loserDevice = remoteWins ? l.GetValueOrDefault(DeviceName) : r.GetValueOrDefault(DeviceName);
            var merged = TextConflictBlock.Merge(winner, loser, NullableStr(loserDevice), remoteWins ? lf : rf, clock.LocalTimeZone);
            if (merged == winner) return (remoteWins, null);
            if (merged == loser) return (!remoteWins, null);
            _textConflicts++;
            return (remoteWins, merged);
        }

        private static string Str(JsonElement el) => NullableStr(el) ?? "";

        private static string? NullableStr(JsonElement el) => el.ValueKind == JsonValueKind.String ? el.GetString() : null;

        /// <summary>Spec §8.3 "cancellazione contro modifica": vince la cancellazione, ma il testo che chi ha cancellato
        /// non aveva diventa una nota rapida della stessa abitudine e dello stesso giorno, col blocco della regola B.
        /// <list type="bullet">
        /// <item>La riga di qui, cancellata dal pari: il pari non aveva il testo se non è tutto nella sua base; senza
        /// base, se la riga è cambiata dopo l'ultimo nostro file che il pari ha unito (<c>merged_from</c>, Ruling 17), o
        /// se il pari non lo dice (spec §8.4: senza base un testo diverso è un conflitto).</item>
        /// <item>La riga del pari, cancellata qui: avevamo il testo se sta in una base qualunque di questa riga (le basi
        /// restano dopo una cancellazione fatta qui). Senza nessuna base la riga l'avevamo creata noi e torna indietro.</item>
        /// </list>
        /// L'id della nota è deterministico (entità, id, ora e testo): due dispositivi che salvano lo stesso testo fanno
        /// la stessa nota, e una nota salvata e poi cancellata (la sua lapide) non torna.</summary>
        private async Task SalvageTextAsync(
            SyncEntity entity, string id, IReadOnlyDictionary<string, JsonElement> row,
            Dictionary<string, JsonElement>? b, long deletedAtMs, bool deletedHere)
        {
            var textField = entity.Fields.FirstOrDefault(f => f.Kind == SyncFieldKind.Text);
            if (textField is null) return;
            var text = Str(row.GetValueOrDefault(textField.PublicName));
            if (text.Length == 0) return;
            var ms = row[Ms].GetInt64();
            if (deletedHere)
            {
                var known = await BaseTextsAsync(entity, id, textField.PublicName).ConfigureAwait(false);
                if (known.Count == 0 || known.Any(k => TextConflictBlock.Contains(k, text))) return;
            }
            else if (b is not null)
            {
                if (TextConflictBlock.Contains(Str(b.GetValueOrDefault(textField.PublicName)), text)) return;
            }
            else if (await RemoteSawAsync(entity, id, ms).ConfigureAwait(false))
            {
                return;
            }

            var noteId = SalvageId(entity.PublicName, id, ms, text);
            // Già salvato, o salvato e poi cancellato (la lapide qui o nel file che stiamo unendo).
            if (remote.Tombstones.Any(t => t.Entity == NoteEntity && t.EntityId == noteId)
                || await ScalarAsync("SELECT 1 FROM sync_tombstone WHERE entity = @e AND entity_id = @id",
                    ("@id", noteId), ("@e", NoteEntity)).ConfigureAwait(false) is not null)
                return;

            var device = NullableStr(row.GetValueOrDefault(DeviceName));
            var isNote = entity.PublicName == NoteEntity;
            var note = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["id"] = JsonSerializer.SerializeToElement(noteId),
                ["habit_node_id"] = row["habit_node_id"],
                ["completion_id"] = JsonSerializer.SerializeToElement<string?>(null),
                ["category"] = JsonSerializer.SerializeToElement(nameof(NoteCategory.Veloce)),
                // Lo stesso giorno: quello della nota, o per una registrazione il suo istante (una nota senza giorno si
                // colloca con created_at). ponytail: per una registrazione target_date resta null, quindi la nota
                // compare negli elenchi delle note (NoteAnchor la colloca con created_at) ma non nel pannello del
                // giorno del calendario. Il giorno logico vuole l'ora di reset (DayBoundary), che qui, dentro la
                // transazione dell'unione, non si risolve.
                ["target_date"] = isNote ? row["target_date"] : JsonSerializer.SerializeToElement<string?>(null),
                ["body_md"] = JsonSerializer.SerializeToElement(TextConflictBlock.Merge("", text, device, ms, clock.LocalTimeZone)),
                ["is_sensitive"] = isNote ? row["is_sensitive"] : JsonSerializer.SerializeToElement(false),
                ["created_at"] = isNote ? row["created_at"] : row["completed_at"],
                ["last_edited_device_name"] = row.GetValueOrDefault(DeviceName),
                [Ms] = JsonSerializer.SerializeToElement(deletedAtMs),
            };
            var notes = SyncCatalog.Entities.First(e => e.PublicName == NoteEntity);
            if (await InsertAsync(notes, noteId, note, ignoreExisting: true).ConfigureAwait(false) == 0) return; // già salvato
            // Come NoteService.CreateAsync: una nota nuova con un testo apre la sua cronologia. Id deterministico come
            // la nota, così due dispositivi che salvano lo stesso testo non fanno due versioni.
            var historyId = SalvageId(HistoryEntity, noteId, deletedAtMs, "");
            await InsertAsync(SyncCatalog.Entities.First(e => e.PublicName == HistoryEntity), historyId, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["id"] = JsonSerializer.SerializeToElement(historyId),
                ["note_id"] = note["id"],
                ["body_md"] = note["body_md"],
                ["created_at"] = note["created_at"],
                [Ms] = note[Ms],
            }, ignoreExisting: true).ConfigureAwait(false);
            _rescued++;
            _touched.Add(notes.Table); // abitudine cancellata anche lei: la nota segue (passo 3)
        }

        private static string SalvageId(string entity, string id, long ms, string text)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Create(CultureInfo.InvariantCulture, $"recordum-sync-salvage\0{entity}\0{id}\0{ms}\0{text}")));
            return new Guid(hash.AsSpan(0, 16)).ToString("D").ToUpperInvariant();
        }

        private async Task<List<string>> BaseTextsAsync(SyncEntity entity, string id, string field)
        {
            await using var cmd = Command($"SELECT json_extract(row_json, '$.{field}') FROM sync_base WHERE entity = @e AND entity_id = @id",
                ("@e", entity.PublicName), ("@id", id));
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var texts = new List<string>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) texts.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));
            return texts;
        }

        private async Task<Dictionary<string, JsonElement>?> BaseAsync(SyncEntity entity, string id) =>
            await ScalarAsync("SELECT row_json FROM sync_base WHERE peer_device_id = @p AND entity = @e AND entity_id = @id",
                ("@p", _peer), ("@e", entity.PublicName), ("@id", id)).ConfigureAwait(false) is string json
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                : null;

        /// <summary>La riga di qui in forma pubblica (senza foto), o <c>null</c>.</summary>
        private async Task<IReadOnlyDictionary<string, JsonElement>?> LocalRowAsync(SyncEntity entity, string id)
        {
            var cols = entity.Fields.Where(f => f.Kind != SyncFieldKind.Photo).Select(f => f.Column).ToArray();
            await using var cmd = Command(
                $"SELECT {string.Join(", ", cols.Select(Q))} FROM {Q(entity.Table)} WHERE {Identity(entity, null)} = @id", ("@id", id));
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            var dbRow = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < cols.Length; i++) dbRow[cols[i]] = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false) ? null : reader.GetValue(i);
            return _codec.ToPublic(entity, dbRow).Fields;
        }

        /// <summary>Le foto: viaggiano a parte, una per volta, con la stessa regola per ora di campo (Ruling 20) e
        /// l'hash SHA-256 (esadecimale minuscolo) come valore. Una foto che il file non porta (o un originale con
        /// <c>receivePhotos</c> spento) non è un cambiamento; una foto che qui manca si riempie da quella remota.</summary>
        private async Task<bool> MergePhotosAsync(
            SyncEntity entity, string id, Dictionary<string, JsonElement> r, bool inserted, long rms,
            IReadOnlyDictionary<string, long> rFieldMs, Dictionary<string, long>? mergedFieldMs,
            Dictionary<string, JsonElement>? b, Dictionary<string, JsonElement> newBase)
        {
            var wrote = false;
            var unchangedSinceBase = b is not null && b.TryGetValue(Ms, out var bms) && bms.GetInt64() == rms;
            foreach (var field in entity.Fields.Where(f => f.Kind == SyncFieldKind.Photo))
            {
                var thumbnail = field.PublicName == "thumbnail"; // l'altra colonna Photo (content) è l'originale
                if (!thumbnail && !receivePhotos) continue;

                var lf = !inserted ? mergedFieldMs![field.PublicName] : 0;
                var rf = rFieldMs.TryGetValue(field.PublicName, out var rfm) ? rfm : rms;

                // Qui la foto c'è e la remota non può vincere: più vecchia, o alla stessa ora e già vista (la riga remota
                // è quella della base, quindi lo spareggio d'hash è già stato deciso). Niente da leggere né da hashare.
                // Non basta la sola ora della riga: una foto può cambiare senza che salga (un altro campo è più recente).
                if (!inserted && (rf < lf || (rf == lf && unchangedSinceBase))
                    && await LocalPhotoPresentAsync(entity, field, r).ConfigureAwait(false))
                {
                    if (b is not null && b.TryGetValue(field.PublicName, out var oldHash)) newBase[field.PublicName] = oldHash;
                    continue;
                }

                var stream = remote.OpenPhoto(id, thumbnail);
                if (stream is null) continue;
                byte[] bytes;
                await using (stream.ConfigureAwait(false))
                using (var buffer = new MemoryStream())
                {
                    await stream.CopyToAsync(buffer, ct).ConfigureAwait(false);
                    bytes = buffer.ToArray();
                }

                var remoteHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                newBase[field.PublicName] = JsonSerializer.SerializeToElement(remoteHash);

                if (!inserted)
                {
                    var localHash = await LocalPhotoHashAsync(entity, field, r).ConfigureAwait(false);
                    if (localHash == remoteHash)
                    {
                        mergedFieldMs![field.PublicName] = Math.Max(lf, rf);
                        continue;
                    }

                    if (localHash is not null)
                    {
                        var useRemote = rf > lf || (rf == lf && StringComparer.Ordinal.Compare(remoteHash, localHash) < 0);
                        if (!useRemote) continue;
                    }

                    mergedFieldMs![field.PublicName] = rf;
                }

                var (where, keys) = KeyWhere(entity, r);
                await ExecAsync($"UPDATE {Q(entity.Table)} SET {Q(field.Column)} = @blob WHERE {where}", [.. keys, ("@blob", bytes)]).ConfigureAwait(false);
                wrote = true;
            }

            return wrote;
        }

        private async Task<bool> LocalPhotoPresentAsync(SyncEntity entity, SyncField field, Dictionary<string, JsonElement> r)
        {
            var (where, keys) = KeyWhere(entity, r);
            return await ScalarAsync($"SELECT {Q(field.Column)} IS NOT NULL FROM {Q(entity.Table)} WHERE {where}", keys).ConfigureAwait(false) is 1L;
        }

        private async Task<string?> LocalPhotoHashAsync(SyncEntity entity, SyncField field, Dictionary<string, JsonElement> r)
        {
            var (where, keys) = KeyWhere(entity, r);
            await using var cmd = Command($"SELECT {Q(field.Column)} FROM {Q(entity.Table)} WHERE {where}", keys);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false) || await reader.IsDBNullAsync(0, ct).ConfigureAwait(false))
                return null;
            await using var blob = reader.GetStream(0);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(blob, ct).ConfigureAwait(false));
        }

        /// <param name="ignoreExisting">Una riga con la stessa chiave c'è già: niente (torna 0).</param>
        private async Task<int> InsertAsync(SyncEntity entity, string id, Dictionary<string, JsonElement> r, bool ignoreExisting = false)
        {
            var values = _codec.ToDb(entity, new SyncRow(id, r)).ToList();
            var cols = string.Join(", ", values.Select(v => Q(v.Key)));
            var pars = values.Select((v, i) => ($"@v{i}", v.Value)).ToArray();
            return await ExecAsync(
                $"INSERT INTO {Q(entity.Table)} ({cols}) VALUES ({string.Join(", ", pars.Select(p => p.Item1))})" +
                (ignoreExisting ? " ON CONFLICT DO NOTHING" : ""), pars).ConfigureAwait(false);
        }

        private async Task UpdateAsync(SyncEntity entity, Dictionary<string, JsonElement> r, Dictionary<string, JsonElement> changes)
        {
            var values = _codec.ToDb(entity, new SyncRow("", changes)).ToList();
            var (where, keys) = KeyWhere(entity, r);
            var pars = values.Select((v, i) => ($"@v{i}", v.Value)).ToArray();
            var set = string.Join(", ", values.Select((v, i) => $"{Q(v.Key)} = @v{i}"));
            await ExecAsync($"UPDATE {Q(entity.Table)} SET {set} WHERE {where}", [.. pars, .. keys]).ConfigureAwait(false);
        }

        /// <summary>Spec §8.3 "cancellazione di una madre": un figlio la cui madre non c'è più la segue (cancellato, o
        /// la FK a null dove la cascade locale farebbe così). Solo le tabelle toccate da questa unione possono averne.</summary>
        private async Task SweepOrphansAsync()
        {
            foreach (var entity in SyncCatalog.Entities.Where(e => _touched.Contains(e.Table)))
            {
                foreach (var fk in _fks.Where(f => f.DepTable == entity.Table))
                {
                    var orphan = string.Join(" AND ", fk.DepCols.Select(c => $"d.{Q(c)} IS NOT NULL")) +
                        $" AND NOT EXISTS (SELECT 1 FROM {Q(fk.PrincipalTable)} AS p WHERE " +
                        string.Join(" AND ", fk.PrincipalCols.Zip(fk.DepCols, (p, d) => $"p.{Q(p)} = d.{Q(d)}")) + ")";
                    if (fk.SetNull)
                    {
                        var set = string.Join(", ", fk.DepCols.Select(c => $"{Q(c)} = NULL"));
                        await ExecAsync($"UPDATE {Q(fk.DepTable)} AS d SET {set} WHERE {orphan}").ConfigureAwait(false);
                        continue;
                    }

                    foreach (var key in await KeysAsync(fk.DepTable, orphan, alias: "d").ConfigureAwait(false))
                        _deleted += await DeleteRowAsync(fk.DepTable, key, []).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Cancella una riga e, prima, chi la riferisce con una FK che non mette a null (RESTRICT non aspetta
        /// la fine della transazione). Con <c>Applying</c> acceso i figli non lasciano lapidi: il file del pari porta le
        /// sue, e ogni dispositivo applica la stessa regola alla madre.</summary>
        private async Task<int> DeleteRowAsync(string table, object?[] key, HashSet<string> seen)
        {
            if (!seen.Add(table + "\0" + string.Join("|", key))) return 0;
            var pk = _pk[table];
            var count = 0;
            foreach (var fk in _fks.Where(f => f.PrincipalTable == table && !f.SetNull))
            {
                var values = fk.PrincipalCols.Select(c => key[Array.IndexOf(pk, c)]).ToArray();
                var where = string.Join(" AND ", fk.DepCols.Select((c, i) => $"{Q(c)} = @k{i}"));
                var children = await KeysAsync(fk.DepTable, where, null, values.Select((v, i) => ($"@k{i}", v)).ToArray()).ConfigureAwait(false);
                foreach (var child in children)
                    count += await DeleteRowAsync(fk.DepTable, child, seen).ConfigureAwait(false);
            }

            var self = string.Join(" AND ", pk.Select((c, i) => $"{Q(c)} = @k{i}"));
            return count + await ExecAsync($"DELETE FROM {Q(table)} WHERE {self}", key.Select((v, i) => ($"@k{i}", v)).ToArray()).ConfigureAwait(false);
        }

        private async Task<List<object?[]>> KeysAsync(string table, string where, string? alias, (string, object?)[] pars)
        {
            var pk = _pk[table];
            var prefix = alias is null ? "" : alias + ".";
            await using var cmd = Command(
                $"SELECT {string.Join(", ", pk.Select(c => prefix + Q(c)))} FROM {Q(table)}{(alias is null ? "" : " AS " + alias)} WHERE {where}", pars);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var keys = new List<object?[]>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var key = new object?[pk.Length];
                for (var i = 0; i < key.Length; i++) key[i] = reader.GetValue(i);
                keys.Add(key);
            }
            return keys;
        }

        private Task<List<object?[]>> KeysAsync(string table, string where, string alias) => KeysAsync(table, where, alias, []);

        private Task<List<object?[]>> KeysAsync(string table, string where, (string, object?) par) => KeysAsync(table, where, null, [par]);

        private async Task<Dictionary<string, long>> TombstonesAsync(SyncEntity entity)
        {
            await using var cmd = Command("SELECT entity_id, deleted_at_ms FROM sync_tombstone WHERE entity = @e AND alias_of IS NULL", ("@e", entity.PublicName));
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var map = new Dictionary<string, long>(StringComparer.Ordinal);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) map[reader.GetString(0)] = reader.GetInt64(1);
            return map;
        }

        private async Task ReloadAliasesAsync()
        {
            _aliases.Clear();
            await using var cmd = Command("SELECT entity, entity_id, alias_of FROM sync_tombstone WHERE alias_of IS NOT NULL");
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                _aliases[(reader.GetString(0), reader.GetString(1))] = reader.GetString(2);
        }

        private string ResolveAlias(string entityName, string id)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { id };
            while (_aliases.TryGetValue((entityName, id), out var next) && visited.Add(next))
                id = next;
            return id;
        }

        private string RewriteAliases(SyncEntity entity, string rowId, Dictionary<string, JsonElement> r)
        {
            if (_aliases.Count == 0) return rowId;

            foreach (var fk in _fks.Where(f => f.DepTable == entity.Table && f.DepCols.Length == 1))
            {
                if (!_entityByTable.TryGetValue(fk.PrincipalTable, out var principal)) continue;
                var fkField = entity.Fields.FirstOrDefault(f => f.Column == fk.DepCols[0])?.PublicName;
                if (fkField is null || !r.TryGetValue(fkField, out var val) || val.ValueKind != JsonValueKind.String) continue;
                var oldTarget = val.GetString()!;
                var newTarget = ResolveAlias(principal.PublicName, oldTarget);
                if (!string.Equals(newTarget, oldTarget, StringComparison.Ordinal))
                    r[fkField] = JsonSerializer.SerializeToElement(newTarget);
            }

            if (entity.KeyColumns.Count == 1)
            {
                var keyField = entity.Fields.First(f => f.Column == entity.KeyColumns[0]).PublicName;
                var resolved = ResolveAlias(entity.PublicName, rowId);
                if (!string.Equals(resolved, rowId, StringComparison.Ordinal))
                {
                    r[keyField] = JsonSerializer.SerializeToElement(resolved);
                    return resolved;
                }
                return rowId;
            }

            return string.Join("|", entity.KeyColumns.Select(c =>
            {
                var pub = entity.Fields.First(f => f.Column == c).PublicName;
                return Str(r[pub]);
            }));
        }

        private async Task<Dictionary<string, Dictionary<string, JsonElement>>> BasesAsync(SyncEntity entity)
        {
            await using var cmd = Command("SELECT entity_id, row_json FROM sync_base WHERE peer_device_id = @p AND entity = @e",
                ("@p", _peer), ("@e", entity.PublicName));
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var map = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.Ordinal);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                map[reader.GetString(0)] = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(reader.GetString(1))!;
            return map;
        }

        private (string Where, (string, object?)[] Pars) KeyWhere(SyncEntity entity, Dictionary<string, JsonElement> r)
        {
            var keyFields = entity.KeyColumns.Select(c => entity.Fields.First(f => f.Column == c)).ToList();
            var values = _codec.ToDb(entity, new SyncRow("", keyFields.ToDictionary(f => f.PublicName, f => r[f.PublicName])));
            return (string.Join(" AND ", entity.KeyColumns.Select((c, i) => $"{Q(c)} = @key{i}")),
                entity.KeyColumns.Select((c, i) => ($"@key{i}", values[c])).ToArray());
        }

        private static bool Travels(SyncEntity entity, string id) =>
            entity.PublicName != SettingsEntity || SyncCatalog.TravellingSettingKeys.Contains(id);

        /// <summary>Solo i campi che il catalogo locale conosce (un pari di versione minore più nuova ne può avere
        /// altri): sono loro che si confrontano, si scrivono e vanno nella base.</summary>
        private static Dictionary<string, JsonElement> Known(SyncEntity entity, SyncRow row) =>
            entity.Fields.Where(f => f.Kind != SyncFieldKind.Photo && row.Fields.ContainsKey(f.PublicName))
                .ToDictionary(f => f.PublicName, f => row.Fields[f.PublicName], StringComparer.Ordinal);

        private static long RowMs(SyncEntity entity, SyncRow row) =>
            row.Fields.TryGetValue(Ms, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms)
                ? ms
                : throw new SyncFormatException($"Row '{entity.PublicName}.{row.Id}' has no integer '{Ms}'.");

        private static bool FieldMsDiffers(
            IReadOnlyDictionary<string, long> current,
            List<KeyValuePair<string, long>> desired) =>
            current.Count != desired.Count || desired.Any(kv => !current.TryGetValue(kv.Key, out var ms) || ms != kv.Value);

        private async Task WriteFieldMsAsync(string entityPublicName, string id, IEnumerable<KeyValuePair<string, long>> entries)
        {
            await ExecAsync("DELETE FROM sync_field_ms WHERE entity = @e AND entity_id = @id",
                ("@e", entityPublicName), ("@id", id)).ConfigureAwait(false);
            foreach (var (field, ms) in entries)
            {
                await ExecAsync(
                    "INSERT INTO sync_field_ms (entity, entity_id, field, ms) VALUES (@e, @id, @f, @ms)",
                    ("@e", entityPublicName), ("@id", id), ("@f", field), ("@ms", ms)).ConfigureAwait(false);
            }
        }

        private static string Identity(SyncEntity entity, string? alias)
        {
            var prefix = alias is null ? "" : alias + ".";
            return string.Join(" || '|' || ", entity.KeyColumns.Select(c => prefix + Q(c)));
        }

        private SqliteCommand Command(string sql, params (string Name, object? Value)[] pars)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            foreach (var (name, value) in pars) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd;
        }

        private async Task<int> ExecAsync(string sql, params (string, object?)[] pars)
        {
            await using var cmd = Command(sql, pars);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        private async Task<object?> ScalarAsync(string sql, params (string, object?)[] pars)
        {
            await using var cmd = Command(sql, pars);
            var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return value is DBNull ? null : value;
        }

        private static string Q(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        private static Dictionary<string, string[]> PrimaryKeys(IModel model)
        {
            var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var et in model.GetEntityTypes())
            {
                var table = et.GetTableName();
                var pk = et.FindPrimaryKey();
                if (table is null || pk is null) continue;
                var store = StoreObjectIdentifier.Table(table, et.GetSchema());
                map[table] = pk.Properties.Select(p => p.GetColumnName(store)!).ToArray();
            }
            return map;
        }

        private static List<Fk> ForeignKeys(IModel model)
        {
            var list = new List<Fk>();
            foreach (var et in model.GetEntityTypes())
            {
                var table = et.GetTableName();
                if (table is null) continue;
                var store = StoreObjectIdentifier.Table(table, et.GetSchema());
                foreach (var fk in et.GetForeignKeys())
                {
                    var principal = fk.PrincipalEntityType;
                    var pTable = principal.GetTableName()!;
                    var pStore = StoreObjectIdentifier.Table(pTable, principal.GetSchema());
                    if (!fk.PrincipalKey.IsPrimaryKey())
                        throw new InvalidOperationException($"FK {table} -> {pTable} does not reference the primary key.");
                    list.Add(new Fk(
                        table,
                        fk.Properties.Select(p => p.GetColumnName(store)!).ToArray(),
                        pTable,
                        fk.PrincipalKey.Properties.Select(p => p.GetColumnName(pStore)!).ToArray(),
                        fk.DeleteBehavior is DeleteBehavior.SetNull or DeleteBehavior.ClientSetNull));
                }
            }
            return list;
        }
    }
#pragma warning restore CA2100
}
