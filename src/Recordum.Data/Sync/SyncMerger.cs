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
/// madri prima, campo per campo contro <c>sync_base</c>; 3. figli rimasti senza madre (arrivati qui o spostati sotto
/// una madre cancellata) seguono la madre come farebbe la cascade; 4. le lapidi battute da una riga più recente
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

    private sealed record Fk(string DepTable, string[] DepCols, string PrincipalTable, string[] PrincipalCols, bool SetNull);

#pragma warning disable CA2100 // SQL costruito solo da identificatori del catalogo e del modello EF; i valori sono parametri
    private sealed class Run(
        IModel model, SqliteConnection conn, SqliteTransaction tx, SyncSnapshot remote, string localDeviceId, bool receivePhotos,
        IClock clock, Action<string>? afterEntity, CancellationToken ct)
    {
        // Chiavi solo locali nella base (sync_base non viaggia), accanto alla riga remota: com'era la riga di qui dopo
        // l'unione con quel pari (ora e hash), quando (orologio di qui) e, se teneva valori di qui, quando era cambiata
        // per l'ultima volta prima. Servono a capire chi ha visto cosa (Ruling 16/17).
        private const string LocalMsKey = "_local_ms";
        private const string LocalHashKey = "_local_hash";
        private const string MergedAtKey = "_merged_at";
        private const string PreKey = "_pre";

        private const string Ms = "updated_at_ms";
        private const string DeviceName = "last_edited_device_name";
        private const string SettingsEntity = "app_setting";
        private const string NoteEntity = "note";
        private const string HistoryEntity = "note_history";

        private readonly SyncRowCodec _codec = new(model);
        private readonly string _peer = remote.Manifest.DeviceId;
        private readonly Dictionary<string, string[]> _pk = PrimaryKeys(model);
        private readonly List<Fk> _fks = ForeignKeys(model);
        private readonly HashSet<string> _touched = new(StringComparer.Ordinal);
        private int _inserted, _updated, _deleted, _textConflicts, _rescued;

        // merged_from del pari per noi: il written_at_ms (orologio di qui) dell'ultimo nostro file che ha unito.
        private readonly long? _seenUntil = remote.Manifest.MergedFrom.TryGetValue(localDeviceId, out var seen) ? seen : null;

        public async Task<SyncMergeReport> ExecuteAsync()
        {
            await ExecAsync("PRAGMA defer_foreign_keys = ON").ConfigureAwait(false);

            var index = SyncCatalog.Entities.Select((e, i) => (e, i)).ToDictionary(x => x.e.PublicName, x => x.i, StringComparer.Ordinal);
            var remoteById = remote.Rows.ToDictionary(
                kv => kv.Key, kv => kv.Value.ToDictionary(r => r.Id, StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var t in remote.Tombstones.Where(t => index.ContainsKey(t.Entity)).OrderByDescending(t => index[t.Entity]))
                await ApplyTombstoneAsync(SyncCatalog.Entities[index[t.Entity]], t, remoteById[t.Entity]).ConfigureAwait(false);

            foreach (var entity in SyncCatalog.Entities)
            {
                await MergeRowsAsync(entity).ConfigureAwait(false);
                afterEntity?.Invoke(entity.PublicName);
            }

            await SweepOrphansAsync().ConfigureAwait(false);
            foreach (var entity in SyncCatalog.Entities)
            {
                await ExecAsync(
                    $"DELETE FROM sync_tombstone WHERE entity = @e AND EXISTS (SELECT 1 FROM {Q(entity.Table)} AS r " +
                    $"WHERE {Identity(entity, "r")} = sync_tombstone.entity_id AND r.updated_at_ms > sync_tombstone.deleted_at_ms)",
                    ("@e", entity.PublicName)).ConfigureAwait(false);
            }

            return new SyncMergeReport(_inserted, _updated, _deleted, _textConflicts, Repaired: 0, _rescued);
        }

        // Spec §8.3 "cancellazione contro modifica": vince l'azione più recente; a parità la cancellazione.
        // La lapide si copia sempre (si tiene la più recente): se qui la riga vince, il passo 4 la toglie.
        private async Task ApplyTombstoneAsync(SyncEntity entity, SyncTombstoneRow t, Dictionary<string, SyncRow> remoteRows)
        {
            if (!Travels(entity, t.EntityId)) return;
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

            await ExecAsync(
                "INSERT INTO sync_tombstone (entity, entity_id, deleted_at_ms, device_id, alias_of) VALUES (@e, @id, @at, @dev, @alias) " +
                "ON CONFLICT (entity, entity_id) DO UPDATE SET deleted_at_ms = excluded.deleted_at_ms, " +
                "device_id = excluded.device_id, alias_of = excluded.alias_of WHERE excluded.deleted_at_ms > sync_tombstone.deleted_at_ms",
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
                var rms = RowMs(entity, row);
                if (tombs.TryGetValue(row.Id, out var deletedAt) && deletedAt >= rms)
                {
                    await SalvageTextAsync(entity, row.Id, r, bases.GetValueOrDefault(row.Id), deletedAt, deletedHere: true).ConfigureAwait(false);
                    continue;
                }

                var newBase = new Dictionary<string, JsonElement>(r, StringComparer.Ordinal);
                var after = r;
                var produced = true; // questa unione ha dato alla riga la sua versione attuale
                Dictionary<string, JsonElement>? oldBase = null;
                if (!local.TryGetValue(row.Id, out var l))
                {
                    await InsertAsync(entity, row.Id, r).ConfigureAwait(false);
                    await MergePhotosAsync(entity, row.Id, r, inserted: true, remoteWins: true, rms, null, newBase, dominant: null).ConfigureAwait(false);
                    _inserted++;
                    _touched.Add(entity.Table);
                }
                else
                {
                    if (entity.IsHistory) continue; // storici: insiemi, una riga che c'è già non cambia
                    var b = oldBase = bases.GetValueOrDefault(row.Id);
                    // Un file vecchio dello stesso pari (la base ha già visto una versione più recente) non fa tornare
                    // indietro nulla.
                    if (b is not null && b.TryGetValue(Ms, out var bms) && bms.GetInt64() > rms) continue;

                    var lms = l.Fields[Ms].GetInt64();
                    var remoteWins = rms > lms || (rms == lms && CompareCanonical(r, l.Fields) < 0);
                    var dominant = b is not null && b[Ms].GetInt64() < rms
                        ? await DominantAsync(entity, row.Id, r, lms, b).ConfigureAwait(false)
                        : null;
                    var photo = await MergePhotosAsync(entity, row.Id, r, inserted: false, remoteWins, rms, b, newBase, dominant).ConfigureAwait(false);
                    var (changes, keptLocal) = MergeFields(entity, l.Fields, r, b, remoteWins, lms, rms, photo, dominant);
                    // La riga unita tiene qualcosa di qui: quando era cambiata per l'ultima volta prima di questa unione.
                    if (keptLocal)
                        newBase[PreKey] = JsonSerializer.SerializeToElement(await LastChangeAsync(entity, row.Id, lms).ConfigureAwait(false));
                    if (changes.Count > 0) await UpdateAsync(entity, r, changes).ConfigureAwait(false);
                    if (changes.Count > 0 || photo.TookRemote)
                    {
                        _updated++;
                        _touched.Add(entity.Table);
                        // Le basi degli altri pari non descrivono più questa versione della riga.
                        await ExecAsync(
                            $"UPDATE sync_base SET row_json = json_remove(row_json, '$.{LocalMsKey}', '$.{PreKey}') " +
                            "WHERE entity = @e AND entity_id = @id AND peer_device_id <> @p",
                            ("@e", entity.PublicName), ("@id", row.Id), ("@p", _peer)).ConfigureAwait(false);
                    }

                    var merged = new Dictionary<string, JsonElement>(l.Fields, StringComparer.Ordinal);
                    foreach (var (k, v) in changes) merged[k] = v;
                    after = merged;
                    produced = changes.Count > 0 || photo.TookRemote;
                }

                if (!entity.IsHistory)
                {
                    newBase[LocalMsKey] = after[Ms];
                    newBase[LocalHashKey] = JsonSerializer.SerializeToElement(RowHash(after, r.Keys));
                    // L'ora d'unione solo se questa unione ha cambiato la riga; un'unione che la lascia com'è porta avanti
                    // quella di prima (se descrive ancora questa versione), altrimenti niente: l'ora di cambio di una
                    // modifica fatta qui resta la sua (LastChangeAsync).
                    if (produced)
                        newBase[MergedAtKey] = JsonSerializer.SerializeToElement(clock.UtcNowMs);
                    else if (oldBase is not null && oldBase.TryGetValue(LocalMsKey, out var oldMs) && oldMs.GetInt64() == after[Ms].GetInt64()
                        && oldBase.TryGetValue(MergedAtKey, out var oldAt))
                        newBase[MergedAtKey] = oldAt;
                }

                if (!entity.IsHistory)
                {
                    await ExecAsync(
                        "INSERT OR REPLACE INTO sync_base (peer_device_id, entity, entity_id, row_json) VALUES (@p, @e, @id, @json)",
                        ("@p", _peer), ("@e", entity.PublicName), ("@id", row.Id), ("@json", JsonSerializer.Serialize(newBase))).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Campo per campo contro la base: cambiato da una parte sola → quella; da tutte e due (o senza base) →
        /// la riga vincente, e per i campi <see cref="SyncFieldKind.Text"/> la regola B tiene tutte e due le versioni.
        /// <c>last_edited_device_name</c> segue il testo: dice chi ha scritto la versione in cima.
        /// Torna i campi da scrivere (pubblici), <c>updated_at_ms</c> compreso se cambia.</summary>
        private (Dictionary<string, JsonElement> Changes, bool KeptLocal) MergeFields(
            SyncEntity entity, IReadOnlyDictionary<string, JsonElement> l, Dictionary<string, JsonElement> r,
            Dictionary<string, JsonElement>? b, bool remoteWins, long lms, long rms, (bool TookRemote, bool KeptLocal) photo,
            bool? dominant)
        {
            var changes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var keptLocal = photo.KeptLocal;
            bool? textFromRemote = null; // nel catalogo il campo Text viene prima di last_edited_device_name
            foreach (var field in entity.Fields)
            {
                var name = field.PublicName;
                if (field.Kind == SyncFieldKind.Photo || name == Ms || !r.TryGetValue(name, out var rv)) continue;
                var lv = l[name];
                if (JsonElement.DeepEquals(lv, rv)) continue;

                bool useRemote;
                JsonElement bv = default;
                var hasBase = b is not null && b.TryGetValue(name, out bv);
                var localChanged = !hasBase || !JsonElement.DeepEquals(lv, bv);
                var remoteChanged = !hasBase || !JsonElement.DeepEquals(rv, bv);
                if (dominant is bool remoteDominates)
                {
                    // Una parte ha già visto tutto quello che l'altra ha (DominantAsync): la sua riga decide ogni campo.
                    useRemote = remoteDominates;
                }
                else if (field.Kind == SyncFieldKind.Text && localChanged && remoteChanged)
                {
                    var (fromRemote, merged) = MergeText(lv, rv, l, r, remoteWins, lms, rms);
                    textFromRemote = fromRemote;
                    if (merged is null)
                    {
                        useRemote = fromRemote;
                    }
                    else
                    {
                        // Un testo nuovo, diverso dalla remota: conta come un valore di qui (ora massimo + 1).
                        changes[name] = JsonSerializer.SerializeToElement(merged);
                        keptLocal = true;
                        continue;
                    }
                }
                else if (name == DeviceName && textFromRemote is bool fromRemote)
                {
                    useRemote = fromRemote;
                }
                else if (hasBase)
                {
                    useRemote = localChanged == remoteChanged ? remoteWins : remoteChanged;
                    if (field.Kind == SyncFieldKind.Text) textFromRemote = useRemote;
                }
                else
                {
                    // ponytail: senza base (prima unione con questo pari) vince la riga intera più recente: due modifiche
                    // a campi diversi della stessa riga fatte prima della prima unione ne perdono una (spec §8.4). Rimedio
                    // se l'uso lo mostra: un'ora per campo in una tabella locale, scritta nel formato (versione minore).
                    useRemote = remoteWins;
                }

                if (useRemote) changes[name] = rv;
                else keptLocal = true;
            }

            // Uguale alla remota → il massimo delle due ore. Diversa dalla remota (ha tenuto qualcosa di qui, o un testo
            // unito con la regola B): deve battere tutte e due le versioni anche dove manca la base, quindi resta l'ora
            // locale solo se è già la più recente, altrimenti massimo + 1. Le foto contano come i campi.
            var tookRemote = changes.Count > 0 || photo.TookRemote;
            var ms = !keptLocal ? Math.Max(lms, rms) : !tookRemote && lms > rms ? lms : Math.Max(lms, rms) + 1;
            if (ms != lms) changes[Ms] = JsonSerializer.SerializeToElement(ms);
            return (changes, keptLocal);
        }

        /// <summary>Ruling 16: la riga remota è cambiata dalla base, ma un campo uguale alla base può voler dire "non
        /// toccato" o "visto il nostro e rimesso com'era" (ABA). Decide chi ha già visto tutto quello che ha l'altro:
        /// <list type="bullet">
        /// <item><c>false</c>: la riga remota è uno stato che avevamo noi dopo l'ultima unione con questo pari (hash).</item>
        /// <item><c>true</c>: il pari ha visto la nostra versione (<see cref="RemoteSawAsync"/>); oppure la nostra riga è
        /// ancora quella uscita dall'ultima unione con lui, fatta di cose sue e della nostra versione di prima, che a quel
        /// punto aveva già visto, e la sua riga è più recente di quell'unione (se no è la sua unione parallela alla
        /// nostra, con le stesse informazioni, e decidono le regole dei campi).</item>
        /// <item><c>null</c>: nessuna delle due; vale la regola dei campi contro la base.</item>
        /// </list></summary>
        private async Task<bool?> DominantAsync(
            SyncEntity entity, string id, Dictionary<string, JsonElement> r, long lms, Dictionary<string, JsonElement> b)
        {
            if (b.TryGetValue(LocalHashKey, out var hash) && hash.GetString() == RowHash(r, r.Keys)) return false;
            if (_seenUntil is long seen && b.TryGetValue(LocalMsKey, out var bl) && bl.GetInt64() == lms
                && b.TryGetValue(PreKey, out var pre) && pre.GetInt64() <= seen && r[Ms].GetInt64() > lms)
                return true;
            return await RemoteSawAsync(entity, id, lms).ConfigureAwait(false) ? true : null;
        }

        /// <summary>Vero se il pari ha unito un nostro file che conteneva già la nostra versione della riga: non è
        /// cambiata dopo il <c>written_at_ms</c> che il pari dichiara in <c>merged_from</c>. Senza <c>merged_from</c> per
        /// noi: falso.</summary>
        private async Task<bool> RemoteSawAsync(SyncEntity entity, string id, long lms) =>
            _seenUntil is long seen && await LastChangeAsync(entity, id, lms).ConfigureAwait(false) <= seen;

        /// <summary>Quando (orologio di qui) la riga ha preso la versione <paramref name="lms"/>: l'ora dell'unione che
        /// l'ha prodotta (una base la porta solo se quell'unione ha cambiato la riga, e la porta avanti finché la riga
        /// non cambia); altrimenti è una modifica fatta qui, e la sua ora è <paramref name="lms"/> più
        /// <see cref="SeenMarginMs"/>.</summary>
        // ponytail: una riga presa da un pari la cui base poi sparisce torna a lms, l'ora di un altro orologio. Succede
        // solo dopo un ripristino, che rigenera device_id: allora merged_from per noi manca e questa ora non si usa.
        private async Task<long> LastChangeAsync(SyncEntity entity, string id, long lms) =>
            await ScalarAsync(
                $"SELECT MIN(json_extract(row_json, '$.{MergedAtKey}')) FROM sync_base " +
                $"WHERE entity = @e AND entity_id = @id AND json_extract(row_json, '$.{LocalMsKey}') = @ms",
                ("@e", entity.PublicName), ("@id", id), ("@ms", lms)).ConfigureAwait(false) is long at ? at : lms + SeenMarginMs;

        private static string RowHash(Dictionary<string, JsonElement> row, IEnumerable<string> fields) =>
            Convert.ToHexStringLower(SHA256.HashData(SyncRowCodec.Canonical(
                new SyncRow("", fields.Where(row.ContainsKey).ToDictionary(k => k, k => row[k], StringComparer.Ordinal)))));


        /// <summary>Regola B per un testo cambiato da tutte e due le parti (o senza base): il vincente con in coda il
        /// perdente. Se il risultato è già una delle due versioni (una contiene l'altra, anche come testo in cima a una
        /// voce già unita) torna da che parte stare e nessun testo nuovo.</summary>
        private (bool FromRemote, string? Merged) MergeText(
            JsonElement lv, JsonElement rv, IReadOnlyDictionary<string, JsonElement> l, Dictionary<string, JsonElement> r,
            bool remoteWins, long lms, long rms)
        {
            var (winner, loser) = remoteWins ? (Str(rv), Str(lv)) : (Str(lv), Str(rv));
            var loserDevice = remoteWins ? l.GetValueOrDefault(DeviceName) : r.GetValueOrDefault(DeviceName);
            var merged = TextConflictBlock.Merge(winner, loser, NullableStr(loserDevice), remoteWins ? lms : rms, clock.LocalTimeZone);
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

        /// <summary>Le foto: viaggiano a parte, una per volta. Nella base si salva lo SHA-256 (esadecimale minuscolo)
        /// di ogni foto presente nel file. Una foto che il file non porta (o un originale con <c>receivePhotos</c>
        /// spento) non è un cambiamento; una foto che qui manca si riempie da quella remota.</summary>
        private async Task<(bool TookRemote, bool KeptLocal)> MergePhotosAsync(
            SyncEntity entity, string id, Dictionary<string, JsonElement> r, bool inserted, bool remoteWins, long rms,
            Dictionary<string, JsonElement>? b, Dictionary<string, JsonElement> newBase, bool? dominant)
        {
            var wrote = false;
            var keptLocal = false;
            var unchangedSinceBase = b is not null && b.TryGetValue(Ms, out var bms) && bms.GetInt64() == rms;
            foreach (var field in entity.Fields.Where(f => f.Kind == SyncFieldKind.Photo))
            {
                var thumbnail = field.PublicName == "thumbnail"; // l'altra colonna Photo (content) è l'originale
                if (!thumbnail && !receivePhotos) continue;
                // La riga remota è quella della base (stessa ora) e qui la foto c'è: niente da leggere né da hashare.
                if (!inserted && unchangedSinceBase && await LocalPhotoPresentAsync(entity, field, r).ConfigureAwait(false))
                {
                    if (b!.TryGetValue(field.PublicName, out var oldHash)) newBase[field.PublicName] = oldHash;
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
                    if (localHash == remoteHash) continue;
                    var useRemote = localHash is null
                        || (dominant ?? (b is not null && b.TryGetValue(field.PublicName, out var bh)
                            ? localHash == bh.GetString() || (remoteHash != bh.GetString() && remoteWins)
                            : remoteWins));
                    if (!useRemote)
                    {
                        keptLocal = true;
                        continue;
                    }
                }

                var (where, keys) = KeyWhere(entity, r);
                await ExecAsync($"UPDATE {Q(entity.Table)} SET {Q(field.Column)} = @blob WHERE {where}", [.. keys, ("@blob", bytes)]).ConfigureAwait(false);
                wrote = true;
            }

            return (wrote, keptLocal);
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
            await using var cmd = Command("SELECT entity_id, deleted_at_ms FROM sync_tombstone WHERE entity = @e", ("@e", entity.PublicName));
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var map = new Dictionary<string, long>(StringComparer.Ordinal);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) map[reader.GetString(0)] = reader.GetInt64(1);
            return map;
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

        /// <summary>Spareggio (formato §5): forma canonica minore, sui soli campi che le due righe hanno entrambe, così
        /// due dispositivi di versioni minori diverse decidono allo stesso modo.</summary>
        private static int CompareCanonical(Dictionary<string, JsonElement> r, IReadOnlyDictionary<string, JsonElement> l)
        {
            var common = r.Keys.Where(l.ContainsKey).ToList();
            var rc = SyncRowCodec.Canonical(new SyncRow("", common.ToDictionary(k => k, k => r[k])));
            var lc = SyncRowCodec.Canonical(new SyncRow("", common.ToDictionary(k => k, k => l[k])));
            return rc.AsSpan().SequenceCompareTo(lc);
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
