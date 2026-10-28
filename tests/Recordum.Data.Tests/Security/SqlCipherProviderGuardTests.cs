using Microsoft.Data.Sqlite;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>
/// Il guard che nomina la causa. Due bundle SQLitePCLRaw nel grafo spediscono entrambi un assembly che si chiama
/// `SQLitePCLRaw.batteries_v2.dll`: nella cartella di output ne sopravvive uno solo, e vince la versione più alta. Se
/// vince quello in chiaro (`SQLitePCLRaw.bundle_e_sqlite3`), <c>Batteries_V2.Init()</c> registra il provider `e_sqlite3`,
/// <c>PRAGMA key</c> diventa un no-op <b>silenzioso</b> e il database viene scritto NON cifrato — senza un errore, senza
/// un warning.
/// <para>È successo davvero (SEC-1): <c>Microsoft.EntityFrameworkCore.Sqlite</c> (il pacchetto completo, non
/// <c>.Sqlite.Core</c>) tirava dentro `bundle_e_sqlite3`, e il giorno in cui ne è uscita una versione più alta del
/// bundle cifrato di allora (`bundle_e_sqlcipher`) la cifratura si è spenta da sola. Non l'aveva rotta un commit: era
/// cambiato il mondo esterno sotto un wildcard di versione.</para>
/// <para>Dall'onda D9 il provider è SQLite3 Multiple Ciphers (`SQLite3MC.PCLRaw.bundle`), che non ha
/// <c>PRAGMA cipher_version</c>: la domanda è se la funzione <c>sqlite3mc_version</c> esiste. Col provider in chiaro le
/// istruzioni nuove (<c>PRAGMA cipher…; PRAGMA legacy…; PRAGMA key…</c>) non danno errore e il file nasce in chiaro
/// (sonda D9 §3): il guard serve ancora. Il secondo assert tiene il motore mantenuto: il bundle di prima era fermo a
/// SQLite 3.39.2.</para>
/// <para>Gli altri test di cifratura lo rilevano già — vanno rossi — ma il loro rosso si legge come "problema
/// d'ambiente" e si archivia. Questo no: dice quale provider è attivo e perché conta. Se fallisce, controllare che nessun
/// pacchetto abbia reintrodotto `bundle_e_sqlite3`
/// (<c>grep -i sqlitepclraw.bundle src/Recordum.Data/obj/project.assets.json</c>).</para>
/// </summary>
public class SqlCipherProviderGuardTests
{
    [Fact]
    public void The_active_sqlite_provider_is_sqlcipher_not_plain_sqlite()
    {
        // Forza l'inizializzazione delle batteries: e' Microsoft.Data.Sqlite a chiamarle, quindi
        // basta aprire una connessione perche' il provider risulti registrato.
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        // Una funzione cercata in pragma_function_list, non chiamata: su e_sqlite3 non c'e' e il conteggio e' 0.
        // (Prima di SEC-1 qui c era "SELECT sqlcipher_export IS NOT NULL": sbagliato — un identificatore nudo e una
        // COLONNA, non una chiamata di funzione, quindi l errore diceva "no such column" e il guard passava anche col
        // provider in chiaro. Misurato, non dedotto.)
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM pragma_function_list WHERE name = 'sqlite3mc_version'";
        var mc = (long)cmd.ExecuteScalar()!;

        Assert.True(
            mc == 1,
            "Il provider SQLite attivo non e SQLite3 Multiple Ciphers (la funzione sqlite3mc_version non c'e'): il "
            + "database verrebbe scritto in chiaro. Controllare che nessun pacchetto abbia reintrodotto "
            + "SQLitePCLRaw.bundle_e_sqlite3 accanto a SQLite3MC.PCLRaw.bundle — usare "
            + "Microsoft.EntityFrameworkCore.Sqlite.Core, non .Sqlite.");

        cmd.CommandText = "SELECT sqlite_version()";
        var version = Version.Parse((string)cmd.ExecuteScalar()!);
        Assert.True(
            version >= new Version(3, 50),
            $"Il provider porta SQLite {version}: un motore fermo (il bundle di prima era a 3.39.2) non riceve le "
            + "correzioni di sicurezza. Aggiornare SQLite3MC.PCLRaw.bundle.");
    }
}
