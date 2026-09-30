# Onda 2.4 — U7 Food diary note-first — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Su un'abitudine journal con gesto "nota veloce", la colonna ✎ Diario diventa un composer breve con foto più la lista delle voci del giorno, ogni salvataggio produce Nota(`Diario`) + Completion, e il template Food diary spedito nasce già così.

**Architecture:** Nessuna tabella e nessuna colonna nuove. Il salvataggio passa da `IDiaryService`, che già crea nota + completion in un solo `SaveChanges`; le foto scelte restano percorsi in memoria finché non esiste il `noteId`, poi diventano allegati via `IAttachmentService`. Il gesto vive in `HabitUiPrefs.JournalGesture` (già esistente dall'Onda 2.0) e arriva da tre sorgenti: il payload del template alla creazione, la sezione Registrazione di Configura in seguito, il builder quando salvi una composizione come template. La colonna Diario ospita **o** il composer **o** l'editor Markdown, mai entrambi, così WebView2 continua a istanziarsi una volta sola.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, EF Core + SQLite/SQLCipher, xunit + FluentAssertions.

## Global Constraints

- Branch di lavoro: **`wave-2-4-food-diary`** (già creato, contiene la spec `18f7b2a`). L'hook `guard-main.js` blocca i commit su `main`.
- Spec di riferimento: `docs/superpowers/archive/specs/2026-08-08-wave-2-4-food-diary-design.md`. In caso di conflitto vince la spec.
- **Ogni nuova stringa utente = 3 edit**: `src/Recordum.WPF/Resources/Strings.resx` (inglese, primario), `src/Recordum.WPF/Resources/Strings.it.resx` (italiano), e la proprietà a mano in `src/Recordum.WPF/Resources/Strings.Designer.cs`. **Quest'ultimo file va editato via shell** (Bash: `python`/`sed`/heredoc): l'hook `guard-generated.js` matcha qualsiasi `*.Designer.cs` e nega Edit/Write, anche se questo designer è mantenuto a mano e non è EF. Non usare `WriteAllLines`/`WriteAllText` di PowerShell (azzerano il file o aggiungono un BOM).
- Mai `DateTime.UtcNow`/`DateTime.Now`: sempre `IClock`.
- **Mai svuotare (`Clear()`) una collection che è `ItemsSource` di un controllo bindato two-way** senza sopprimere la persistenza per tutta la durata dell'operazione e ri-affermare il valore dopo. È la lezione di `4d01f9c` e `1d96967`, due bug da perdita dati.
- Comando migrazioni (lo startup project è **`Recordum.Data`**, non `Recordum.WPF` — con WPF fallisce):
  ```bash
  dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.Data
  ```
  Al file generato va aggiunto a mano `#pragma warning disable CA1861` subito dopo `#nullable disable`, altrimenti `TreatWarningsAsErrors` fa fallire la build.
- Convenzione del payload template: **chiavi snake_case, valori enum PascalCase** (`"habit_type":"Journal"`, `"streak_recovery":"GraceDay"`). `ui_prefs_json` sul nodo usa invece **camelCase** — sono due contratti distinti, mappati esplicitamente in Task 1.
- Build/test: `dotnet build` e `dotnet test tests/Recordum.Data.Tests` (idem `Recordum.Core.Tests`, `Recordum.WPF.Tests`). Baseline verde a inizio onda: **Core 113 / Data 432 / WPF 842 / Plugins 66 / ExportEngine 37 / ChartEngine 18 = 1508**.
- Nessuna verifica visiva in quest'onda: l'autore la fa una volta sola a valle della 2.5.

---

### Task 1: `ui_prefs` nel contratto del payload template

Il payload di un template non sa esprimere le preferenze di presentazione, quindi nessun template può pre-impostare il gesto journal. Questo task apre quel canale, dal parsing fino alla scrittura di `habit_node.ui_prefs_json`.

**Files:**
- Modify: `src/Recordum.Data/Services/HabitTemplateService.cs` (ctor riga 12-18, `InstantiateAsync` riga 64-115, classi payload riga 218-272)
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (registrazione di `HabitTemplateService`)
- Test: `tests/Recordum.Data.Tests/Integration/HabitTemplateServiceTests.cs`

**Interfaces:**
- Consumes: `IHabitUiPrefsService.SetAsync(Guid, HabitUiPrefs, CancellationToken)`, `HabitUiPrefs` (record con `KeyFigure`, `Strip`, `OpenColumns`, `JournalGesture`), `JournalPrimaryGesture { Diary, QuickNote }`.
- Produces: `HabitTemplateService` con un parametro in più nel costruttore (`IHabitUiPrefsService uiPrefs`, **ultimo prima di `ILogger`**); `UiPrefsPayload` con la sola proprietà `JournalGesture` (`string?`), letta dalla chiave `ui_prefs.journal_gesture`.

- [ ] **Step 1: Scrivi il test che fallisce**

In `tests/Recordum.Data.Tests/Integration/HabitTemplateServiceTests.cs`, aggiungi:

```csharp
[Fact]
public async Task InstantiateAsync_applies_the_journal_gesture_from_the_payload()
{
    await using var fx = new DbFixture();
    var tmplId = Guid.NewGuid();
    await using (var db = await fx.Factory.CreateDbContextAsync())
    {
        db.HabitTemplates.Add(new HabitTemplate
        {
            Id = tmplId,
            Name = "Food diary",
            PayloadJson = "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"}," +
                          "\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}," +
                          "\"ui_prefs\":{\"journal_gesture\":\"QuickNote\"}}",
            IsOfficial = true,
        });
        await db.SaveChangesAsync();
    }

    var svc = CreateSut(fx);
    var newId = await svc.InstantiateAsync(tmplId, parentId: null);

    var prefs = new HabitUiPrefsService(fx.Factory);
    (await prefs.GetAsync(newId)).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
}

[Fact]
public async Task InstantiateAsync_leaves_ui_prefs_null_when_the_payload_has_none()
{
    await using var fx = new DbFixture();
    var tmplId = Guid.NewGuid();
    await using (var db = await fx.Factory.CreateDbContextAsync())
    {
        db.HabitTemplates.Add(new HabitTemplate
        {
            Id = tmplId,
            Name = "Daily journal",
            PayloadJson = "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"}," +
                          "\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}}",
            IsOfficial = true,
        });
        await db.SaveChangesAsync();
    }

    var newId = await CreateSut(fx).InstantiateAsync(tmplId, parentId: null);

    await using var db2 = await fx.Factory.CreateDbContextAsync();
    // Nessuna scrittura spuria: la colonna resta NULL, cioè "usa il default del tipo".
    (await db2.HabitNodes.FindAsync(newId))!.UiPrefsJson.Should().BeNull();
}
```

Aggiorna l'helper `CreateSut` dello stesso file passando il nuovo servizio:

```csharp
private static HabitTemplateService CreateSut(DbFixture fx) => new(
    fx.Factory,
    fx.Clock,
    new NullPluginContext(),
    new HabitNodeService(fx.Factory, fx.Clock),
    new HabitConfigService(fx.Factory, fx.Clock),
    new HabitUiPrefsService(fx.Factory),
    NullLogger<HabitTemplateService>.Instance);
```

> Se la firma di `CreateSut` nel file differisce (dipendenze già presenti), **non riscriverla da zero**: aggiungi solo `new HabitUiPrefsService(fx.Factory)` come penultimo argomento, prima del logger.

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `dotnet test tests/Recordum.Data.Tests --filter "FullyQualifiedName~HabitTemplateServiceTests"`
Expected: errore di compilazione sul costruttore (7 argomenti contro 6 attesi).

- [ ] **Step 3: Implementa**

In `src/Recordum.Data/Services/HabitTemplateService.cs`, aggiungi il parametro al costruttore primario:

```csharp
public sealed class HabitTemplateService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    IHabitNodeService nodes,
    IHabitConfigService configs,
    IHabitUiPrefsService uiPrefs,
    ILogger<HabitTemplateService> logger) : IHabitTemplateService
```

Aggiungi la classe payload accanto alle altre (dopo `ConfigPayload`):

```csharp
private sealed class UiPrefsPayload
{
    /// <summary>"Diary" | "QuickNote". Chiave snake_case, valore PascalCase, come gli altri enum
    /// del payload. Null = il nodo resta senza prefs e usa il default del tipo.</summary>
    public string? JournalGesture { get; set; }
}
```

e la proprietà su `TemplatePayload`:

```csharp
private sealed class TemplatePayload
{
    public NodePayload? Node { get; set; }
    public ConfigPayload? Config { get; set; }
    public UiPrefsPayload? UiPrefs { get; set; }
    public List<MetricPayload>? Metrics { get; set; }
    public List<SegmentPayload>? Segments { get; set; }
    public GoalPayload? Goal { get; set; }
}
```

In `InstantiateAsync`, subito dopo il blocco `if (node.NodeType == NodeType.Habit) { ... }` e **prima** di `return newId;`:

```csharp
        // Presentazione per-nodo: si scrive solo se il template la dichiara, così un template
        // che non dice niente lascia ui_prefs_json NULL = "default del tipo" (governance §6).
        if (payload.UiPrefs?.JournalGesture is { } gestureText
            && Enum.TryParse<JournalPrimaryGesture>(gestureText, ignoreCase: true, out var gesture))
        {
            var prefs = await uiPrefs.GetAsync(newId, ct);
            await uiPrefs.SetAsync(newId, prefs with { JournalGesture = gesture }, ct);
        }
```

Aggiungi gli using mancanti in cima al file se non ci sono già: `using Recordum.Core.Models;` (c'è) — `JournalPrimaryGesture` vive in `Recordum.Core.Enums`, già importato.

- [ ] **Step 4: Aggiorna la registrazione DI**

In `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`, verifica che `IHabitUiPrefsService` sia registrato **prima** o comunque insieme a `IHabitTemplateService` (l'ordine di registrazione non conta per il container, ma la dipendenza deve esistere). Se `HabitTemplateService` è registrato con `AddScoped<IHabitTemplateService, HabitTemplateService>()` non serve alcuna modifica: la DI risolve il nuovo parametro da sola. Se invece è costruito con una factory lambda che elenca gli argomenti a mano, aggiungi `sp.GetRequiredService<IHabitUiPrefsService>()` nella posizione corrispondente.

Run: `dotnet build src/Recordum.WPF`
Expected: build pulita.

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `dotnet test tests/Recordum.Data.Tests --filter "FullyQualifiedName~HabitTemplateServiceTests"`
Expected: PASS, inclusi i due test nuovi.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Data/Services/HabitTemplateService.cs src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs tests/Recordum.Data.Tests/Integration/HabitTemplateServiceTests.cs
git commit -m "feat(templates): il payload puo' dichiarare ui_prefs (journal_gesture)"
```

---

### Task 2: Seed Food diary riallineato + migration data-only v2.17

Il template spedito perde le tre metriche che su un journal non hanno superficie di inserimento e dichiara il gesto. `Daily journal` dichiara il proprio esplicitamente.

**Files:**
- Modify: `src/Recordum.Data/SeedData/HabitTemplateSeed.cs` (riga 24 `DailyJournalPayload`, riga 38-39 `FoodDiaryPayload`, riga 138 descrizione)
- Create: `src/Recordum.Data/Migrations/<timestamp>_FoodDiaryQuickNoteV217.cs`
- Modify: 8 file di test che citano `2.16` (elencati sotto)
- Test: `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs`

**Interfaces:**
- Consumes: `UiPrefsPayload` (Task 1).
- Produces: `schema_version` = `"2.17"`; il template `00000003-0000-0000-0000-000000000008` senza metriche e con `ui_prefs.journal_gesture = "QuickNote"`.

- [ ] **Step 1: Scrivi il test di guardia che fallisce**

In `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs`:

```csharp
[Fact]
public void No_official_journal_template_declares_metrics()
{
    // Un habit Journal si registra scrivendo: non ha nessun modulo metriche. Un template
    // journal che dichiara metriche promette un inserimento che il tipo non sa offrire
    // (principio 8). Gemello del guard sulle durate senza unita' introdotto con R7.
    using var ctx = _fixture.CreateContext();
    foreach (var t in ctx.HabitTemplates.Where(t => t.IsOfficial).ToList())
    {
        using var doc = System.Text.Json.JsonDocument.Parse(t.PayloadJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("node", out var node)) continue;
        if (!node.TryGetProperty("habit_type", out var ht) || ht.GetString() != "Journal") continue;
        var hasMetrics = root.TryGetProperty("metrics", out var metrics)
            && metrics.ValueKind == System.Text.Json.JsonValueKind.Array
            && metrics.GetArrayLength() > 0;
        Assert.False(hasMetrics, $"Template '{t.Name}' e' un Journal ma dichiara metriche.");
    }
}

[Fact]
public void Food_diary_template_ships_the_quick_note_gesture()
{
    using var ctx = _fixture.CreateContext();
    var t = ctx.HabitTemplates.Single(x => x.Name == "Food diary");
    using var doc = System.Text.Json.JsonDocument.Parse(t.PayloadJson);
    var gesture = doc.RootElement.GetProperty("ui_prefs").GetProperty("journal_gesture").GetString();
    Assert.Equal("QuickNote", gesture);
}
```

E aggiorna il test esistente del numero di schema, **nome del metodo incluso**:

```csharp
[Fact]
public void AppSetting_schema_version_is_2_17()
{
    // ... corpo invariato, cambia solo l'atteso:
    Assert.Equal("2.17", schemaVersion);
}
```

- [ ] **Step 2: Esegui e verifica che fallisca**

Run: `dotnet test tests/Recordum.Data.Tests --filter "FullyQualifiedName~SeedDataTests"`
Expected: FAIL — "Template 'Food diary' e' un Journal ma dichiara metriche", più il `KeyNotFoundException` su `ui_prefs` e il `2.17` atteso contro `2.16`.

- [ ] **Step 3: Riscrivi i due payload seed**

In `src/Recordum.Data/SeedData/HabitTemplateSeed.cs`, sostituisci `DailyJournalPayload`:

```csharp
    private const string DailyJournalPayload =
        "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"Diary\"},\"metrics\":[],\"segments\":[]}";
```

e `FoodDiaryPayload`:

```csharp
    private const string FoodDiaryPayload =
        "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"QuickNote\"},\"metrics\":[],\"segments\":[]}";
```

Sostituisci anche la descrizione del template (riga ~138), che parla di una meccanica da counter:

```csharp
            Description = "Log each meal in seconds: a short note and a photo, as many times a day as you eat.",
```

- [ ] **Step 4: Genera la migration**

```bash
dotnet ef migrations add FoodDiaryQuickNoteV217 --project src/Recordum.Data --startup-project src/Recordum.Data
```

Apri il file `src/Recordum.Data/Migrations/<timestamp>_FoodDiaryQuickNoteV217.cs` e aggiungi `#pragma warning disable CA1861` subito dopo `#nullable disable`. Il corpo deve essere questo (EF genera già gli `UpdateData` dai `HasData`; verifica che ci siano tutti e tre e correggili se manca il bump di `schema_version`):

```csharp
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 8, 8, 0, 0, 0, 0, DateTimeKind.Utc), "2.17" });

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000005"),
                column: "payload_json",
                value: "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"Diary\"},\"metrics\":[],\"segments\":[]}");

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000008"),
                columns: new[] { "description", "payload_json" },
                values: new object[]
                {
                    "Log each meal in seconds: a short note and a photo, as many times a day as you eat.",
                    "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"QuickNote\"},\"metrics\":[],\"segments\":[]}",
                });
        }
```

> **Verifica il GUID di `Daily journal`** leggendo `HabitTemplateSeed.All` prima di scrivere la migration: nel piano è indicato `...0005` per coerenza con l'ordine del seed, ma va confermato sul file. Un GUID sbagliato produce una `UpdateData` che non aggiorna nulla, silenziosamente.

Il `Down` rimette i payload precedenti (le tre metriche del Food diary, la vecchia descrizione, nessun `ui_prefs`) e `schema_version` a `"2.16"` con `new DateTime(2026, 8, 8, ...)`.

Non toccare `*.Designer.cs` né `RecordumDbContextModelSnapshot.cs`: lasciali come li genera EF (l'hook `guard-generated.js` li protegge).

- [ ] **Step 5: Aggiorna gli 8 riferimenti a 2.16 nei test**

Sono questi, e tre hanno il numero **nel nome del metodo**:

| File | Cosa cambiare |
|---|---|
| `tests/Recordum.Data.Tests/Backup/RestoreServicePreviewTests.cs:29` | `Be("2.17")` |
| `tests/Recordum.Data.Tests/Backup/BackupServiceTests.cs:31` | `Be("2.17")` |
| `tests/Recordum.Data.Tests/Migrations/ActiveTimerV25MigrationTests.cs:18,22,31` | commento + **nome metodo** `Schema_version_AppSetting_is_2_17_after_full_migration` + `Equal("2.17", ...)` |
| `tests/Recordum.Data.Tests/Migrations/NoteCategoryV211MigrationTests.cs:25` | `Equal("2.17", ...)` |
| `tests/Recordum.Data.Tests/Migrations/PerTypePrefsV214MigrationTests.cs:18,26` | **nome metodo** `Schema_version_is_2_17_after_full_migration` + `Equal("2.17", ...)` |
| `tests/Recordum.Data.Tests/Migrations/CounterReclassV210Tests.cs:61` | `Equal("2.17", ...)` |
| `tests/Recordum.Data.Tests/Schema/StreakBrokenAtV26Tests.cs:37,38` | commento (cita `FoodDiaryQuickNoteV217`) + `Equal("2.17", ...)` |
| `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs:111,119` | già fatto allo Step 1 |

Controlla di non averne dimenticato nessuno:

Run: `rg -n '"2\.16"|_2_16' tests/`
Expected: nessun risultato.

> Su questo repo **non** usare `grep -rn` da shell: il working dir sta su un mount Google Drive e la ricerca ricorsiva va in timeout. Usa `rg` o gli strumenti Grep/Glob dell'agente.

- [ ] **Step 6: Esegui i test Data e verifica che passino**

Run: `dotnet test tests/Recordum.Data.Tests`
Expected: PASS, suite intera.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Data/SeedData/HabitTemplateSeed.cs src/Recordum.Data/Migrations tests/Recordum.Data.Tests
git commit -m "feat(seed): Food diary e' un journal note-first (schema 2.17)"
```

---

### Task 3: Il builder salva un payload che il servizio sa rileggere

**Questo task chiude un bug preesistente scoperto scrivendo il piano, non una feature nuova.** `HabitBuilderViewModel` ha una propria classe `TemplatePayload` (riga 331) che scrive `repeatable`, `counts_occurrences`, `mode` e `metrics[].sort_order` **al livello radice**, mentre `HabitTemplateService` legge `repeatable` da `config.repeatable` e ignora del tutto gli altri tre. Oggi quindi: salvi una composizione ripetibile come template, la re-istanzi, e torna non ripetibile. Senza questa correzione, aggiungere `ui_prefs` al lato builder produrrebbe un'altra chiave che nessuno legge.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` (riga 295-339)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs`

**Interfaces:**
- Consumes: la forma del payload letta da `HabitTemplateService` (Task 1): `{ node, config: { repeatable, ... }, ui_prefs: { journal_gesture }, metrics, segments, goal }`.
- Produces: `HabitBuilderViewModel.JournalGesture` (`JournalPrimaryGesture`, default `Diary`) e `ShowJournalGesture` (`bool`, vero quando `DerivedType == HabitType.Journal`).

- [ ] **Step 1: Scrivi i test che falliscono**

In `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs` (usa lo stesso helper di costruzione del VM già presente nel file):

```csharp
[Fact]
public async Task SaveAsTemplate_nests_repeatable_under_config_so_instantiation_reads_it()
{
    var sut = CreateSut();
    sut.Name = "Sonno";
    sut.CountsOccurrences = true;
    sut.Repeatable = true;

    await sut.SaveAsTemplateCommand.ExecuteAsync(null);

    var json = _templates.Created.Single().PayloadJson;
    using var doc = JsonDocument.Parse(json);
    doc.RootElement.GetProperty("config").GetProperty("repeatable").GetBoolean().Should().BeTrue();
    doc.RootElement.TryGetProperty("repeatable", out _).Should().BeFalse("il servizio legge solo config.repeatable");
}

[Fact]
public async Task SaveAsTemplate_writes_the_journal_gesture_for_a_journal_composition()
{
    var sut = CreateSut();
    sut.Name = "Diario alimentare";
    sut.AddField(FieldNature.Text);          // una composizione di solo testo -> Journal
    sut.JournalGesture = JournalPrimaryGesture.QuickNote;

    await sut.SaveAsTemplateCommand.ExecuteAsync(null);

    var json = _templates.Created.Single().PayloadJson;
    using var doc = JsonDocument.Parse(json);
    doc.RootElement.GetProperty("ui_prefs").GetProperty("journal_gesture").GetString().Should().Be("QuickNote");
}

[Fact]
public void Journal_gesture_knob_is_offered_only_for_journal_compositions()
{
    var sut = CreateSut();
    sut.CountsOccurrences = true;               // -> Counter
    sut.ShowJournalGesture.Should().BeFalse();

    sut.CountsOccurrences = false;
    sut.AddField(FieldNature.Text);             // -> Journal
    sut.ShowJournalGesture.Should().BeTrue();
}
```

> `AddField(FieldNature.Text)` e `_templates.Created` sono i nomi usati dal file di test esistente. Se differiscono, adegua le chiamate a quelle già presenti invece di introdurre nuovi helper.

- [ ] **Step 2: Esegui e verifica che falliscano**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HabitBuilderViewModelTests"`
Expected: FAIL — `config` non esiste nel JSON; `JournalGesture`/`ShowJournalGesture` non compilano.

- [ ] **Step 3: Allinea la classe payload del builder al contratto del servizio**

In `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs`, sostituisci la classe mirror e il metodo che la riempie:

```csharp
    // Mirror della forma consumata da HabitTemplateService.InstantiateAsync (snake_case via options).
    // I nomi e l'annidamento devono restare identici a quelli del servizio: una chiave scritta qui e
    // non letta la' e' silenziosamente persa al re-instanziamento.
    private sealed class TemplatePayload
    {
        public NodePayload? Node { get; set; }
        public ConfigPayload? Config { get; set; }
        public UiPrefsPayload? UiPrefs { get; set; }
        public List<MetricPayload>? Metrics { get; set; }
    }

    private sealed class ConfigPayload
    {
        // null-when-false (WhenWritingNull) cosi' i template salvati prima di U8 restano byte-identici.
        public bool? Repeatable { get; set; }
    }

    private sealed class UiPrefsPayload
    {
        public string? JournalGesture { get; set; }
    }
```

e il costruttore del payload:

```csharp
    private string BuildTemplatePayloadJson()
    {
        var unitCodeById = AvailableUnits.ToDictionary(u => u.Id, u => u.Code);
        var scaleCodeById = AvailableScales.ToDictionary(s => s.Id, s => s.Code);

        var payload = new TemplatePayload
        {
            Node = new NodePayload
            {
                Name = Name.Trim(),
                NodeType = NodeType.Habit.ToString(),
                HabitType = DerivedType.ToString(),
                Color = Color,
                Icon = Icon,
            },
            Config = Repeatable ? new ConfigPayload { Repeatable = true } : null,
            UiPrefs = DerivedType == HabitType.Journal
                ? new UiPrefsPayload { JournalGesture = JournalGesture.ToString() }
                : null,
            Metrics = Fields
                .Where(f => f.Nature != FieldNature.Text)
                .Select(f => new MetricPayload
                {
                    Name = f.Name,
                    Type = f.MetricType.ToString(),
                    // Codici (non GUID) perche' InstantiateAsync risolve unita' e scala per Code.
                    Unit = f.UnitOfMeasureId is { } uid && unitCodeById.TryGetValue(uid, out var uc) ? uc : null,
                    ScaleCode = f.ScaleTemplateId is { } sid && scaleCodeById.TryGetValue(sid, out var sc) ? sc : null,
                })
                .ToList(),
        };
        return JsonSerializer.Serialize(payload, s_templateJsonOptions);
    }
```

Togli `SortOrder` da `MetricPayload` se è dichiarato lì: il servizio assegna l'ordine dalla posizione nell'array, quindi la chiave era scritta e mai letta. `counts_occurrences` e `mode` spariscono dal payload per lo stesso motivo — il tipo derivato è già codificato in `node.habit_type`, che il servizio legge davvero.

Aggiungi la manopola al VM, accanto a `Repeatable`:

```csharp
    /// <summary>Gesto primario di una composizione journal (Onda 2.4). Serializzato in ui_prefs
    /// quando la composizione si salva come template; per l'abitudine creata da Lancia lo imposta
    /// poi Configura, che e' l'unico posto dove si cambia dopo.</summary>
    [ObservableProperty] private JournalPrimaryGesture _journalGesture = JournalPrimaryGesture.Diary;

    public bool ShowJournalGesture => DerivedType == HabitType.Journal;
```

e nel metodo `Recompute()` (quello che già notifica `DerivedType`, `FieldsAllowed`, ecc.) aggiungi:

```csharp
        OnPropertyChanged(nameof(ShowJournalGesture));
```

- [ ] **Step 4: Esponi la manopola nella view del builder**

In `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml`, accanto al check "ripetibile" (cerca `IsChecked="{Binding Repeatable}"`), aggiungi:

```xml
<StackPanel Orientation="Horizontal" Margin="0,6,0,0"
            Visibility="{Binding ShowJournalGesture, Converter={StaticResource BoolToVisible}}">
    <TextBlock Text="{x:Static res:Strings.Editor_Registration_JournalGesture}"
               VerticalAlignment="Center" Margin="0,0,8,0"/>
    <ComboBox ItemsSource="{Binding JournalGestureOptions}"
              DisplayMemberPath="Label"
              SelectedValuePath="Gesture"
              SelectedValue="{Binding JournalGesture}"
              MinWidth="200"/>
</StackPanel>
```

e nel VM la lista delle opzioni (statica, mai svuotata — vedi il vincolo globale sulle `ItemsSource`):

```csharp
    public IReadOnlyList<JournalGestureOption> JournalGestureOptions { get; } =
    [
        new(JournalPrimaryGesture.Diary, Strings.Editor_Registration_Gesture_Diary),
        new(JournalPrimaryGesture.QuickNote, Strings.Editor_Registration_Gesture_QuickNote),
    ];
```

Il record `JournalGestureOption` viene definito in Task 4 (file `RegistrationSectionViewModel.cs`); se esegui i task fuori ordine, definiscilo prima lì.

Le tre stringhe usate qui sono create in Task 4. Se esegui questo task per primo, creale ora seguendo la procedura a 3 edit del blocco Global Constraints.

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HabitBuilderViewModelTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs
git commit -m "fix(builder): il payload salvato come template usa la forma che il servizio legge"
```

---

### Task 4: Il gesto si cambia da Configura

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/Sections/RegistrationSectionViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitEditor/Sections/RegistrationSectionView.xaml`
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` (riga 119, costruzione della sezione)
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/RegistrationSectionTests.cs`

**Interfaces:**
- Consumes: `IHabitUiPrefsService`, `FakeHabitUiPrefsService` (già in `tests/Recordum.WPF.Tests/Fixtures/`).
- Produces: `RegistrationSectionViewModel(IHabitConfigService configs, IHabitUiPrefsService uiPrefs)`; `record JournalGestureOption(JournalPrimaryGesture Gesture, string Label)`; proprietà `ShowRepeatable`, `ShowJournalGesture`, `SelectedGesture`.

- [ ] **Step 1: Aggiungi le tre stringhe**

In `src/Recordum.WPF/Resources/Strings.resx`:

```xml
  <data name="Editor.Registration.JournalGesture" xml:space="preserve">
    <value>Primary gesture</value>
  </data>
  <data name="Editor.Registration.Gesture.Diary" xml:space="preserve">
    <value>Write today's entry</value>
  </data>
  <data name="Editor.Registration.Gesture.QuickNote" xml:space="preserve">
    <value>Quick note with photo</value>
  </data>
```

In `src/Recordum.WPF/Resources/Strings.it.resx` gli stessi tre `name` con: `Gesto primario`, `Scrivi la voce di oggi`, `Nota veloce con foto`.

In `Strings.Designer.cs` — **via shell, non con Edit/Write** — aggiungi le tre proprietà accanto alle altre `Editor_Registration_*`:

```bash
python - <<'PY'
import io
p = "src/Recordum.WPF/Resources/Strings.Designer.cs"
s = io.open(p, encoding="utf-8-sig").read()
anchor = "    public static string Editor_Registration_Repeatable => Get(\"Editor.Registration.Repeatable\");"
add = anchor + """
    public static string Editor_Registration_JournalGesture => Get("Editor.Registration.JournalGesture");
    public static string Editor_Registration_Gesture_Diary => Get("Editor.Registration.Gesture.Diary");
    public static string Editor_Registration_Gesture_QuickNote => Get("Editor.Registration.Gesture.QuickNote");"""
assert anchor in s, "ancora non trovata: apri il file e allinea la stringa 'anchor'"
io.open(p, "w", encoding="utf-8-sig", newline="").write(s.replace(anchor, add, 1))
print("ok")
PY
```

Run: `dotnet build src/Recordum.WPF`
Expected: build pulita (conferma che il designer non è stato corrotto).

- [ ] **Step 2: Scrivi i test che falliscono**

In `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/RegistrationSectionTests.cs`:

```csharp
[Fact]
public void Applicable_to_counter_and_journal_habits()
{
    var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), new FakeHabitUiPrefsService());
    section.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeTrue();
    section.IsApplicable(NodeType.Habit, HabitType.Journal).Should().BeTrue();
    section.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeFalse();
    section.IsApplicable(NodeType.Group, null).Should().BeFalse();
}

[Fact]
public async Task Journal_shows_the_gesture_and_hides_the_repeatable_check()
{
    var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), new FakeHabitUiPrefsService());
    var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };

    await section.LoadAsync(node);

    section.ShowJournalGesture.Should().BeTrue();
    section.ShowRepeatable.Should().BeFalse();
}

[Fact]
public async Task Selecting_the_quick_note_gesture_persists_it_in_ui_prefs()
{
    var prefs = new FakeHabitUiPrefsService();
    var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), prefs);
    var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
    await section.LoadAsync(node);

    section.SelectedGesture = section.GestureOptions.Single(o => o.Gesture == JournalPrimaryGesture.QuickNote);
    await Task.Delay(50); // ApplyAsync e' fire-and-forget nelle sezioni

    (await prefs.GetAsync(node.Id)).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
}

[Fact]
public async Task LoadAsync_does_not_persist_the_loaded_gesture_back()
{
    // Stessa guardia del test gemello sul repeatable: la selezione impostata durante il load
    // non deve rimbalzare in scrittura (classe di bug 4d01f9c / 1d96967).
    var prefs = new FakeHabitUiPrefsService();
    var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
    await prefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });
    var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), prefs);

    await section.LoadAsync(node);

    section.SelectedGesture!.Gesture.Should().Be(JournalPrimaryGesture.QuickNote);
    await Task.Delay(50);
    (await prefs.GetAsync(node.Id)).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
}
```

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~RegistrationSectionTests"`
Expected: FAIL — costruttore a 1 argomento, membri inesistenti.

- [ ] **Step 3: Implementa la sezione**

Sostituisci `src/Recordum.WPF/ViewModels/HabitEditor/Sections/RegistrationSectionViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>"Registrazione" section: come si registra questa abitudine. Per i counter e' il flag
/// ripetibile (U8, persistito su habit_config); per i journal e' il gesto primario (U7, persistito
/// in ui_prefs_json). Due tipi, due manopole, due percorsi di persistenza — ma una sola domanda.</summary>
public sealed partial class RegistrationSectionViewModel(
    IHabitConfigService configs,
    IHabitUiPrefsService uiPrefs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Registration;

    private readonly IHabitConfigService _configs = configs;
    private readonly IHabitUiPrefsService _uiPrefs = uiPrefs;
    private bool _loading;
    private HabitUiPrefs _current = new();
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private bool _repeatable;
    [ObservableProperty] private bool _showRepeatable;
    [ObservableProperty] private bool _showJournalGesture;
    [ObservableProperty] private JournalGestureOption? _selectedGesture;

    /// <summary>Lista fissa: non e' mai svuotata, quindi non puo' innescare la coercizione-a-null
    /// che ha causato i bug 4d01f9c / 1d96967.</summary>
    public IReadOnlyList<JournalGestureOption> GestureOptions { get; } =
    [
        new(JournalPrimaryGesture.Diary, Strings.Editor_Registration_Gesture_Diary),
        new(JournalPrimaryGesture.QuickNote, Strings.Editor_Registration_Gesture_QuickNote),
    ];

    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit is HabitType.Counter or HabitType.Journal;

    public async Task LoadAsync(HabitNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _loading = true;
        try
        {
            NodeId = node.Id;
            ShowRepeatable = node.HabitType == HabitType.Counter;
            ShowJournalGesture = node.HabitType == HabitType.Journal;

            var cfg = await _configs.GetAsync(node.Id).ConfigureAwait(true);
            Repeatable = cfg?.Repeatable ?? false;

            _current = await _uiPrefs.GetAsync(node.Id).ConfigureAwait(true);
            var gesture = _current.JournalGesture ?? JournalPrimaryGesture.Diary;
            SelectedGesture = GestureOptions.First(o => o.Gesture == gesture);
        }
        finally { _loading = false; }
    }

    partial void OnRepeatableChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.Repeatable = value).AsTask());
    }

    partial void OnSelectedGestureChanged(JournalGestureOption? value)
    {
        if (_loading || NodeId == Guid.Empty || value is null) return;
        _current = _current with { JournalGesture = value.Gesture };
        _ = ApplyAsync(() => _uiPrefs.SetAsync(NodeId, _current).AsTask());
    }
}

/// <summary>Una scelta di gesto primario journal: l'enum + la sua etichetta localizzata.</summary>
public sealed record JournalGestureOption(JournalPrimaryGesture Gesture, string Label);
```

- [ ] **Step 4: Aggiorna la view e il sito di costruzione**

`src/Recordum.WPF/Views/HabitEditor/Sections/RegistrationSectionView.xaml`:

```xml
<UserControl x:Class="Recordum.WPF.Views.HabitEditor.Sections.RegistrationSectionView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:res="clr-namespace:Recordum.WPF.Resources">
    <StackPanel>
        <CheckBox Content="{x:Static res:Strings.Editor_Registration_Repeatable}"
                  IsChecked="{Binding Repeatable}" Margin="0,2"
                  Visibility="{Binding ShowRepeatable, Converter={StaticResource BoolToVisible}}"/>
        <StackPanel Margin="0,2"
                    Visibility="{Binding ShowJournalGesture, Converter={StaticResource BoolToVisible}}">
            <TextBlock Text="{x:Static res:Strings.Editor_Registration_JournalGesture}"
                       Foreground="{DynamicResource Brush.TextMuted}" Margin="0,0,0,4"/>
            <ComboBox ItemsSource="{Binding GestureOptions}"
                      DisplayMemberPath="Label"
                      SelectedItem="{Binding SelectedGesture}"/>
        </StackPanel>
    </StackPanel>
</UserControl>
```

> `BoolToVisible` deve essere raggiungibile da questo UserControl: se le altre section view lo prendono da un `ResourceDictionary` incluso in `SectionTemplates.xaml` o in `App.xaml`, non serve nulla; altrimenti aggiungi lo stesso `UserControl.Resources` che usano le sezioni vicine.

In `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` riga ~119:

```csharp
        Registration = new RegistrationSectionViewModel(configs, uiPrefs);
```

`uiPrefs` è già un parametro del costruttore di `HabitEditorViewModel` (lo usa `HomeRow`); se il nome locale differisce, usa quello.

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~RegistrationSectionTests"`
Expected: PASS, 6 test.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitEditor src/Recordum.WPF/Views/HabitEditor src/Recordum.WPF/Resources tests/Recordum.WPF.Tests/ViewModels/HabitEditor/RegistrationSectionTests.cs
git commit -m "feat(editor): il gesto primario journal si cambia da Configura"
```

---

### Task 5: `JournalQuickEntryViewModel` — la logica del composer

Solo VM e test, nessuna XAML: il task successivo la disegna. Il VM è testabile per intero con i fake già in `tests/Recordum.WPF.Tests/Fixtures/`.

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Journal/JournalQuickEntryViewModel.cs`
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Journal/JournalEntryRowViewModel.cs`
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/JournalQuickEntryViewModelTests.cs`

**Interfaces:**
- Consumes: `IDiaryService.SaveEntryAsync(Guid, DateOnly, string, CancellationToken) → ValueTask<Guid>`, `IDiaryService.EditEntryAsync(Guid, string, CancellationToken)`, `IDiaryService.DeleteEntryAsync(Guid, CancellationToken)`, `INoteService.ListByHabitAsync(Guid, CancellationToken)`, `IAttachmentService.AddAsync(Guid, string, CancellationToken) → Task<Attachment>`, `IAttachmentService.ListByNoteAsync`, `IOpenFileDialog.PromptMultiple(string) → string[]`, `IConfirmationDialog.ConfirmAsync(string, string) → Task<bool>`, `IClock`.
- Produces: `JournalQuickEntryViewModel` con `LoadAsync(HabitNode node, CancellationToken)`, `SetDay(DateOnly)`, `Draft` (string), `PendingPhotos` (`ObservableCollection<string>`), `Entries` (`ObservableCollection<JournalEntryRowViewModel>`), comandi `AddPhoto`, `Save`, `Delete`, `BeginEdit`, `CancelEdit`, `ErrorMessage`.

- [ ] **Step 1: Aggiungi le stringhe**

`Strings.resx` (inglese) — e le gemelle italiane in `Strings.it.resx`:

| name | EN | IT |
|---|---|---|
| `Diario.QuickEntry.Placeholder` | `What did you have?` | `Che cosa hai preso?` |
| `Diario.QuickEntry.AddPhoto` | `Add photo` | `Aggiungi foto` |
| `Diario.QuickEntry.Save` | `Save` | `Salva` |
| `Diario.QuickEntry.Today` | `Entries for this day` | `Voci di questo giorno` |
| `Diario.QuickEntry.Empty` | `Nothing recorded yet.` | `Ancora niente.` |
| `Diario.QuickEntry.Edit` | `Edit` | `Modifica` |
| `Diario.QuickEntry.Cancel` | `Cancel` | `Annulla` |
| `Diario.QuickEntry.Delete` | `Delete` | `Elimina` |
| `Diario.QuickEntry.Error.Empty` | `Write something or add a photo.` | `Scrivi qualcosa o aggiungi una foto.` |
| `Diario.QuickEntry.Error.PhotoFailed` | `Some photos could not be attached.` | `Alcune foto non sono state allegate.` |
| `Diario.QuickEntry.DeleteConfirm.Title` | `Delete entry` | `Elimina voce` |
| `Diario.QuickEntry.DeleteConfirm.Message` | `This entry and its photos will be removed.` | `Questa voce e le sue foto verranno rimosse.` |

Poi le 12 proprietà in `Strings.Designer.cs` **via shell** (stessa tecnica dello Step 1 di Task 4, con un'ancora presa da una proprietà `Diario_*` o `Notes_*` già esistente; se non ce n'è nessuna, ancora sull'ultima riga prima della chiusura della classe).

Run: `dotnet build src/Recordum.WPF`
Expected: build pulita.

- [ ] **Step 2: Scrivi i test che falliscono**

Crea `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/JournalQuickEntryViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Journal;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class JournalQuickEntryViewModelTests
{
    private static HabitNode Journal() => new()
    {
        Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Food diary",
    };

    private static (JournalQuickEntryViewModel Sut, FakeDiaryService Diary, FakeAttachmentService Photos,
                    FakeOpenFileDialog Dialog, FakeConfirmationDialog Confirm, FakeNoteService Notes) Build()
    {
        var diary = new FakeDiaryService();
        var notes = new FakeNoteService();
        var photos = new FakeAttachmentService();
        var dialog = new FakeOpenFileDialog();
        var confirm = new FakeConfirmationDialog();
        var sut = new JournalQuickEntryViewModel(diary, notes, photos, dialog, confirm, new FakeClock());
        return (sut, diary, photos, dialog, confirm, notes);
    }

    [Fact]
    public async Task Save_creates_a_diary_entry_so_the_day_counts_as_done()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal());
        sut.Draft = "Insalata di farro";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle()
            .Which.Text.Should().Be("Insalata di farro");
        sut.Draft.Should().BeEmpty("il composer si svuota dopo il salvataggio");
    }

    [Fact]
    public async Task Save_refuses_an_entry_with_neither_text_nor_photo()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal());
        sut.Draft = "   ";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().BeEmpty();
        sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_accepts_a_photo_with_no_text()
    {
        var (sut, diary, photos, dialog, _, _) = Build();
        await sut.LoadAsync(Journal());
        dialog.NextFiles = ["C:\\tmp\\piatto.jpg"];
        sut.AddPhotoCommand.Execute(null);

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle();
        photos.Added.Should().ContainSingle().Which.SourcePath.Should().Be("C:\\tmp\\piatto.jpg");
    }

    [Fact]
    public async Task Photos_are_attached_only_after_the_note_exists()
    {
        var (sut, diary, photos, dialog, _, _) = Build();
        await sut.LoadAsync(Journal());
        dialog.NextFiles = ["C:\\tmp\\a.jpg"];
        sut.AddPhotoCommand.Execute(null);

        // Prima del salvataggio non deve esistere nessuna nota e nessun allegato.
        diary.SavedEntries.Should().BeEmpty();
        photos.Added.Should().BeEmpty();
        sut.PendingPhotos.Should().ContainSingle();

        sut.Draft = "colazione";
        await sut.SaveCommand.ExecuteAsync(null);

        photos.Added.Single().NoteId.Should().Be(diary.SavedEntries.Single().NoteId);
        sut.PendingPhotos.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failing_photo_does_not_lose_the_entry()
    {
        var (sut, diary, photos, dialog, _, _) = Build();
        await sut.LoadAsync(Journal());
        photos.ThrowOnAdd = true;
        dialog.NextFiles = ["C:\\tmp\\rotta.jpg"];
        sut.AddPhotoCommand.Execute(null);
        sut.Draft = "pranzo";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle("la voce resta salvata anche se la foto fallisce");
        sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_uses_the_selected_day_so_yesterdays_dinner_lands_on_yesterday()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal());
        var yesterday = new DateOnly(2026, 8, 7);
        sut.SetDay(yesterday);
        sut.Draft = "cena di ieri";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Single().Day.Should().Be(yesterday);
    }

    [Fact]
    public async Task Delete_asks_for_confirmation_and_removes_the_entry()
    {
        var (sut, diary, _, _, confirm, _) = Build();
        await sut.LoadAsync(Journal());
        sut.Draft = "da buttare";
        await sut.SaveCommand.ExecuteAsync(null);
        var row = sut.Entries.Single();

        confirm.Answer = false;
        await sut.DeleteCommand.ExecuteAsync(row);
        diary.DeletedIds.Should().BeEmpty("un rifiuto non cancella niente");

        confirm.Answer = true;
        await sut.DeleteCommand.ExecuteAsync(row);
        diary.DeletedIds.Should().ContainSingle();
        sut.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Editing_a_row_updates_the_body_without_creating_a_second_entry()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal());
        sut.Draft = "Pasta al pesto";
        await sut.SaveCommand.ExecuteAsync(null);
        var row = sut.Entries.Single();

        sut.BeginEditCommand.Execute(row);
        row.EditDraft = "Pasta al pomodoro";
        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle("la modifica non crea una voce nuova");
        diary.EditedEntries.Single().Text.Should().Be("Pasta al pomodoro");
        sut.Entries.Single().Text.Should().Be("Pasta al pomodoro");
    }
}
```

Se `FakeAttachmentService`, `FakeOpenFileDialog`, `FakeConfirmationDialog`, `FakeNoteService` non esistono già in `tests/Recordum.WPF.Tests/Fixtures/`, creali con la forma minima usata sopra: liste pubbliche di ciò che hanno ricevuto (`Added` con `NoteId`/`SourcePath`, `SavedEntries` con `NoteId`/`Day`/`Text`, `EditedEntries`, `DeletedIds`), `NextFiles`, `Answer`, `ThrowOnAdd`. `FakeDiaryService` esiste già: estendilo con le liste che qui mancano invece di duplicarlo.

- [ ] **Step 3: Esegui e verifica che falliscano**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~JournalQuickEntryViewModelTests"`
Expected: FAIL — il tipo `JournalQuickEntryViewModel` non esiste.

- [ ] **Step 4: Implementa il row VM**

`src/Recordum.WPF/ViewModels/HabitScreen/Journal/JournalEntryRowViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Notes;

namespace Recordum.WPF.ViewModels.HabitScreen.Journal;

/// <summary>Una voce di diario nella lista del giorno. Nessun comando proprio: modifica ed
/// eliminazione appartengono al VM padre, come per NoteRowViewModel.</summary>
public sealed partial class JournalEntryRowViewModel(Note note) : ObservableObject
{
    public Guid Id { get; } = note.Id;
    public DateTime CreatedAt { get; } = note.CreatedAt;

    [ObservableProperty] private string _text = note.BodyMd;

    /// <summary>Testo in corso di modifica in riga; valorizzato solo mentre <see cref="IsEditing"/>.</summary>
    [ObservableProperty] private string _editDraft = string.Empty;

    [ObservableProperty] private bool _isEditing;

    public ObservableCollection<AttachmentViewModel> Photos { get; } = [];
}
```

- [ ] **Step 5: Implementa il VM del composer**

`src/Recordum.WPF/ViewModels/HabitScreen/Journal/JournalQuickEntryViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;
using Recordum.WPF.ViewModels.HabitScreen.Notes;

namespace Recordum.WPF.ViewModels.HabitScreen.Journal;

/// <summary>Composer "nota veloce" della colonna Diario (Onda 2.4, U7). Ogni salvataggio e' una
/// voce di diario vera — Nota(Diario) + Completion via IDiaryService — cosi' streak, heatmap e
/// cifra chiave funzionano senza semantica nuova. Le foto restano percorsi in memoria finche' non
/// esiste il noteId: allegare prima vorrebbe dire creare una nota-bozza che, se abbandoni, resta
/// orfana.</summary>
public sealed partial class JournalQuickEntryViewModel : ObservableObject
{
    private const string ImageFileFilter =
        "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp";

    private readonly IDiaryService _diary;
    private readonly INoteService _notes;
    private readonly IAttachmentService _attachments;
    private readonly IOpenFileDialog _dialog;
    private readonly IConfirmationDialog _confirm;
    private readonly IClock _clock;

    private Guid _habitNodeId;
    private DateOnly _day;

    [ObservableProperty] private string _draft = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isSaving;

    /// <summary>Percorsi scelti e non ancora allegati. Diventano allegati solo dopo il salvataggio.</summary>
    public ObservableCollection<string> PendingPhotos { get; } = [];

    public ObservableCollection<JournalEntryRowViewModel> Entries { get; } = [];

    public JournalQuickEntryViewModel(
        IDiaryService diary,
        INoteService notes,
        IAttachmentService attachments,
        IOpenFileDialog dialog,
        IConfirmationDialog confirm,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(diary);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(clock);
        _diary = diary;
        _notes = notes;
        _attachments = attachments;
        _dialog = dialog;
        _confirm = confirm;
        _clock = clock;
        _day = Today();
    }

    private DateOnly Today() => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone));

    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        _habitNodeId = node.Id;
        Draft = string.Empty;
        ErrorMessage = null;
        PendingPhotos.Clear();
        await ReloadEntriesAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Allinea il composer al giorno selezionato nel calendario di Cronologia — e' cosi'
    /// che si registra la cena di ieri, senza un secondo selettore di data.</summary>
    public void SetDay(DateOnly day)
    {
        _day = day;
        _ = ReloadEntriesAsync(CancellationToken.None);
    }

    private async Task ReloadEntriesAsync(CancellationToken ct)
    {
        if (_habitNodeId == Guid.Empty) return;
        var all = await _notes.ListByHabitAsync(_habitNodeId, ct).ConfigureAwait(true);
        var ofDay = all
            .Where(n => n.Category == NoteCategory.Diario
                        && (n.TargetDate ?? DateOnly.FromDateTime(n.CreatedAt)) == _day)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        Entries.Clear();
        foreach (var n in ofDay)
        {
            var row = new JournalEntryRowViewModel(n);
            Entries.Add(row);
            foreach (var a in await _attachments.ListByNoteAsync(n.Id, ct).ConfigureAwait(true))
                row.Photos.Add(new AttachmentViewModel(a, _attachments));
        }
    }

    [RelayCommand]
    private void AddPhoto()
    {
        foreach (var path in _dialog.PromptMultiple(ImageFileFilter))
            PendingPhotos.Add(path);
    }

    [RelayCommand]
    private void RemovePendingPhoto(string? path)
    {
        if (path is not null) PendingPhotos.Remove(path);
    }

    [RelayCommand]
    private void BeginEdit(JournalEntryRowViewModel? row)
    {
        if (row is null) return;
        foreach (var r in Entries) r.IsEditing = false;
        row.EditDraft = row.Text;
        row.IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        foreach (var r in Entries) r.IsEditing = false;
    }

    [RelayCommand]
    private async Task Save(CancellationToken ct)
    {
        if (_habitNodeId == Guid.Empty) return;
        ErrorMessage = null;

        // Una modifica in riga in corso vince sul composer: e' l'azione che l'utente sta guardando.
        if (Entries.FirstOrDefault(r => r.IsEditing) is { } editing)
        {
            await _diary.EditEntryAsync(editing.Id, editing.EditDraft, ct).ConfigureAwait(true);
            editing.Text = editing.EditDraft;
            editing.IsEditing = false;
            return;
        }

        var text = Draft?.Trim() ?? string.Empty;
        if (text.Length == 0 && PendingPhotos.Count == 0)
        {
            ErrorMessage = Strings.Diario_QuickEntry_Error_Empty;
            return;
        }

        IsSaving = true;
        try
        {
            var noteId = await _diary.SaveEntryAsync(_habitNodeId, _day, text, ct).ConfigureAwait(true);

            var failed = false;
            foreach (var path in PendingPhotos.ToList())
            {
                try
                {
                    await _attachments.AddAsync(noteId, path, ct).ConfigureAwait(true);
                }
#pragma warning disable CA1031 // un file illeggibile non deve abortire il resto del batch
                catch (Exception)
#pragma warning restore CA1031
                {
                    failed = true;
                }
            }
            if (failed) ErrorMessage = Strings.Diario_QuickEntry_Error_PhotoFailed;

            Draft = string.Empty;
            PendingPhotos.Clear();
            await ReloadEntriesAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task Delete(JournalEntryRowViewModel? row, CancellationToken ct)
    {
        if (row is null) return;
        var ok = await _confirm.ConfirmAsync(
            Strings.Diario_QuickEntry_DeleteConfirm_Title,
            Strings.Diario_QuickEntry_DeleteConfirm_Message).ConfigureAwait(true);
        if (!ok) return;
        await _diary.DeleteEntryAsync(row.Id, ct).ConfigureAwait(true);
        Entries.Remove(row);
    }
}
```

> `Entries.Clear()` in `ReloadEntriesAsync` è sicuro: `Entries` è la sorgente di un `ItemsControl` in sola lettura, non di un selettore bindato two-way. Il vincolo globale riguarda le `ItemsSource` che alimentano un `SelectedItem`/`SelectedValue` persistito — qui non ce ne sono.

> **Il caso limite della spec §5 "cambio abitudine con un salvataggio in volo" è chiuso per costruzione**, non da un guard: `_habitNodeId` è catturato al `LoadAsync` e il VM è istanziato per-abitudine da `BuildQuickAddAsync` (Task 6), quindi un salvataggio partito da questa abitudine finisce su questa abitudine e la lista che si ricarica appartiene alla stessa istanza. Non serve la generation guard di CR14/CR18; non aggiungerla.

- [ ] **Step 6: Esegui i test e verifica che passino**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~JournalQuickEntryViewModelTests"`
Expected: PASS, 8 test.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Journal src/Recordum.WPF/Resources tests/Recordum.WPF.Tests
git commit -m "feat(diario): composer nota veloce — VM, foto in attesa, voci del giorno"
```

---

### Task 6: La colonna Diario mostra il composer, e la fascia "Oggi" ci porta

**Files:**
- Create: `src/Recordum.WPF/Views/HabitScreen/JournalQuickEntryView.xaml` (+ `.xaml.cs`)
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (riga ~1197-1217, host della colonna Diario)
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (riga ~473 routing, ~662 `BuildQuickAddAsync`, ~1092 `OnTodayStripAction`)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenViewModelTests.cs`

**Interfaces:**
- Consumes: `JournalQuickEntryViewModel` (Task 5), `IHabitUiPrefsService`, `TodayContentAction`.
- Produces: su `HabitScreenViewModel` — `JournalQuickEntry` (`JournalQuickEntryViewModel?`), `QuickEntryForDiario`, e `QuickAddForDiario` ristretto al solo gesto `Diary`.

- [ ] **Step 1: Scrivi i test che falliscono**

In `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenViewModelTests.cs` (riusa l'helper di costruzione già presente nel file):

```csharp
[Fact]
public async Task Quick_note_journal_hosts_the_composer_and_never_the_markdown_editor()
{
    var (sut, fx) = CreateSut();
    var node = await fx.AddJournalAsync("Food diary");
    await fx.UiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });

    await sut.LoadAsync(node.Id);

    sut.QuickEntryForDiario.Should().NotBeNull();
    sut.QuickAddForDiario.Should().BeNull("WebView2 non deve istanziarsi per un'abitudine a nota veloce");
}

[Fact]
public async Task Diary_journal_still_hosts_the_markdown_editor()
{
    var (sut, fx) = CreateSut();
    var node = await fx.AddJournalAsync("Daily journal");
    await fx.UiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.Diary });

    await sut.LoadAsync(node.Id);

    sut.QuickAddForDiario.Should().NotBeNull();
    sut.QuickEntryForDiario.Should().BeNull();
}

[Fact]
public async Task A_journal_with_no_pref_defaults_to_the_diary_editor()
{
    var (sut, fx) = CreateSut();
    var node = await fx.AddJournalAsync("Senza prefs");

    await sut.LoadAsync(node.Id);

    sut.QuickAddForDiario.Should().NotBeNull();
}

[Fact]
public async Task The_quick_note_affordance_opens_the_Diario_column()
{
    var (sut, fx) = CreateSut();
    var node = await fx.AddJournalAsync("Food diary");
    await fx.UiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });
    await sut.LoadAsync(node.Id);
    sut.IsDiarioOpen = false;

    sut.RaiseTodayStripActionForTest(TodayContentAction.OpenQuickNote);

    sut.IsDiarioOpen.Should().BeTrue("il composer vive in Diario, non in Note");
    sut.IsNoteOpen.Should().BeFalse();
}
```

Se non esiste, aggiungi il seam di test accanto agli altri già presenti nel VM:

```csharp
    /// <summary>Test seam: OnTodayStripAction e' privato e normalmente arriva da un evento della fascia.</summary>
    internal void RaiseTodayStripActionForTest(TodayContentAction action) => OnTodayStripAction(this, action);
```

> `CreateSut()` e `fx.AddJournalAsync(...)` sono i nomi ipotizzati per gli helper del file di test: **leggi le prime righe di `HabitScreenViewModelTests.cs` e usa quelli veri**, invece di introdurre helper nuovi. Se la fixture non espone un `UiPrefs` scrivibile, aggiungilo lì una volta sola — servirà a tutti e quattro i test.

- [ ] **Step 2: Esegui e verifica che falliscano**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~HabitScreenViewModelTests"`
Expected: FAIL — `QuickEntryForDiario` non esiste.

- [ ] **Step 3: Implementa il routing nel VM**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`:

Aggiungi il campo e le proprietà accanto a `QuickAddForDiario` (riga ~473):

```csharp
    [ObservableProperty] private JournalQuickEntryViewModel? _journalQuickEntry;

    /// <summary>Vero quando il journal caricato ha il gesto "nota veloce". Deciso al load da
    /// ui_prefs_json; null = default del tipo = Diary.</summary>
    private bool _isQuickNoteJournal;

    // La colonna Diario ospita O il composer O l'editor Markdown, mai entrambi: cosi' WebView2
    // continua a istanziarsi una volta sola (cautela spec §3.5) e su un food diary mai.
    public QuickAddViewModelBase? QuickAddForDiario => IsJournal && !_isQuickNoteJournal ? QuickAdd : null;
    public JournalQuickEntryViewModel? QuickEntryForDiario => IsJournal && _isQuickNoteJournal ? JournalQuickEntry : null;
    public QuickAddViewModelBase? QuickAddForCronologia => IsJournal ? null : QuickAdd;

    private void NotifyQuickAddRouting()
    {
        OnPropertyChanged(nameof(QuickAddForDiario));
        OnPropertyChanged(nameof(QuickEntryForDiario));
        OnPropertyChanged(nameof(QuickAddForCronologia));
    }
```

In `BuildQuickAddAsync` (riga ~662), prima dello `switch` sul tipo, leggi il gesto e costruisci il composer:

```csharp
        _isQuickNoteJournal = false;
        JournalQuickEntry = null;
        if (node.HabitType == HabitType.Journal)
        {
            var prefs = await _uiPrefs.GetAsync(node.Id, ct).ConfigureAwait(true);
            _isQuickNoteJournal = prefs.JournalGesture == JournalPrimaryGesture.QuickNote;
            if (_isQuickNoteJournal)
            {
                var composer = new JournalQuickEntryViewModel(
                    _diary, _notes, _attachments, _openFileDialog, _confirmationDialog, _clock);
                await composer.LoadAsync(node, ct).ConfigureAwait(true);
                JournalQuickEntry = composer;
                QuickAdd = null;          // niente editor Markdown per questa abitudine
                NotifyQuickAddRouting();
                return;
            }
        }
```

`_uiPrefs`, `_notes`, `_attachments`, `_openFileDialog`, `_confirmationDialog` vanno aggiunti come campi e parametri del costruttore di `HabitScreenViewModel` se non ci sono già (`_diary` e `_clock` ci sono). Aggiorna di conseguenza la registrazione in `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` e l'helper `CreateSut` dei test.

Collega il calendario: dove `HabitRecordsViewModel`/`NotesViewModel` ricevono già `SetCalendarDay(day)`, aggiungi accanto:

```csharp
        JournalQuickEntry?.SetDay(day);
```

E correggi la destinazione della fascia (riga ~1092):

```csharp
        var column = action switch
        {
            TodayContentAction.OpenDiario => HabitColumn.Diario,
            // Il composer nota veloce vive in Diario (Onda 2.4): prima puntava a Note, dove non c'e'.
            TodayContentAction.OpenQuickNote => HabitColumn.Diario,
            _ => HabitColumn.Cronologia,
        };
```

- [ ] **Step 4: Crea la view**

`src/Recordum.WPF/Views/HabitScreen/JournalQuickEntryView.xaml`:

```xml
<UserControl x:Class="Recordum.WPF.Views.HabitScreen.JournalQuickEntryView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:res="clr-namespace:Recordum.WPF.Resources"
             xmlns:vmj="clr-namespace:Recordum.WPF.ViewModels.HabitScreen.Journal"
             d:DataContext="{d:DesignInstance vmj:JournalQuickEntryViewModel}"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <DockPanel Margin="12">
        <!-- Composer -->
        <StackPanel DockPanel.Dock="Top">
            <TextBox Text="{Binding Draft, UpdateSourceTrigger=PropertyChanged}"
                     AcceptsReturn="True" TextWrapping="Wrap" MinLines="2" MaxLines="6"
                     VerticalScrollBarVisibility="Auto"
                     Tag="{x:Static res:Strings.Diario_QuickEntry_Placeholder}">
                <TextBox.InputBindings>
                    <KeyBinding Key="Return" Modifiers="Control" Command="{Binding SaveCommand}"/>
                </TextBox.InputBindings>
            </TextBox>

            <ItemsControl ItemsSource="{Binding PendingPhotos}" Margin="0,8,0,0">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><WrapPanel Orientation="Horizontal"/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Grid Margin="0,0,6,6">
                            <Image Source="{Binding}" Width="48" Height="48" Stretch="UniformToFill"/>
                            <Button Content="✕" Width="16" Height="16" Padding="0"
                                    HorizontalAlignment="Right" VerticalAlignment="Top"
                                    Command="{Binding DataContext.RemovePendingPhotoCommand,
                                              RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}"/>
                        </Grid>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                <Button Content="{x:Static res:Strings.Diario_QuickEntry_AddPhoto}"
                        Command="{Binding AddPhotoCommand}" Padding="10,5"/>
                <Button Content="{x:Static res:Strings.Diario_QuickEntry_Save}"
                        Command="{Binding SaveCommand}" Padding="12,5" Margin="8,0,0,0"/>
            </StackPanel>

            <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" Margin="0,6,0,0"
                       Visibility="{Binding ErrorMessage, Converter={StaticResource NullToVisibility}}"/>

            <TextBlock Text="{x:Static res:Strings.Diario_QuickEntry_Today}"
                       Foreground="{DynamicResource Brush.TextMuted}" Margin="0,16,0,4"/>
        </StackPanel>

        <!-- Voci del giorno -->
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding Entries}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border BorderThickness="0,0,0,1" BorderBrush="{DynamicResource Brush.Border}" Padding="0,8">
                            <StackPanel>
                                <DockPanel>
                                    <TextBlock DockPanel.Dock="Left" Margin="0,0,8,0"
                                               Foreground="{DynamicResource Brush.TextMuted}"
                                               Text="{Binding CreatedAt, StringFormat=HH:mm}"/>
                                    <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                                        <Button Content="{x:Static res:Strings.Diario_QuickEntry_Edit}"
                                                Command="{Binding DataContext.BeginEditCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                CommandParameter="{Binding}" Padding="6,2"
                                                Visibility="{Binding IsEditing, Converter={StaticResource InvBoolToVisible}}"/>
                                        <Button Content="{x:Static res:Strings.Diario_QuickEntry_Delete}"
                                                Command="{Binding DataContext.DeleteCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                CommandParameter="{Binding}" Padding="6,2" Margin="6,0,0,0"/>
                                    </StackPanel>
                                    <TextBlock Text="{Binding Text}" TextWrapping="Wrap"
                                               Visibility="{Binding IsEditing, Converter={StaticResource InvBoolToVisible}}"/>
                                </DockPanel>

                                <StackPanel Visibility="{Binding IsEditing, Converter={StaticResource BoolToVisible}}">
                                    <TextBox Text="{Binding EditDraft, UpdateSourceTrigger=PropertyChanged}"
                                             AcceptsReturn="True" TextWrapping="Wrap" MinLines="2"/>
                                    <StackPanel Orientation="Horizontal" Margin="0,6,0,0">
                                        <Button Content="{x:Static res:Strings.Diario_QuickEntry_Save}"
                                                Command="{Binding DataContext.SaveCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                Padding="10,4"/>
                                        <Button Content="{x:Static res:Strings.Diario_QuickEntry_Cancel}"
                                                Command="{Binding DataContext.CancelEditCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                Padding="10,4" Margin="6,0,0,0"/>
                                    </StackPanel>
                                </StackPanel>

                                <ItemsControl ItemsSource="{Binding Photos}" Margin="0,6,0,0">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate><WrapPanel Orientation="Horizontal"/></ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <Image Source="{Binding FilePath}" Width="56" Height="56"
                                                   Stretch="UniformToFill" Margin="0,0,6,6"/>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

Il code-behind è il costruttore vuoto con `InitializeComponent()`, come le altre view di questa cartella.

> I convertitori `BoolToVisible`, `InvBoolToVisible` e `NullToVisibility` usati qui sono dichiarati nelle risorse di `HabitScreen.xaml`, che **non** sono visibili da un UserControl separato. Verifica dove li prende `NotesTabView.xaml` (l'altra view estratta della stessa colonna) e replica esattamente quella soluzione — un `ResourceDictionary` condiviso o un `UserControl.Resources` locale. Un binding a un convertitore non risolto non fallisce la build: fallisce a runtime, silenziosamente, lasciando il controllo sempre visibile.

- [ ] **Step 5: Aggancia la view alla colonna Diario**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`, nel `Border` della colonna Diario (riga ~1199), sostituisci il singolo `ContentControl` con due host mutuamente esclusivi — uno solo dei due binding è non-null, quindi solo uno materializza il contenuto:

```xml
                                <Grid>
                                    <ContentControl Content="{Binding QuickAddForDiario}"
                                                    ContentTemplate="{StaticResource JournalDiarioTemplate}"/>
                                    <ContentControl Content="{Binding QuickEntryForDiario}"/>
                                </Grid>
```

e aggiungi il `DataTemplate` implicito accanto agli altri (dopo `JournalDiarioTemplate`, riga ~720):

```xml
        <DataTemplate DataType="{x:Type vmj:JournalQuickEntryViewModel}">
            <v:JournalQuickEntryView/>
        </DataTemplate>
```

con lo xmlns `xmlns:vmj="clr-namespace:Recordum.WPF.ViewModels.HabitScreen.Journal"` dichiarato in testa al file.

- [ ] **Step 6: Esegui i test e verifica che passino**

Run: `dotnet test tests/Recordum.WPF.Tests`
Expected: PASS, suite intera.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF tests/Recordum.WPF.Tests
git commit -m "feat(diario): la colonna mostra il composer quando il gesto e' nota veloce"
```

---

### Task 7: Documentazione e verifica finale

**Files:**
- Modify: `docs/ARCHITECTURE.md` (tabella chiavi `ui_prefs_json`, §"habit_node.ui_prefs_json keys")
- Modify: `docs/phase-4-issues.md` (U7 → `fixed`)
- Modify: `CLAUDE.md` (§6 delta schema, §7 progresso)
- Modify: `.superpowers/sdd/progress.md` (ledger di sessione)

- [ ] **Step 1: Documenta la chiave `ui_prefs_json`**

In `docs/ARCHITECTURE.md`, nella tabella sotto "#### `habit_node.ui_prefs_json` keys", aggiungi:

```markdown
| `journalGesture` | `"Diary"` \| `"QuickNote"` | Gesto primario di un habit journal: scrivere la voce di oggi nell'editor Markdown, oppure il composer nota veloce con foto. Assente = `Diary`. | Onda 2.4 |
```

- [ ] **Step 2: Chiudi U7 nel tracker**

In `docs/phase-4-issues.md`, porta la voce **U7** a `fixed` e annota che le tre metriche fantasma del template sono state rimosse dal seed ma **restano** sulle abitudini già istanziate (nessun backfill, sono dati utente).

- [ ] **Step 3: Aggiorna CLAUDE.md**

In §1 aggiorna la catena dei delta schema mettendo in testa `food_diary_quick_note` v2.17 (data-only: il template "Food diary" perde le metriche e dichiara `journal_gesture: QuickNote`, `Daily journal` dichiara `Diary`). In §7 aggiungi un paragrafo di progresso datato con: la decisione che la nota veloce È una voce di diario, il composer in colonna Diario, il template riallineato, e il fix del payload builder di Task 3.

- [ ] **Step 4: Esegui tutte le suite**

```bash
dotnet test tests/Recordum.Core.Tests
dotnet test tests/Recordum.Data.Tests
dotnet test tests/Recordum.WPF.Tests
dotnet test tests/Recordum.Plugins.Tests
dotnet test tests/Recordum.Plugins.ExportEngine.Tests
dotnet test tests/Recordum.Plugins.ChartEngine.Tests
```

Expected: tutte verdi. Rispetto alla baseline (Core 113 / Data 432 / WPF 842 / Plugins 66 / ExportEngine 37 / ChartEngine 18) i conteggi Data e WPF salgono; nessuna suite deve scendere.

- [ ] **Step 5: Commit**

```bash
git add docs CLAUDE.md
git commit -m "docs: U7 chiuso — schema 2.17, chiave journalGesture"
```

> `.superpowers/` è gitignored e `progress.md` **non è tracciato**: aggiornalo, ma non provare a committarlo — `git add` fallirebbe. È un ledger locale della macchina, non parte della cronologia.

---

## Note per chi esegue

- **Ordine di esecuzione: 1 → 2 → 4 → 3 → 5 → 6 → 7.** Due dipendenze reali, e sono le sole:
  - **Task 1 prima di Task 2**: il seed scrive `ui_prefs` nel payload, e senza il parsing quella chiave verrebbe ignorata — il test del gesto fallirebbe per il motivo sbagliato.
  - **Task 4 prima di Task 3**: le tre stringhe `Editor_Registration_*` e il record `JournalGestureOption` nascono in Task 4, e Task 3 li consuma. Eseguirli nell'ordine numerato costringerebbe Task 3 a crearli, duplicando il lavoro.
- **Task 3 è separabile.** Chiude un bug preesistente (il payload salvato dal builder in una forma che il servizio non rilegge) e non serve a far funzionare il food diary spedito. Se l'autore decide di stralciarlo, va aperto un finding nel tracker invece di lasciarlo implicito: senza, "template = composizione salvata" resta rotto per `repeatable`.
- **Niente verifica visiva in quest'onda.** L'autore guarda tutta l'Onda 2 una volta sola dopo la 2.5.
