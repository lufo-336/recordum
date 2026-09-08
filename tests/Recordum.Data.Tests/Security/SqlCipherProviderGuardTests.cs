using Microsoft.Data.Sqlite;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>
/// Il guard che nomina la causa. `SQLitePCLRaw.bundle_e_sqlcipher` e `SQLitePCLRaw.bundle_e_sqlite3`
/// spediscono entrambi un assembly che si chiama `SQLitePCLRaw.batteries_v2.dll`: nella cartella di
/// output ne sopravvive uno solo, e vince la versione più alta. Se vince quello in chiaro,
/// <c>Batteries_V2.Init()</c> registra il provider `e_sqlite3`, <c>PRAGMA key</c> diventa un no-op
/// <b>silenzioso</b> e il database viene scritto NON cifrato — senza un errore, senza un warning.
/// <para>È successo davvero: <c>Microsoft.EntityFrameworkCore.Sqlite</c> (il pacchetto completo, non
/// <c>.Sqlite.Core</c>) tirava dentro `bundle_e_sqlite3`, e il giorno in cui ne è uscita una versione
/// più alta del cipher bundle la cifratura si è spenta da sola. Non l'aveva rotta un commit: era
/// cambiato il mondo esterno sotto un wildcard di versione.</para>
/// <para>Gli altri test di cifratura lo rilevavano già — andavano rossi — ma il loro rosso si legge
/// come "problema d'ambiente" e si archivia. Questo no: dice quale provider è attivo e perché conta.
/// Se fallisce, controllare che nessun pacchetto abbia reintrodotto `bundle_e_sqlite3`
/// (<c>grep bundle_e_sqlite3 src/Recordum.Data/obj/project.assets.json</c>).</para>
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

        // PRAGMA cipher_version e il controllo canonico: su SQLCipher torna la versione, su
        // e_sqlite3 e una pragma sconosciuta e SQLite la ignora in silenzio, senza righe.
        // (Prima qui c era "SELECT sqlcipher_export IS NOT NULL": sbagliato — un identificatore
        // nudo e una COLONNA, non una chiamata di funzione, quindi l errore diceva "no such column"
        // e il guard passava anche col provider in chiaro. Misurato, non dedotto.)
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA cipher_version;";
        var cipherVersion = cmd.ExecuteScalar() as string;

        Assert.False(
            string.IsNullOrWhiteSpace(cipherVersion),
            "Il provider SQLite attivo non e SQLCipher (PRAGMA cipher_version non torna nulla): il "
            + "database verrebbe scritto in chiaro. Controllare che nessun pacchetto abbia "
            + "reintrodotto SQLitePCLRaw.bundle_e_sqlite3 — usare "
            + "Microsoft.EntityFrameworkCore.Sqlite.Core, non .Sqlite.");
    }
}
