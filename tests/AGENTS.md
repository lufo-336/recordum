# Recordum/tests

> Vedi [`../AGENTS.md`](../AGENTS.md) per le regole globali del progetto.

## Ambito

Questa directory contiene i progetti di test di Recordum, con una struttura 1:1 rispetto ai progetti `src/`.

## Progetti

| Progetto di test | Progetto sorgente |
|---|---|
| `Recordum.Core.Tests` | `Recordum.Core` |
| `Recordum.Data.Tests` | `Recordum.Data` |
| `Recordum.Plugins.Tests` | `Recordum.Plugins` |
| `Recordum.Plugins.ChartEngine.Tests` | `Recordum.Plugins.ChartEngine` |
| `Recordum.Plugins.ExportEngine.Tests` | `Recordum.Plugins.ExportEngine` |
| `Recordum.WPF.Tests` | `Recordum.WPF` |

**Nota:** `Recordum.Plugins.Contracts` non ha un progetto di test dedicato.

## Regole operative

### Convenzioni di test

- I behavioral changes devono essere accompagnati da test.
- Il data layer e lo streak engine sono coperti su un database in-memory reale, non su mock.
- Per i test WPF che richiedono `IClock`, usare `FakeClock`.
- I test che coinvolgono DPAPI/SQLCipher girano solo su Windows.
- Suite attuale: Data ~370 test, WPF ~683 test.

### Cosa testare

- **Data layer**: CRUD, soft delete cascade/restore, HabitConfigHistory snapshot.
- **StreakEngine**: tutte le combinazioni schedule_type × streak_mode × streak_recovery × pause + cambi configurazione + reset_time cascade.
- **Export**: test su dataset grandi.
- **WPF ViewModels**: logica di navigazione, validazione, comandi (con dependency injection).
- **Plugins**: attivazione, registrazione extension point, routing eventi.

### Cosa NON testare (in unit test)

- Layout UI e rendering (verifica visiva manuale sull'app WPF).
- Interazioni WebView2 (editor Markdown).
- Encryption/decryption DPAPI (dipende dall'ambiente Windows).

## Comandi

```bash
# Tutti i test
dotnet test

# Progetto specifico
dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj

# Con output dettagliato
dotnet test -v n

# Singolo test (filtro per nome)
dotnet test --filter "FullyQualifiedName~NomeDelTest"
```

## Verifica tipica

1. `dotnet test` — tutti i test devono passare
2. Verificare che i test WPF non DPAPI-dipendenti passino anche su Mac (con `-p:EnableWindowsTargeting=true`)
3. I test che falliscono solo su Mac (DPAPI/SQLCipher Windows-only) sono accettabili

## Indice AGENTS.md

- Vedi `../AGENTS.md`
