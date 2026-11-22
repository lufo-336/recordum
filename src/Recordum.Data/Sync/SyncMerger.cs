using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

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
/// </summary>
public static class SyncMerger
{
    public static Task<SyncMergeReport> MergeAsync(
        RecordumDbContext db, SyncSnapshot remote, string localDeviceId, bool receivePhotos, CancellationToken ct) =>
        MergeAsync(db, remote, localDeviceId, receivePhotos, afterEntity: null, ct);

    /// <param name="afterEntity">Solo per i test: chiamato dopo le righe di ogni entità (un'eccezione lì prova il
    /// rollback).</param>
    internal static async Task<SyncMergeReport> MergeAsync(
        RecordumDbContext db, SyncSnapshot remote, string localDeviceId, bool receivePhotos,
        Action<string>? afterEntity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(remote);
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
                var run = new Run(db.Model, conn, (SqliteTransaction)efTx.GetDbTransaction(), remote, receivePhotos, afterEntity, ct);
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
        IModel model, SqliteConnection conn, SqliteTransaction tx, SyncSnapshot remote, bool receivePhotos,
        Action<string>? afterEntity, CancellationToken ct)
    {
        private const string Ms = "updated_at_ms";
        private const string SettingsEntity = "app_setting";

        private readonly SyncRowCodec _codec = new(model);
        private readonly string _peer = remote.Manifest.DeviceId;
        private readonly Dictionary<string, string[]> _pk = PrimaryKeys(model);
        private readonly List<Fk> _fks = ForeignKeys(model);
        private readonly HashSet<string> _touched = new(StringComparer.Ordinal);
        private int _inserted, _updated, _deleted, _textConflicts;

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

            return new SyncMergeReport(_inserted, _updated, _deleted, _textConflicts, Repaired: 0);
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
                if (tombs.TryGetValue(row.Id, out var deletedAt) && deletedAt >= rms) continue;

                var newBase = new Dictionary<string, JsonElement>(r, StringComparer.Ordinal);
                if (!local.TryGetValue(row.Id, out var l))
                {
                    await InsertAsync(entity, row.Id, r).ConfigureAwait(false);
                    await MergePhotosAsync(entity, row.Id, r, inserted: true, remoteWins: true, null, newBase).ConfigureAwait(false);
                    _inserted++;
                    _touched.Add(entity.Table);
                }
                else
                {
                    if (entity.IsHistory) continue; // storici: insiemi, una riga che c'è già non cambia
                    var b = bases.GetValueOrDefault(row.Id);
                    // Un file vecchio dello stesso pari (la base ha già visto una versione più recente) non fa tornare
                    // indietro nulla.
                    if (b is not null && b.TryGetValue(Ms, out var bms) && bms.GetInt64() > rms) continue;

                    var lms = l.Fields[Ms].GetInt64();
                    var remoteWins = rms > lms || (rms == lms && CompareCanonical(r, l.Fields) < 0);
                    var changes = MergeFields(entity, l.Fields, r, b, remoteWins, lms, rms);
                    var photo = await MergePhotosAsync(entity, row.Id, r, inserted: false, remoteWins, b, newBase).ConfigureAwait(false);
                    if (changes.Count > 0) await UpdateAsync(entity, r, changes).ConfigureAwait(false);
                    if (changes.Count > 0 || photo)
                    {
                        _updated++;
                        _touched.Add(entity.Table);
                    }
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
        /// la riga vincente. Torna i campi da scrivere (pubblici), <c>updated_at_ms</c> compreso se cambia.</summary>
        private Dictionary<string, JsonElement> MergeFields(
            SyncEntity entity, IReadOnlyDictionary<string, JsonElement> l, Dictionary<string, JsonElement> r,
            Dictionary<string, JsonElement>? b, bool remoteWins, long lms, long rms)
        {
            var changes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var keptLocal = false;
            foreach (var field in entity.Fields)
            {
                var name = field.PublicName;
                if (field.Kind == SyncFieldKind.Photo || name == Ms || !r.TryGetValue(name, out var rv)) continue;
                var lv = l[name];
                if (JsonElement.DeepEquals(lv, rv)) continue;

                bool useRemote;
                if (b is not null && b.TryGetValue(name, out var bv))
                {
                    var localChanged = !JsonElement.DeepEquals(lv, bv);
                    var remoteChanged = !JsonElement.DeepEquals(rv, bv);
                    useRemote = localChanged == remoteChanged ? remoteWins : remoteChanged;
                    if (localChanged && remoteChanged && field.Kind == SyncFieldKind.Text) _textConflicts++;
                }
                else
                {
                    // ponytail: senza base (prima unione con questo pari) vince la riga intera più recente: due modifiche
                    // a campi diversi della stessa riga fatte prima della prima unione ne perdono una (spec §8.4). Rimedio
                    // se l'uso lo mostra: un'ora per campo in una tabella locale, scritta nel formato (versione minore).
                    useRemote = remoteWins;
                    if (field.Kind == SyncFieldKind.Text) _textConflicts++;
                }

                // Task 7: i Text in conflitto qui fanno "vince la più recente"; la regola B li terrà tutti e due.
                if (useRemote) changes[name] = rv;
                else keptLocal = true;
            }

            // Uguale alla remota → il massimo delle due ore. Diversa dalla remota (ha tenuto qualcosa di qui): deve
            // battere tutte e due le versioni anche dove manca la base, quindi resta l'ora locale solo se è già la più
            // recente, altrimenti massimo + 1.
            var ms = !keptLocal ? Math.Max(lms, rms) : changes.Count == 0 && lms > rms ? lms : Math.Max(lms, rms) + 1;
            if (ms != lms) changes[Ms] = JsonSerializer.SerializeToElement(ms);
            return changes;
        }

        /// <summary>Le foto: viaggiano a parte, una per volta. Nella base si salva lo SHA-256 (esadecimale minuscolo)
        /// di ogni foto presente nel file. Una foto che il file non porta (o un originale con <c>receivePhotos</c>
        /// spento) non è un cambiamento; una foto che qui manca si riempie da quella remota.</summary>
        private async Task<bool> MergePhotosAsync(
            SyncEntity entity, string id, Dictionary<string, JsonElement> r, bool inserted, bool remoteWins,
            Dictionary<string, JsonElement>? b, Dictionary<string, JsonElement> newBase)
        {
            var wrote = false;
            foreach (var field in entity.Fields.Where(f => f.Kind == SyncFieldKind.Photo))
            {
                var thumbnail = field.PublicName == "thumbnail"; // l'altra colonna Photo (content) è l'originale
                if (!thumbnail && !receivePhotos) continue;
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

                var write = inserted;
                if (!inserted)
                {
                    var localHash = await LocalPhotoHashAsync(entity, field, r).ConfigureAwait(false);
                    write = localHash is null
                        || (localHash != remoteHash && (b is not null && b.TryGetValue(field.PublicName, out var bh)
                            ? localHash == bh.GetString() || (remoteHash != bh.GetString() && remoteWins)
                            : remoteWins));
                }

                if (!write) continue;
                var (where, keys) = KeyWhere(entity, r);
                await ExecAsync($"UPDATE {Q(entity.Table)} SET {Q(field.Column)} = @blob WHERE {where}", [.. keys, ("@blob", bytes)]).ConfigureAwait(false);
                wrote = true;
            }

            return wrote;
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

        private async Task InsertAsync(SyncEntity entity, string id, Dictionary<string, JsonElement> r)
        {
            var values = _codec.ToDb(entity, new SyncRow(id, r)).ToList();
            var cols = string.Join(", ", values.Select(v => Q(v.Key)));
            var pars = values.Select((v, i) => ($"@v{i}", v.Value)).ToArray();
            await ExecAsync($"INSERT INTO {Q(entity.Table)} ({cols}) VALUES ({string.Join(", ", pars.Select(p => p.Item1))})", pars).ConfigureAwait(false);
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
