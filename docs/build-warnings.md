# Build Warnings

> Ultimo aggiornamento: 2026-07-02

Stato attuale dei warning di build per Recordum.

## Riepilogo

| Metrica | Valore |
|---|---|
| Progetti compilati | 13 |
| Warning totali | **8** |
| Errori | 0 |
| Build | ✅ **Succeeded** (TreatWarningsAsErrors=true, con eccezione NU1903 declassata) |

## Warning correnti

### NU1903 — CVE-2025-6965: SQLitePCLRaw.lib.e_sqlite3

**Codice:** `NU1903`
**Gravità:** 🔴 Alta (declassata a warning visibile, non blocca il build)
**Pacchetto:** `SQLitePCLRaw.lib.e_sqlite3` 2.1.10
**CVE:** [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)
**Tipo:** Vulnerabilità NuGet

#### Descrizione

Il pacchetto `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 (transitivo via EF Core SQLite) è flaggato per una vulnerabilità di memory corruption in SQLite < 3.50.2 (CVE-2025-6965). Il problema affetta **tutte** le versioni 2.1.x del pacchetto e **non esiste ancora un fix upstream** di SQLitePCLRaw.

#### Progetti coinvolti (4 progetti × 2 occorrenze = 8 warning)

| Progetto | Occorrenze |
|---|---|
| `src/Recordum.Data` | 2 (soluzione + progetto) |
| `src/Recordum.WPF` | 2 (soluzione + progetto) |
| `tests/Recordum.Data.Tests` | 2 (soluzione + progetto) |
| `tests/Recordum.WPF.Tests` | 2 (soluzione + progetto) |

Questi 4 progetti hanno una dipendenza transitiva da SQLite tramite `Microsoft.EntityFrameworkCore.Sqlite` e `Microsoft.EntityFrameworkCore.Sqlite.Core`.

#### Rischio pratico per Recordum

Il rischio è **basso** nel contesto di Recordum:

- App offline-first: il DB SQLCipher risiede localmente, nessun dato utente transitato in rete.
- Nessuna query utente: tutto il SQL è generato da EF Core, non c'è input SQL diretto.
- DB cifrato: SQLCipher rende inaccessibile il file DB senza chiave.

Il declassamento a warning (non errore) è configurato in `Directory.Build.props`:

```xml
<WarningsNotAsErrors>$(WarningsNotAsErrors);NU1903</WarningsNotAsErrors>
```

#### Risoluzione

Rimuovere il declassamento da `Directory.Build.props` non appena `SQLitePCLRaw` spedirà SQLite ≥ 3.50.2, verificando che la versione aggiornata non rompa la compatibilità con SQLCipher.

**Trigger di chiusura:** `dotnet build` non emette più `NU1903` per `SQLitePCLRaw.lib.e_sqlite3` dopo aggiornamento del pacchetto.

---

## Storico

| Data | Warning | Note |
|---|---|---|
| 2026-07-02 | 8 × NU1903 | Build corrente. Documentato per la prima volta. |
