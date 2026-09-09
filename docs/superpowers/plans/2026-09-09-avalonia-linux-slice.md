# Fetta verticale Avalonia per Linux — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un'app Avalonia che su Linux si avvia, apre un database SQLCipher sbloccato da passphrase, mostra l'albero delle abitudini nella Home riusando i ViewModel di WPF **senza riscriverli**, registra il completamento di un'abitudine *action*, e conserva il dato al riavvio.

**Architecture:** Nuovo progetto `src/Recordum.Avalonia` (`net10.0`, nessun riferimento a `Recordum.WPF`) che **compila gli stessi file `.cs` dei ViewModel** via `<Compile Include>`, elencati uno per uno. Due assembly, una sorgente: se un ViewModel non è portabile, non compila. L'estrazione di un vero `Recordum.Presentation` è l'**esito** di questa fetta, non la sua premessa.

**Tech Stack:** C#/.NET 10, Avalonia 12.1.2 + Avalonia.Desktop, Avalonia.Headless.XUnit 12.1.2, CommunityToolkit.Mvvm (già in uso), EF Core + SQLite/SQLCipher, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-09-avalonia-linux-slice-design.md` — **leggerla prima di ogni task**.

## Global Constraints

- **Branch**: creare `avalonia-linux-slice` **da `r9-day-boundary`** (dove vivono spec e piano), in una chiamata separata dal primo commit — `guard-main.js` legge il branch *prima* di eseguire, quindi un comando composto che parte da un branch protetto viene negato per intero. Mai commit su `main`.
- **Nessuna modifica di comportamento a WPF.** Le uniche righe che si toccano in `src/Recordum.WPF/` sono le due cuciture del Task 4. Ogni altro file WPF resta invariato: la suite WPF non è eseguibile su questa macchina, quindi ciò che non si può provare non si tocca.
- **Nessun cambio di schema, nessuna migration.** `schema_version` resta 2.18.
- **Ambiente** (tutti i comandi presuppongono queste due righe):
  ```bash
  export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  ```
- **`dotnet test` può usare un binario stale su questa macchina** (misurato il 2026-09-09: 143 test invece di 145 dopo averne aggiunti due). Quando il conteggio non torna: `dotnet build <proj> --no-incremental` seguito da `dotnet test <proj> --no-build`. Un test "verde" mai eseguito è peggio di un test rosso.
- **`-p:EnableWindowsTargeting=true`** serve a ogni build che tocchi la soluzione intera, perché contiene progetti `net10.0-windows`. Il progetto Avalonia da solo non ne ha bisogno.
- **Suite di riferimento su Linux prima di cominciare**: Core 145 / Data 478 (+18 DPAPI, Windows-only per natura, **attesi rossi**) / Plugins 66 / ExportEngine 37. Se un altro numero cambia, è questa fetta.
- **Regola di convivenza (spec §9)**: finché questa fetta è esplorativa, **le feature nuove si fanno solo su WPF** e Avalonia insegue. Questo piano non aggiunge funzioni all'app: le porta.
- **Stringhe utente in `.resx`** (English-first). Questa fetta non dovrebbe aggiungerne: se un task ne chiede una, è il segnale che sta allargando lo scope. L'unica eccezione prevista è la finestra di sblocco del Task 3.
- Commit frequenti. Messaggi `feat(avalonia):` / `fix(avalonia):` / `test(avalonia):` / `docs:`, footer:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
  ```

## Il criterio di successo, fissato prima di cominciare

A fetta conclusa si conta:

```bash
git diff --stat $(git merge-base HEAD r9-day-boundary)..HEAD -- src/Recordum.WPF/ViewModels/
```

**Preventivo: un file solo** (`HomeClockTicker.cs`, Task 4), più lo spostamento di `DropPlan` che è una riga. Il numero reale, qualunque sia, va scritto nel commit di chiusura del Task 9 — è il dato per cui questa fetta esiste.

---

## File Structure

**Creati:**

| File | Responsabilità |
|---|---|
| `src/Recordum.Avalonia/Recordum.Avalonia.csproj` | progetto shell + elenco esplicito dei file di ViewModel collegati + resx collegate |
| `src/Recordum.Avalonia/Program.cs` | entry point desktop |
| `src/Recordum.Avalonia/App.axaml` + `.axaml.cs` | applicazione, tema, composizione DI |
| `src/Recordum.Avalonia/Themes/Tokens.axaml` | i token minimi (sfondo, testo, muted, accento, bordo, raggio) in chiaro e scuro |
| `src/Recordum.Avalonia/Bootstrap/SliceServices.cs` | composition root: registra ciò che la fetta usa, e nient'altro |
| `src/Recordum.Avalonia/Bootstrap/SliceInitializer.cs` | apre il DB con la chiave, migra, semina la demo se vuoto |
| `src/Recordum.Avalonia/Bootstrap/AvaloniaTickSource.cs` | implementazione di `ITickSource` su `Avalonia.Threading.DispatcherTimer` |
| `src/Recordum.Avalonia/Views/UnlockWindow.axaml` + `.axaml.cs` | passphrase all'avvio |
| `src/Recordum.Avalonia/Views/MainWindow.axaml` + `.axaml.cs` | finestra principale, ospita la Home |
| `src/Recordum.Avalonia/Views/HomeView.axaml` + `.axaml.cs` | l'albero delle righe |
| `tests/Recordum.Avalonia.Tests/…` | smoke headless, DI, initializer, tap |

**Modificati (i soli file WPF toccati da questo piano):**

| File | Cosa |
|---|---|
| `src/Recordum.Core/Abstractions/ITickSource.cs` (**nuovo**, in Core) | l'astrazione del battito |
| `src/Recordum.WPF/ViewModels/Home/HomeClockTicker.cs` | riceve un `ITickSource` invece di costruire un `DispatcherTimer` |
| `src/Recordum.WPF/Services/WpfTickSource.cs` (**nuovo**) | l'implementazione WPF |
| `src/Recordum.WPF/ViewModels/DropPlan.cs` (**nuovo**) | `DropPlan` esce da `TreeDragDropBehavior` |
| `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` | usa il `DropPlan` spostato |
| `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` | registra `ITickSource` → `WpfTickSource` |
| `Recordum.slnx` | i due progetti nuovi |

---

### Task 1: Il progetto esiste, la finestra si apre, e il test lo prova headless

Il primo obiettivo non è la Home: è avere un'app Avalonia che compila e si testa **su questa macchina**. Tutto il resto poggia qui.

**Files:**
- Create: `src/Recordum.Avalonia/Recordum.Avalonia.csproj`, `Program.cs`, `App.axaml`, `App.axaml.cs`, `Views/MainWindow.axaml`, `Views/MainWindow.axaml.cs`
- Create: `tests/Recordum.Avalonia.Tests/Recordum.Avalonia.Tests.csproj`, `SmokeTests.cs`
- Modify: `Recordum.slnx`

**Interfaces:**
- Produces: `Recordum.Avalonia.App` (classe `Application`), `Recordum.Avalonia.Views.MainWindow` (classe `Window`). Il Task 5 sostituirà il contenuto della finestra.

- [ ] **Step 1: Creare il progetto shell**

`src/Recordum.Avalonia/Recordum.Avalonia.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>false</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.2" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.2" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.2" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../Recordum.Core/Recordum.Core.csproj" />
    <ProjectReference Include="../Recordum.Data/Recordum.Data.csproj" />
    <ProjectReference Include="../Recordum.Plugins.Contracts/Recordum.Plugins.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**Attenzione a due dettagli:**
- `<OutputType>WinExe</OutputType>` su Linux **non** significa Windows: sopprime la console, ed è ciò che Avalonia usa su tutte le piattaforme.
- Le versioni di `CommunityToolkit.Mvvm` e `Microsoft.Extensions.DependencyInjection` devono **coincidere con quelle di `Recordum.WPF`**. Leggerle da `src/Recordum.WPF/Recordum.WPF.csproj` e ricopiarle, invece di fidarsi dei numeri qui sopra: due versioni diverse dello stesso toolkit nello stesso repo sono un guaio silenzioso (attributi `[ObservableProperty]` generati da un generatore diverso da quello che li compila).

Rimuovere `<ApplicationManifest>` se non si crea `app.manifest` (non serve su Linux).

- [ ] **Step 2: Program, App, MainWindow**

`Program.cs`:

```csharp
using Avalonia;

namespace Recordum.Avalonia;

internal static class Program
{
    // Avalonia vuole che nulla tocchi la UI prima di AppMain.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Chiamato anche dai test headless: DEVE restare public e senza effetti collaterali.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

`App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Recordum.Avalonia.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

`App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Recordum.Avalonia.Views;

namespace Recordum.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
```

`Views/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Recordum.Avalonia.Views.MainWindow"
        Title="Recordum" Width="1000" Height="700">
  <TextBlock Name="Placeholder" Text="Recordum" Margin="24" FontSize="24"/>
</Window>
```

`Views/MainWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Recordum.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 3: Il progetto di test headless**

`tests/Recordum.Avalonia.Tests/Recordum.Avalonia.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Headless.XUnit" Version="12.1.2" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Recordum.Avalonia/Recordum.Avalonia.csproj" />
  </ItemGroup>
</Project>
```

Le versioni di `FluentAssertions`, `xunit` e `Microsoft.NET.Test.Sdk` vanno **lette da `tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj`** e ricopiate, non indovinate.

`tests/Recordum.Avalonia.Tests/SmokeTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Recordum.Avalonia.Views;

namespace Recordum.Avalonia.Tests;

public class SmokeTests
{
    [AvaloniaFact]
    public void The_main_window_opens_with_a_title()
    {
        var window = new MainWindow();
        window.Show();

        window.Title.Should().Be("Recordum");
        window.IsVisible.Should().BeTrue();
    }
}
```

Serve anche il file che dichiara l'app headless a xUnit — `tests/Recordum.Avalonia.Tests/TestAppBuilder.cs`:

```csharp
using Avalonia;
using Avalonia.Headless;
using Recordum.Avalonia;

[assembly: AvaloniaTestApplication(typeof(Recordum.Avalonia.Tests.TestAppBuilder))]

namespace Recordum.Avalonia.Tests;

public static class TestAppBuilder
{
    // Nota: NON UsePlatformDetect() — headless è il "platform".
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
```

- [ ] **Step 4: Aggiungere i due progetti a `Recordum.slnx`**

Aprire `Recordum.slnx`, copiare la forma delle righe esistenti e aggiungere i due percorsi. È XML: se la forma non è ovvia, guardare come sono elencati `Recordum.WPF` e `Recordum.WPF.Tests`.

- [ ] **Step 5: Eseguire il test**

```bash
dotnet build tests/Recordum.Avalonia.Tests --no-incremental
dotnet test tests/Recordum.Avalonia.Tests --no-build
```
Atteso: **1 test verde**. Se il runner si lamenta di un display mancante, il progetto **non** sta usando l'headless: rileggere `TestAppBuilder` (l'errore tipico è `UsePlatformDetect()`).

- [ ] **Step 6: Verificare che la soluzione intera compili ancora**

```bash
dotnet build Recordum.slnx -p:UseAppHost=false -p:EnableWindowsTargeting=true
```
Atteso: `Build succeeded`, 0 errori.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Avalonia tests/Recordum.Avalonia.Tests Recordum.slnx
git commit -m "feat(avalonia): scheletro della shell Linux + primo test headless"
```

---

### Task 2: La sonda del PNG — si può *guardare* una schermata senza l'autore?

Task corto e con un esito binario. La spec lo chiama **sonda, non promessa**: se regge, i task successivi lo usano per verificare a vista; se non regge, si dice e si va avanti senza.

**Files:**
- Create: `tests/Recordum.Avalonia.Tests/ScreenshotProbeTests.cs`
- Create: `tests/Recordum.Avalonia.Tests/Screenshots/` (cartella di output, **gitignorata**)
- Modify: `.gitignore`

**Interfaces:**
- Produces: `ScreenshotProbeTests.Capture(Window window, string name)` — helper `internal static` riusato dai Task 6 e 8 per salvare un frame.

- [ ] **Step 1: Scrivere il test-sonda**

```csharp
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Recordum.Avalonia.Views;

namespace Recordum.Avalonia.Tests;

public class ScreenshotProbeTests
{
    /// <summary>Salva il frame renderizzato della finestra come PNG e ne torna il percorso.
    /// Serve a poter GUARDARE una schermata invece di asserire soltanto sui ViewModel.</summary>
    internal static string Capture(Window window, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}.png");

        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException(
                "CaptureRenderedFrame ha tornato null: il backend headless sta disegnando senza Skia.");
        frame.Save(path);
        return path;
    }

    [AvaloniaFact]
    public void A_rendered_frame_can_be_saved_as_a_png()
    {
        var window = new MainWindow();
        window.Show();

        var path = Capture(window, "probe");

        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG di una finestra 1000x700 non può pesare poche centinaia di byte: se è minuscolo, "
            + "il frame è vuoto e la sonda non ha provato niente");
    }
}
```

- [ ] **Step 2: Eseguirlo e leggere l'esito**

```bash
dotnet build tests/Recordum.Avalonia.Tests --no-incremental
dotnet test tests/Recordum.Avalonia.Tests --no-build --filter FullyQualifiedName~ScreenshotProbe
```

**Se fallisce con `CaptureRenderedFrame` nullo**, il backend headless sta usando il disegno finto. Provare **una volta** ad attivare Skia in `TestAppBuilder`:

```csharp
public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
```

Se anche così non produce un'immagine, **fermarsi**: marcare la sonda come fallita, `Skip` sul test con la ragione scritta nell'attributo, e proseguire col Task 3. Non spendere più di un tentativo: l'informazione "non si può" vale quanto "si può", e il resto del piano non dipende da questo.

- [ ] **Step 3: Ignorare gli output**

In `.gitignore`, accanto alle altre voci di output:
```
tests/Recordum.Avalonia.Tests/**/Screenshots/
```

- [ ] **Step 4: Commit**

```bash
git add tests/Recordum.Avalonia.Tests .gitignore
git commit -m "test(avalonia): sonda screenshot headless — esito: <riuscita|fallita, con la ragione>"
```
Il messaggio di commit **deve dire quale dei due**, perché è il dato che serve a chi legge dopo.

---

### Task 3: Il database cifrato si apre su Linux, senza DPAPI

Il cuore della fetta lato dati: passphrase → chiave → SQLCipher → migrazioni → seed demo.

**Files:**
- Create: `src/Recordum.Avalonia/Bootstrap/SliceServices.cs`, `src/Recordum.Avalonia/Bootstrap/SliceInitializer.cs`
- Create: `src/Recordum.Avalonia/Views/UnlockWindow.axaml` + `.axaml.cs`
- Create: `tests/Recordum.Avalonia.Tests/SliceInitializerTests.cs`
- Modify: `src/Recordum.Avalonia/App.axaml.cs`

**Interfaces:**
- Consumes: `RecordumOptions` — **attenzione**: vive in `src/Recordum.WPF/Bootstrap/RecordumOptions.cs`, quindi va **collegato** come sorgente (`<Compile Include>`), non referenziato. `PassphraseKeyProvider(string passphrase, string saltPath)`, `SqlCipherConnectionFactory.Open(path, key)`, `RecordumDbContext`, `DemoDataSeeder`.
- Produces:
  - `SliceServices.Build(RecordumOptions options, string passphrase) → ServiceProvider`
  - `SliceInitializer.InitializeAsync(IServiceProvider sp, CancellationToken ct) → Task` — migra e semina se il DB è vuoto.

- [ ] **Step 1: Scrivere i test che falliscono**

`tests/Recordum.Avalonia.Tests/SliceInitializerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Avalonia.Bootstrap;
using Recordum.Data;
using Xunit;

namespace Recordum.Avalonia.Tests;

public class SliceInitializerTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "recordum-slice-" + Guid.NewGuid().ToString("N"));

    // NB: questo helper lo riusano anche i Task 5 e 7, quindi vive in un file suo
    // (tests/Recordum.Avalonia.Tests/SliceTestPaths.cs) come `internal static`, non come metodo
    // privato di questa classe. Firma: `static RecordumOptions For(string dir)`.
    private RecordumOptions Options()
    {
        Directory.CreateDirectory(_dir);
        return new RecordumOptions
        {
            DatabasePath = Path.Combine(_dir, "recordum.db"),
            KeyFilePath = Path.Combine(_dir, "db.key"),
            SaltFilePath = Path.Combine(_dir, "db.salt"),
            AttachmentsDirectory = Path.Combine(_dir, "attachments"),
            BackupsDirectory = Path.Combine(_dir, "backups"),
            AutoBackupKeyPath = Path.Combine(_dir, "autobackup.key"),
        };
    }

    [Fact]
    public async Task Initialize_creates_a_migrated_database_seeded_with_demo_habits()
    {
        var options = Options();
        await using var sp = SliceServices.Build(options, "correct horse battery staple");

        await SliceInitializer.InitializeAsync(sp, default);

        var factory = sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        (await db.HabitNodes.CountAsync()).Should().BeGreaterThan(0,
            "il seed demo deve aver popolato l'albero");
        (await db.AppSettings.SingleAsync(s => s.Key == "schema_version")).Value
            .Should().Be("2.18");
    }

    [Fact]
    public async Task The_database_is_really_encrypted_and_a_wrong_passphrase_cannot_read_it()
    {
        var options = Options();
        await using (var sp = SliceServices.Build(options, "la passphrase giusta"))
            await SliceInitializer.InitializeAsync(sp, default);

        await using var wrong = SliceServices.Build(options, "la passphrase sbagliata");
        var factory = wrong.GetRequiredService<IDbContextFactory<RecordumDbContext>>();

        var act = async () =>
        {
            await using var db = await factory.CreateDbContextAsync();
            await db.HabitNodes.CountAsync();
        };

        await act.Should().ThrowAsync<Exception>(
            "aprire un DB SQLCipher con la chiave sbagliata deve fallire alla prima lettura — "
            + "se non fallisce, il file non è cifrato");
    }

    [Fact]
    public async Task Initialize_is_idempotent_and_does_not_reseed()
    {
        var options = Options();
        await using var sp = SliceServices.Build(options, "passphrase");
        await SliceInitializer.InitializeAsync(sp, default);
        var factory = sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
        int after;
        await using (var db = await factory.CreateDbContextAsync())
            after = await db.HabitNodes.CountAsync();

        await SliceInitializer.InitializeAsync(sp, default);

        await using var db2 = await factory.CreateDbContextAsync();
        (await db2.HabitNodes.CountAsync()).Should().Be(after);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* file ancora aperto: è un temp, lo pulisce l'OS */ }
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

```bash
dotnet build tests/Recordum.Avalonia.Tests --no-incremental
dotnet test tests/Recordum.Avalonia.Tests --no-build --filter FullyQualifiedName~SliceInitializer
```
Atteso: FAIL in compilazione (`SliceServices` non esiste).

- [ ] **Step 3: Collegare `RecordumOptions` e scrivere il composition root**

Nel `.csproj` di `Recordum.Avalonia`, prima sezione di file collegati:

```xml
<ItemGroup Label="Sorgenti condivise con la shell WPF (impalcatura: spec §5)">
  <Compile Include="../Recordum.WPF/Bootstrap/RecordumOptions.cs" Link="Shared/RecordumOptions.cs" />
</ItemGroup>
```

`src/Recordum.Avalonia/Bootstrap/SliceServices.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Data;
using Recordum.Data.Interceptors;
using Recordum.Data.Security;
using Recordum.Data.Services;
using Recordum.WPF.Bootstrap;   // RecordumOptions, collegato come sorgente

namespace Recordum.Avalonia.Bootstrap;

/// <summary>
/// Composition root della fetta: registra ciò che serve alla Home e nient'altro. Non è il gemello
/// di ServiceRegistrar e non deve diventarlo — quello registra plugin, chart engine, export,
/// WebView2 e scheduler di backup, che qui sono fuori scope (e ChartEngine è net10.0-windows,
/// quindi su Linux non si può nemmeno referenziare).
/// </summary>
public static class SliceServices
{
    public static ServiceProvider Build(RecordumOptions options, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton(options);

        // Niente DPAPI su Linux: la chiave viene dalla passphrase (Argon2id).
        services.AddSingleton<IDbKeyProvider>(_ => new PassphraseKeyProvider(passphrase, options.SaltFilePath));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<HabitConfigHistoryInterceptor>();
        services.AddSingleton<TimestampInterceptor>();
        services.AddSingleton<SqlCipherKeyConnectionInterceptor>();

        services.AddDbContextFactory<RecordumDbContext>((sp, builder) =>
        {
            builder.UseSqlite(options.ConnectionString);
            builder.AddInterceptors(
                sp.GetRequiredService<SqlCipherKeyConnectionInterceptor>(),
                sp.GetRequiredService<TimestampInterceptor>(),
                sp.GetRequiredService<HabitConfigHistoryInterceptor>());
        });

        return services.BuildServiceProvider();
    }
}
```

**Da verificare leggendo `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` invece di fidarsi di questo blocco**: i nomi esatti degli interceptor, l'ordine in cui vengono aggiunti (l'ordine di registrazione = ordine di invocazione in EF Core), il tipo concreto di `IClock` e come `IPluginContext` viene fornito al `TimestampInterceptor`. Ricopiare da lì; qui sopra c'è la forma, non necessariamente la firma.

I servizi di dominio (`IHabitNodeService`, `IHabitTreeQueryService`, `IPurgeService`, `ICompletionService`, `IResetTimeResolver`, `IStreakAggregator`, `IHabitUiPrefsService`, `IEventBus`) si registrano allo stesso modo: **copiare le righe corrispondenti da `ServiceRegistrar.cs`**, prendendo solo quelle che la Home usa. Se un servizio ne pretende un altro, il contenitore lo dirà al primo `GetRequiredService`.

- [ ] **Step 4: L'initializer**

`src/Recordum.Avalonia/Bootstrap/SliceInitializer.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Data;
using Recordum.Data.Security;
using Recordum.WPF.Bootstrap;

namespace Recordum.Avalonia.Bootstrap;

/// <summary>
/// Avvio della fetta: sblocca il file, applica le migrazioni, semina la demo se il DB è vuoto.
/// Deliberatamente NON è ApplicationInitializer, che pretende PluginManager, ChartEngine,
/// ExportEngine, WebView2 e BackupSchedulerHost — tutta roba fuori dalla fetta, e ChartEngine
/// non è nemmeno compilabile qui. Unificarli è un lavoro successivo, non di questa fetta.
/// </summary>
public static class SliceInitializer
{
    public static async Task InitializeAsync(IServiceProvider sp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sp);
        var options = sp.GetRequiredService<RecordumOptions>();

        // Sblocca il file prima delle migrazioni: PRAGMA key è applicata per-connessione
        // dall'interceptor, ma questa apertura primer fa fallire SUBITO una chiave sbagliata.
        using (var key = await sp.GetRequiredService<IDbKeyProvider>().GetKeyAsync(ct).ConfigureAwait(false))
        using (SqlCipherConnectionFactory.Open(options.DatabasePath, key))
        {
        }

        var factory = sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Database.MigrateAsync(ct).ConfigureAwait(false);

        if (!await db.HabitNodes.AnyAsync(ct).ConfigureAwait(false))
            await SeedDemoAsync(factory, ct).ConfigureAwait(false);
    }

    /// <summary>Tre nodi scritti a mano, non DemoDataSeeder: quello vive in src/Recordum.WPF/Bootstrap
    /// e pretende una decina di servizi (tag, goal, metriche, note, diario, template, albero), cioè
    /// trascinerebbe dentro mezza applicazione per darci un albero da guardare. Alla fetta serve
    /// l'albero, non il catalogo.</summary>
    private static async Task SeedDemoAsync(IDbContextFactory<RecordumDbContext> factory, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var gruppo = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Group, Name = "Salute", Color = "#4dbd91" };
        db.HabitNodes.AddRange(
            gruppo,
            new HabitNode
            {
                Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action,
                Name = "Camminata", Color = "#f5a623", ParentId = gruppo.Id,
            },
            new HabitNode
            {
                Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter,
                Name = "Bicchieri d'acqua", Color = "#5bc8f5", ParentId = gruppo.Id,
            });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
```

I `CreatedAt`/`UpdatedAt` li scrive il `TimestampInterceptor` dall'`IClock`: non assegnarli a mano.
Se più avanti servisse un albero ricco (per i grafici, o per il pass visivo), **allora** si valuta di
collegare `DemoDataSeeder` — ma è un task suo, non una riga di questo.

- [ ] **Step 5: La finestra di sblocco**

`Views/UnlockWindow.axaml` — una `TextBox` con `PasswordChar="•"`, un pulsante, e un messaggio d'errore. Espone:

```csharp
public string? Passphrase { get; private set; }   // null = l'utente ha annullato
```

Il `MainWindow` non si apre finché `Passphrase` è null. Le due stringhe utente ("Passphrase", "Sblocca") vanno in `.resx` come da convenzione — **oppure**, se collegare le `.resx` risulta più lungo del previsto (v. Task 5 Step 2), lasciarle inline in questo task e aprire un promemoria nel commit: sono due, e la fetta non è ciò che si spedisce.

- [ ] **Step 6: Eseguire i test**

```bash
dotnet build tests/Recordum.Avalonia.Tests --no-incremental
dotnet test tests/Recordum.Avalonia.Tests --no-build
```
Atteso: i tre test dell'initializer verdi, più quelli dei task precedenti.

- [ ] **Step 7: Provare il test contro il codice rotto**

Nel `SliceServices`, sostituire il `PassphraseKeyProvider` con una chiave costante uguale per tutti:
il test `The_database_is_really_encrypted_and_a_wrong_passphrase_cannot_read_it` **deve fallire**.
Se resta verde, sta provando qualcos'altro (tipicamente: il DB non viene mai aperto davvero). Ripristinare.

- [ ] **Step 8: Commit**

```bash
git add src/Recordum.Avalonia tests/Recordum.Avalonia.Tests
git commit -m "feat(avalonia): DB cifrato, migrazioni e seed su Linux senza DPAPI"
```

---

### Task 4: Le due cuciture in WPF — `ITickSource` e `DropPlan`

Gli unici file di `src/Recordum.WPF/` che questo piano tocca. Entrambe le modifiche **migliorano** WPF di per sé, il che è il criterio per accettarle.

**Files:**
- Create: `src/Recordum.Core/Abstractions/ITickSource.cs`
- Create: `src/Recordum.WPF/Services/WpfTickSource.cs`
- Create: `src/Recordum.WPF/ViewModels/DropPlan.cs`
- Create: `src/Recordum.Avalonia/Bootstrap/AvaloniaTickSource.cs`
- Modify: `src/Recordum.WPF/ViewModels/Home/HomeClockTicker.cs`, `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs`, `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/Home/HomeClockTickerTests.cs` (nuovo o esistente — cercarlo prima)

**Interfaces:**
- Produces:
  ```csharp
  public interface ITickSource : IDisposable
  {
      void Start(TimeSpan interval, Action onTick);
      void Stop();
  }
  ```
  `HomeClockTicker(ITickSource? ticks = null)` — il parametro è opzionale e default `null` (nessun battito), così i ~90 test WPF che lo costruiscono senza argomenti restano veri.
- Produces: `Recordum.WPF.ViewModels.DropPlan` (record struct spostato, stessa forma).

- [ ] **Step 1: Scrivere il test che fallisce**

```csharp
[Fact]
public void The_ticker_fans_out_to_every_callback_on_each_tick()
{
    var ticks = new FakeTickSource();
    var sut = new HomeClockTicker(ticks);
    var a = 0; var b = 0;
    sut.Register(() => a++);
    sut.Register(() => b++);

    ticks.Fire();
    ticks.Fire();

    a.Should().Be(2);
    b.Should().Be(2);
}

[Fact]
public void Clear_stops_the_tick_source()
{
    var ticks = new FakeTickSource();
    var sut = new HomeClockTicker(ticks);
    sut.Register(() => { });

    sut.Clear();

    ticks.IsRunning.Should().BeFalse();
}
```

con, in `tests/Recordum.WPF.Tests/Fixtures/FakeTickSource.cs`:

```csharp
using Recordum.Core.Abstractions;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>Battito controllato dal test: rende osservabile ciò che il DispatcherTimer nascondeva
/// (i test headless non lo facevano nemmeno partire, perché Application.Current è null).</summary>
public sealed class FakeTickSource : ITickSource
{
    private Action? _onTick;
    public bool IsRunning { get; private set; }

    public void Start(TimeSpan interval, Action onTick) { _onTick = onTick; IsRunning = true; }
    public void Stop() => IsRunning = false;
    public void Fire() { if (IsRunning) _onTick?.Invoke(); }
    public void Dispose() => Stop();
}
```

- [ ] **Step 2: Eseguirlo — e sapere che qui NON si può**

`dotnet test tests/Recordum.WPF.Tests` **non gira su Linux** (serve `Microsoft.WindowsDesktop.App`). Su questa macchina ci si ferma a:

```bash
dotnet build tests/Recordum.WPF.Tests -p:UseAppHost=false -p:EnableWindowsTargeting=true --no-incremental
```
Atteso: errore di compilazione (`HomeClockTicker` non accetta un argomento). È il rosso disponibile qui; l'esecuzione vera resta alla VM e va **scritta nel commit**.

- [ ] **Step 3: L'astrazione e le due implementazioni**

`src/Recordum.Core/Abstractions/ITickSource.cs`:

```csharp
namespace Recordum.Core.Abstractions;

/// <summary>Un battito periodico sul thread dell'interfaccia. Esiste perché il ticker della Home
/// non deve conoscere il toolkit: WPF e Avalonia hanno entrambi un DispatcherTimer, con lo stesso
/// nome e namespace diversi, e i test ne vogliono uno finto.</summary>
public interface ITickSource : IDisposable
{
    void Start(TimeSpan interval, Action onTick);
    void Stop();
}
```

`src/Recordum.WPF/Services/WpfTickSource.cs` e `src/Recordum.Avalonia/Bootstrap/AvaloniaTickSource.cs`: identici salvo il `using` (`System.Windows.Threading` / `Avalonia.Threading`). Entrambi creano il timer alla prima `Start`, agganciano `Tick`, e in `Dispose` sganciano l'handler — **il timer del dispatcher è radicato dal dispatcher: senza lo sgancio, continua a battere e perde memoria** (è la ragione per cui `HomeClockTicker` documenta l'obbligo di `Dispose`).

`HomeClockTicker` diventa:

```csharp
public sealed class HomeClockTicker(ITickSource? ticks = null) : IDisposable
{
    private readonly List<Action> _callbacks = new();

    public void Register(Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        _callbacks.Add(tick);
        ticks?.Start(TimeSpan.FromSeconds(1), TickNow);
    }

    public void Clear() { _callbacks.Clear(); ticks?.Stop(); }

    public void TickNow() { foreach (var cb in _callbacks.ToArray()) cb(); }

    public void Dispose() { Clear(); ticks?.Dispose(); }
}
```

Registrare in `ServiceRegistrar.cs`: `services.AddTransient<ITickSource, WpfTickSource>();` — e **verificare come `HomeClockTicker` viene costruito oggi** (`new` diretto dentro `HomeViewModel`, o dal contenitore?): se è `new` diretto, la sorgente del battito va passata da chi costruisce il `HomeViewModel`, non iniettata nel ticker.

- [ ] **Step 4: Spostare `DropPlan`**

Da `TreeDragDropBehavior.cs:21` a un file suo, `src/Recordum.WPF/ViewModels/DropPlan.cs`:

```csharp
namespace Recordum.WPF.ViewModels;

/// <summary>Dove finisce una riga trascinata: genitore, ancora, e se va prima o dopo l'ancora.
/// È dato puro — sta con i ViewModel che la consumano, non dentro il behavior WPF che la produce,
/// così HomeViewModel non dipende da System.Windows per conoscere la forma di un drop.</summary>
public readonly record struct DropPlan(Guid? Parent, Guid? Anchor, bool PlaceAfter);
```

In `TreeDragDropBehavior.cs`: togliere la dichiarazione, aggiungere `using Recordum.WPF.ViewModels;`. In `HomeViewModel.cs`: togliere `using Recordum.WPF.Behaviors;` **solo se** non serve ad altro (verificare: `ApplyDropAsync` potrebbe usare anche `DropZone`; se sì, spostare anche quello con la stessa motivazione, o lasciare il using e dirlo nel commit).

- [ ] **Step 5: Verificare che WPF compili ancora**

```bash
dotnet build Recordum.slnx -p:UseAppHost=false -p:EnableWindowsTargeting=true --no-incremental
```
Atteso: `Build succeeded`. **Contare le righe toccate** in `src/Recordum.WPF/ViewModels/` — serve al Task 9:
```bash
git diff --stat -- src/Recordum.WPF/ViewModels/
```

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Core/Abstractions/ITickSource.cs src/Recordum.WPF src/Recordum.Avalonia tests/Recordum.WPF.Tests
git commit -m "refactor: ITickSource e DropPlan — le due cuciture fra le shell

I test WPF nuovi COMPILANO ma non sono stati eseguiti (Linux non ha
Microsoft.WindowsDesktop.App): da lanciare sulla VM."
```

---

### Task 5: I ViewModel della Home compilano dentro Avalonia

Il momento della verità della fetta: gli stessi file `.cs`, un secondo assembly.

**Files:**
- Modify: `src/Recordum.Avalonia/Recordum.Avalonia.csproj` (elenco dei `<Compile Include>` + le `.resx`)
- Test: `tests/Recordum.Avalonia.Tests/HomeViewModelReuseTests.cs`

**Interfaces:**
- Consumes: `HomeViewModel(IHabitTreeQueryService, IHabitNodeService, IPurgeService, IHabitTemplatePickerLauncher? = null, QuickActionHandlerSelector? = null, IEventBus? = null, IHabitUiPrefsService? = null, IStreakAggregator? = null, IClock? = null, IResetTimeResolver? = null)`; `HomeRowViewModel(HomeTreeRow row, int indentLevel, IHabitNodeService nodes, IPurgeService purge, HabitUiPrefs? uiPrefs = null, IStreakAggregator? streaks = null, IClock? clock = null)`.
- Produces: nessuna API nuova. Il deliverable è **che compili**, più un test che costruisce il VM vero.

- [ ] **Step 1: Scrivere il test che fallisce**

```csharp
[Fact]
public async Task The_real_HomeViewModel_lists_the_habits_of_a_seeded_database()
{
    var options = SliceTestPaths.For(_dir);        // helper condiviso, definito nel Task 3
    await using var sp = SliceServices.Build(options, "passphrase");
    await SliceInitializer.InitializeAsync(sp, default);

    var sut = new HomeViewModel(
        sp.GetRequiredService<IHabitTreeQueryService>(),
        sp.GetRequiredService<IHabitNodeService>(),
        sp.GetRequiredService<IPurgeService>());

    await sut.LoadCommand.ExecuteAsync(null);

    sut.Rows.Should().NotBeEmpty();
    sut.State.Should().Be(HomeState.Loaded);
}
```

**Nomi verificati sul codice il 2026-09-09** (non indovinarli di nuovo): `LoadAsync` è **privato** — è un
`[RelayCommand]`, quindi dall'esterno si invoca `LoadCommand.ExecuteAsync(null)`. L'enum è
`HomeState { Loading, Loaded, Empty, Error }`: **non esiste `Ready`**. Un albero seminato con tre nodi
finisce in `Loaded`.

- [ ] **Step 2: Collegare i file, uno per uno**

Nel `.csproj`, estendendo la sezione del Task 3:

```xml
<ItemGroup Label="Sorgenti condivise con la shell WPF (impalcatura: spec §5)">
  <Compile Include="../Recordum.WPF/Bootstrap/RecordumOptions.cs"          Link="Shared/RecordumOptions.cs" />
  <Compile Include="../Recordum.WPF/Resources/Strings.Designer.cs"          Link="Shared/Strings.Designer.cs" />
  <Compile Include="../Recordum.WPF/Formatting/**/*.cs"                     Link="Shared/Formatting/%(RecursiveDir)%(Filename)%(Extension)" />
  <Compile Include="../Recordum.WPF/ViewModels/DropPlan.cs"                 Link="Shared/ViewModels/DropPlan.cs" />
  <Compile Include="../Recordum.WPF/ViewModels/IRowSelectionHost.cs"        Link="Shared/ViewModels/IRowSelectionHost.cs" />
  <Compile Include="../Recordum.WPF/ViewModels/HomeViewModel.cs"            Link="Shared/ViewModels/HomeViewModel.cs" />
  <Compile Include="../Recordum.WPF/ViewModels/HomeRowViewModel.cs"         Link="Shared/ViewModels/HomeRowViewModel.cs" />
  <Compile Include="../Recordum.WPF/ViewModels/Home/HomeClockTicker.cs"     Link="Shared/ViewModels/Home/HomeClockTicker.cs" />
  <Compile Include="../Recordum.WPF/ViewModels/Quick/**/*.cs"               Link="Shared/ViewModels/Quick/%(RecursiveDir)%(Filename)%(Extension)" />
  <Compile Include="../Recordum.WPF/ViewModels/HabitEditor/IHabitTemplatePickerLauncher.cs" Link="Shared/ViewModels/HabitEditor/IHabitTemplatePickerLauncher.cs" />
</ItemGroup>
```

**Il compilatore guida da qui in avanti**: si aggiunge un file, si ricompila, si legge il primo errore, si aggiunge il file che manca. **Se un errore riguarda un tipo WPF** (`System.Windows.*`), *fermarsi e scriverlo nel commit*: è precisamente il dato che questa fetta esiste per raccogliere, e va nel conteggio finale del Task 9 — non aggirato con un `#if`.

- [ ] **Step 3: Le risorse `.resx` — la trappola**

`Strings.Designer.cs` costruisce il suo `ResourceManager` così:

```csharp
new ResourceManager("Recordum.WPF.Resources.Strings", typeof(Strings).Assembly)
```

Il nome è **letterale** e l'assembly è **quello che compila il file**. Dentro `Recordum.Avalonia` l'assembly è un altro, quindi la risorsa va incorporata con esattamente quel nome logico o ogni stringa esplode a runtime con `MissingManifestResourceException` (e **non** a compilazione: i test che non leggono stringhe passeranno lo stesso).

```xml
<ItemGroup>
  <EmbeddedResource Include="../Recordum.WPF/Resources/Strings.resx"
                    LogicalName="Recordum.WPF.Resources.Strings.resources" />
  <EmbeddedResource Include="../Recordum.WPF/Resources/Strings.it.resx"
                    LogicalName="Recordum.WPF.Resources.Strings.it.resources" />
</ItemGroup>
```

Aggiungere **un test che legge una stringa vera**, o la trappola resta armata:

```csharp
[Fact]
public void The_shared_resx_resolves_inside_this_assembly()
{
    Recordum.WPF.Resources.Strings.Home_Empty_Active.Should().NotBeNullOrWhiteSpace();
}
```
(usare una chiave che esiste davvero: cercarla in `Strings.Designer.cs`.)

Se la satellite italiana non si genera dal `LogicalName`, **non insistere**: la fetta è in inglese, si annota e si va avanti.

- [ ] **Step 4: Eseguire i test**

```bash
dotnet build tests/Recordum.Avalonia.Tests --no-incremental
dotnet test tests/Recordum.Avalonia.Tests --no-build
```
Atteso: tutti verdi, incluso il nuovo test sul `HomeViewModel` vero.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Avalonia tests/Recordum.Avalonia.Tests
git commit -m "feat(avalonia): i ViewModel della Home compilano e caricano dentro la shell Linux"
```
Il messaggio deve dire **quanti file è stato necessario collegare** e se qualcuno ha richiesto modifiche.

---

### Task 6: La Home a schermo

**Files:**
- Create: `src/Recordum.Avalonia/Views/HomeView.axaml` + `.axaml.cs`
- Modify: `src/Recordum.Avalonia/Views/MainWindow.axaml` (ospita la HomeView), `App.axaml.cs` (costruisce il VM dal contenitore)
- Test: `tests/Recordum.Avalonia.Tests/HomeViewTests.cs`

**Interfaces:**
- Consumes: `HomeViewModel.Rows` (`ObservableCollection<HomeRowViewModel>`). Proprietà di riga **verificate sul codice il 2026-09-09**: `Name` (`[ObservableProperty]`), `ColorHex` (`string`), `IndentLevel` (`int`), `HabitNodeId` (`Guid`), `HabitType` (`HabitType?`, null = gruppo), `IsGroup`, `IsCompletedToday`.
- Produces: `HomeView` (`UserControl`) con `DataContext` di tipo `HomeViewModel`.

- [ ] **Step 1: Scrivere il test che fallisce**

```csharp
[AvaloniaFact]
public async Task The_home_renders_one_visual_row_per_habit()
{
    var (window, vm) = await ShowHomeAsync();     // helper: costruisce DB+VM+finestra

    var rows = window.GetVisualDescendants().OfType<ItemsControl>()
        .First(c => c.Name == "RowsHost");

    rows.ItemCount.Should().Be(vm.Rows.Count);
    vm.Rows.Should().NotBeEmpty("altrimenti il test passerebbe con zero righe da entrambe le parti");
}
```

L'ultima riga è la parte che conta: **un confronto fra due zeri è verde e non prova niente** — la stessa trappola di W24-R2/R3 registrata in CLAUDE.md §8.

- [ ] **Step 2: Eseguire e vedere il rosso**

```bash
dotnet build tests/Recordum.Avalonia.Tests --no-incremental
dotnet test tests/Recordum.Avalonia.Tests --no-build --filter FullyQualifiedName~HomeView
```

- [ ] **Step 3: Scrivere la view**

Un `ItemsControl x:Name="RowsHost"` su `Rows`, con un `DataTemplate` che rende: barra d'accento a sinistra 4px del colore della riga, nome, rientro proporzionale a `IndentLevel`. Niente drag-drop, niente menù contestuale, niente strisce.

**Sul font**: il brief chiede "Inter o system stack", l'app WPF usa Palatino, e su Linux quel font di norma non esiste. Non inseguirlo: lasciare il default di sistema e annotare la resa nel commit — la scelta di cosa spedire è dell'autore (spec §11.1).

I quattro vincoli del linguaggio visivo che valgono anche qui (design brief): **il colore è un identificatore, non decorazione**; niente emoji nella chrome; motion assente; tipografia generosa.

- [ ] **Step 4: Verde, e uno sguardo**

```bash
dotnet test tests/Recordum.Avalonia.Tests --no-build
```
**Se la sonda del Task 2 è riuscita**, aggiungere in coda al test `ScreenshotProbeTests.Capture(window, "home")` e **aprire il PNG**: è la prima volta che si può guardare una schermata di Recordum senza chiederla all'autore. Se qualcosa è visibilmente storto, si corregge adesso.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Avalonia tests/Recordum.Avalonia.Tests
git commit -m "feat(avalonia): la Home mostra l'albero delle abitudini"
```

---

### Task 7: Il tap che registra, e che sopravvive al riavvio

**Files:**
- Modify: `src/Recordum.Avalonia/Views/HomeView.axaml` (il pulsante di completamento sulla riga)
- Test: `tests/Recordum.Avalonia.Tests/QuickActionTests.cs`

**Interfaces:**
- Consumes: `HomeRowViewModel.QuickActionCommand` (generato da `[RelayCommand] private async Task QuickActionAsync(CancellationToken)`), `HomeRowViewModel.HabitNodeId`, `HomeRowViewModel.HabitType`.
- **Vincolo verificato sul codice, non ovvio**: il comando ha `CanExecute = nameof(CanQuickAction)`, e `CanQuickAction` è `!IsGroup && HabitType is not null && _quickActionSelector is not null`. Quindi **il `QuickActionHandlerSelector` va passato a `HomeViewModel`** (quarto/quinto parametro opzionale) o il comando non è mai eseguibile e il test fallisce senza dire perché. Registrarlo in `SliceServices` e passarlo dove il Task 5 costruiva il VM con tre soli argomenti.

- [ ] **Step 1: Scrivere il test che fallisce**

```csharp
[AvaloniaFact]
public async Task Tapping_an_action_habit_records_a_completion_that_outlives_the_process()
{
    var options = SliceTestPaths.For(_dir);
    Guid habitId;

    await using (var sp = SliceServices.Build(options, "passphrase"))
    {
        await SliceInitializer.InitializeAsync(sp, default);
        var vm = await BuildHomeAsync(sp);
        var row = vm.Rows.First(r => r.HabitType == HabitType.Action);
        habitId = row.HabitNodeId;

        await row.QuickActionCommand.ExecuteAsync(null);
    }

    // Nuovo contenitore = nuova connessione: prova che il dato è sul disco, non in memoria.
    await using var reopened = SliceServices.Build(options, "passphrase");
    var factory = reopened.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    (await db.Completions.CountAsync(c => c.HabitNodeId == habitId)).Should().Be(1);
}
```

- [ ] **Step 2: Eseguire e vedere il rosso**

- [ ] **Step 3: Collegare il pulsante nella view e i servizi mancanti nel contenitore**

Se `QuickActionHandlerSelector` pretende servizi non ancora registrati, aggiungerli in `SliceServices` copiando le righe da `ServiceRegistrar.cs`.

- [ ] **Step 4: Verde**

```bash
dotnet build tests/Recordum.Avalonia.Tests --no-incremental && dotnet test tests/Recordum.Avalonia.Tests --no-build
```

- [ ] **Step 5: Provare il test contro il codice rotto**

Commentare la riga che invoca il comando: il test **deve** fallire su `Be(1)`. Se resta verde, sta contando una completion seminata dalla demo — filtrare per un'abitudine che il seed lascia vuota, o azzerare prima. Ripristinare.

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(avalonia): il tap registra un completamento che sopravvive al riavvio"
```

---

### Task 8: I token del tema, in chiaro e in scuro

**Files:**
- Create: `src/Recordum.Avalonia/Themes/Tokens.axaml`
- Modify: `App.axaml` (include i token), `Views/HomeView.axaml` (usa i token invece dei colori letterali)
- Test: `tests/Recordum.Avalonia.Tests/ThemeTests.cs`

**Interfaces:**
- Produces: risorse `Brush.Background`, `Brush.Text`, `Brush.TextMuted`, `Brush.Accent`, `Brush.Border`, e `Radius.Card` — **stessi nomi di `src/Recordum.WPF/Themes/Tokens.xaml`**, così un domani si confrontano a occhio invece che a memoria. Leggere quel file e ricopiarne i valori esatti.

- [ ] **Step 1: Il test**

```csharp
[AvaloniaTheory]
[InlineData("Light")]
[InlineData("Dark")]
public void Every_token_resolves_in_both_variants(string variant)
{
    var app = Application.Current!;
    app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

    foreach (var key in new[] { "Brush.Background", "Brush.Text", "Brush.TextMuted", "Brush.Accent", "Brush.Border" })
        app.TryFindResource(key, app.ActualThemeVariant, out var value).Should()
            .BeTrue($"il token {key} deve esistere in tema {variant}");
}
```
(verificare la firma esatta di `TryFindResource` in Avalonia 12: se differisce, adattare **il test**, non l'intento.)

- [ ] **Step 2: Rosso, poi i token, poi verde**

- [ ] **Step 3: Se la sonda del Task 2 è riuscita**, catturare `home-light.png` e `home-dark.png` e guardarli entrambi. Il criterio non è "compila": è **sembra Recordum**.

- [ ] **Step 4: Commit**

```bash
git commit -am "feat(avalonia): token del tema in chiaro e scuro"
```

---

### Task 9: Il conto, e cosa abbiamo imparato

La fetta esiste per produrre un numero e un giudizio. Questo task li scrive dove non si perdono.

**Files:**
- Modify: `docs/phase-4-issues.md`, `CLAUDE.md`, `docs/dev-environment-sync.md`

- [ ] **Step 1: Contare**

```bash
BASE=$(git merge-base HEAD r9-day-boundary)
git diff --stat $BASE..HEAD -- src/Recordum.WPF/ViewModels/
git diff --stat $BASE..HEAD -- src/Recordum.WPF/
```
Il primo numero è **il criterio di successo dichiarato dalla spec**. Preventivo: un file.

- [ ] **Step 2: Eseguire tutte le suite eseguibili**

```bash
for p in Core Data Plugins Plugins.ExportEngine Avalonia; do
  dotnet build tests/Recordum.$p.Tests -p:UseAppHost=false -p:EnableWindowsTargeting=true --no-incremental
  dotnet test  tests/Recordum.$p.Tests -p:UseAppHost=false -p:EnableWindowsTargeting=true --no-build --nologo
done
```
Riferimento: Core 145 / Data 478 (+18 DPAPI attesi) / Plugins 66 / ExportEngine 37 / Avalonia = il totale nuovo.

- [ ] **Step 3: Scrivere l'esito**

Nel tracker, una sezione `## Fetta Avalonia (data)` con: il numero di righe di ViewModel toccate, i file che hanno richiesto una modifica **e perché**, l'esito della sonda screenshot, e le cose scoperte che valgono per il porting completo. In `CLAUDE.md`, un paragrafo di progresso e — se la fetta è riuscita — una riga in §3 (Stack) e §4 (Architettura) che dice che esiste una seconda shell e a che stadio è. In `docs/dev-environment-sync.md`, i comandi per costruire ed eseguire l'app Avalonia su Linux.

- [ ] **Step 4: Il giudizio, esplicito**

Nel commit di chiusura, rispondere alla domanda che la fetta doveva rispondere: **il porting completo è meccanico, oppure no?** Con il numero accanto. Se la risposta è "no", elencare i tre ostacoli più grossi: è quello il materiale per la spec successiva.

- [ ] **Step 5: Commit**

```bash
git add docs CLAUDE.md
git commit -m "docs: esito della fetta Avalonia — <N> righe di ViewModel toccate, <giudizio>"
```

---

## Dopo l'esecuzione

1. **L'autore apre l'app** e dice se sembra Recordum. Nessun PNG lo sostituisce.
2. **Review di branch** sull'intero diff (`superpowers:requesting-code-review`), non per-task: su questo repo le review di branch hanno trovato quello che le review per-task non potevano vedere — da ultimo i due Critical di R9 (2026-09-09).
3. **Decisione dell'autore**: Avalonia diventa la shell ufficiale, o resta un esperimento? Da qui dipende se il passo successivo è l'estrazione di `Recordum.Presentation` (i 126 file, un refactor meccanico da fare **dopo** il pass visivo dell'Onda 2) oppure niente.
4. **Non si mergia in `main`** finché la v1.0 WPF non ha fatto il suo pass visivo: questo branch è una linea parallela, e mescolarlo con la linea che deve diventare v1.0 rende impossibile capire quale delle due ha rotto cosa.
