# Onda F0 — Le fondamenta della piattaforma: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Estrarre i 126 file di ViewModel in un progetto `Recordum.Presentation` senza UI — il che libera 178 file di test dalla dipendenza da Windows e li fa girare su Linux — dopo aver tagliato le sette cuciture che oggi legano i ViewModel a WPF.

**Architecture:** `Recordum.Presentation` è `net10.0` **senza** `-windows`, quindi il confine dell'estrazione lo impone il compilatore: ciò che tocca `System.Windows` non può entrarci. Le sette cuciture si tagliano **prima** della traslocazione, una alla volta, mentre il codice è ancora in `Recordum.WPF` e quindi compilabile in entrambi i modi. Ogni cucitura sostituisce un accesso diretto a un tipo WPF con un'astrazione in `Recordum.Core`, che ciascuna shell implementa a modo suo. La traslocazione vera è poi una `git mv` più una sostituzione di namespace, verificata da una build.

**Tech Stack:** C# / .NET 10, `net10.0` puro per Presentation; Avalonia 12.1.2 per `Recordum.UI`; xunit 2.9.3 (Presentation) e xunit.v3 (UI, per `Avalonia.Headless.XUnit`); `CommunityToolkit.Mvvm` 8.4.

**Spec:** [`docs/superpowers/specs/2026-09-10-avalonia-as-the-platform-design.md`](../specs/2026-09-10-avalonia-as-the-platform-design.md) — questo piano implementa la sola onda **`F0`** di §6. Le onde `F1`–`F8` avranno ciascuna il proprio piano, scritto dalla stessa spec a ridosso dell'esecuzione (§17).

## Global Constraints

- **Prerequisito, e blocca solo il Task 7** (spec §14, riformulato il 2026-09-10): **una esecuzione** della suite WPF di R9 sulla VM, verde. Non serve che R9 sia mergiato in `main`, né che abbia avuto la sua verifica a schermo — quelle sono igiene del repo e si fanno quando conviene. La ragione del vincolo è che il **Task 7 rinomina 126 file**, e far atterrare eventuali correzioni di R9 attraverso una rinomina di quella taglia è l'unico scenario davvero brutto dell'onda. **I Task 1-6 non sono bloccati**: sono verificati dal compilatore, e la suite WPF non è eseguibile su Linux comunque. Il comando pronto sta in `docs/dev-environment-sync.md`, sezione *"Handoff per la VM — onda F0"*.
- **Branch**: si lavora su `avalonia-linux-slice`. **Mai commit su `main`**: l'hook `guard-main.js` legge il branch *prima* di eseguire, quindi un comando composto che parte da `main` viene negato per intero anche se contiene un `git checkout -b`. Cambiare branch in una chiamata separata.
- **Ambiente Linux, SDK userland**: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"` all'inizio di ogni sessione di shell.
- **La soluzione è `Recordum.slnx`**, non `.sln`.
- **Build**: `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true`. **MAI passare `-p:UseAppHost=false`** da riga di comando: è già il default in `Directory.Build.props` come property di progetto, e da CLI diventa una global property che vince sempre e rompe la build di `tests/Recordum.UI.Tests` (xunit.v3 pretende l'apphost nativo, errore `xUnit.net v3 test projects must build an app host`).
- **La suite WPF NON è eseguibile su Linux** (serve `Microsoft.WindowsDesktop.App`). `Recordum.WPF` e `Recordum.WPF.Tests` si **compilano** soltanto: una modifica ai loro file è verificata dal compilatore, mai da un test, finché non trasloca.
- **`TreatWarningsAsErrors=true`** è globale (`Directory.Build.props`): qualunque warning nuovo rompe la build.
- **Baseline da cui si parte** (misurata il 2026-09-10): build 0 errori / 39 warning; `Recordum.Core.Tests` **145**, `Recordum.Data.Tests` **478** (+18 DPAPI, Windows-only per natura), `Recordum.Plugins.Tests` **66**, `Recordum.Plugins.ExportEngine.Tests` **37**, `Recordum.Avalonia.Tests` **12**.
- **Stringhe utente** in `.resx`, English-first. Una stringa nuova = 3 edit: `Strings.resx` + `Strings.it.resx` + accessor a mano in `Strings.Designer.cs`. **`Strings.Designer.cs` va editato via shell** (`python`/`sed`/heredoc): `guard-generated.js` blocca Edit/Write su qualunque `*.Designer.cs`. Questo piano non aggiunge stringhe nuove.
- **Mai `DateTime.UtcNow`/`Now`**: il tempo passa da `IClock`. **Mai derivare un giorno da un istante** fuori da `DayBoundary`: `DayDerivationGuardTests` fallisce.
- **Un `grep` per `new NomeClasse(` non trova tutti i siti di costruzione.** C# permette il `new` *target-typed*: `ChartBuilderViewModelTests.cs:21` scrive `new(charts, tree, metrics, …)` dentro un helper il cui tipo di ritorno dà il tipo, e nessuna ricerca per nome lo vede. Nel Task 4 questo ha nascosto **due** siti su undici, trovati solo dal compilatore. Conseguenza operativa per ogni task che cambia una firma: **l'elenco autorevole dei siti è l'output del compilatore, mai un `grep`** — il grep serve a orientarsi, non a chiudere.
- **`tests/Recordum.UI.Tests` è l'unico progetto di test SENZA `<Using Include="Xunit" />`** nel proprio `.csproj`: ogni file che usa `[Fact]` o `Assert` lì dentro ha bisogno di un `using Xunit;` esplicito (`[AvaloniaFact]` no — vive in `Avalonia.Headless.XUnit`). Senza, il compilatore si ferma su `CS0246` per `Fact`, **prima** di arrivare a valutare il resto del file — e maschera qualunque errore il test fosse nato per dimostrare.
- **`InitializeComponent()`, mai `AvaloniaXamlLoader.Load(this)`**: il secondo carica lo XAML ma non cabla i campi `x:Name`, e il guasto è un `NullReferenceException` a runtime senza nessun errore di compilazione.
- **Un test di regressione va provato contro il codice rotto**: dove il piano lo chiede, disattivare il fix, vedere il rosso, ripristinare. Un test che non può fallire è peggio di nessun test. **Vale anche per i test di guardia** — quelli che sorvegliano un invariante invece di una regressione: la prima stesura di `NoWpfReferenceTests` asseriva su `TargetFrameworkAttribute.FrameworkName`, che vale `.NETCoreApp,Version=v10.0` **sia** per `net10.0` **sia** per `net10.0-windows`, quindi il guard contro il TFM sbagliato era verde per costruzione. Se un test dichiara di sorvegliare qualcosa, il piano deve dire **come lo si è visto scattare**.
- **Footer di ogni commit**:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
  ```

## Struttura dei file

Cosa nasce, cosa si sposta, e di cosa risponde ciascuno.

| File / progetto | Responsabilità |
|---|---|
| `src/Recordum.Presentation/` (nuovo, `net10.0`) | I 126 ViewModel, le 504 stringhe, le 10 interfacce di servizio, il bootstrap portabile, `DropPlan`, `Formatting/`. Zero UI. Referenzia Core, Data, Plugins, Plugins.Contracts |
| `tests/Recordum.Presentation.Tests/` (nuovo, `net10.0`) | I 178 file di test liberi da Windows. **Girano su Linux** |
| `src/Recordum.Core/Abstractions/ITickSource.cs` (nuovo) | `ITickSource` + `ITickSourceFactory`: "chiamami ogni X, sul thread dell'interfaccia". Una sola responsabilità, due implementazioni per-shell |
| `src/Recordum.Core/Abstractions/NullTickSource.cs` (nuovo) | L'implementazione che non ticchetta mai, per i test e per le anteprime inerti |
| `src/Recordum.UI/` (rinominato da `src/Recordum.Avalonia/`) | La shell Avalonia. Il nome non fa più ombra al namespace radice `Avalonia` |
| `src/Recordum.WPF/Services/DispatcherTickSource.cs` (nuovo) | `ITickSourceFactory` su `System.Windows.Threading.DispatcherTimer`. Muore con WPF |
| `src/Recordum.UI/Services/DispatcherTickSource.cs` (nuovo) | Lo stesso su `Avalonia.Threading.DispatcherTimer` |
| `src/Recordum.Core/Abstractions/IApplicationLifetime.cs` (nuovo) | `Shutdown()`: il ViewModel chiede di uscire, non sa come |
| `src/Recordum.UI/Fonts/` (nuovo) | Il serif imbustato, servito da `avares://` |

**Cosa `F0` NON deve costruire, perché esiste già.** La riga `F0` della spec (§6) nomina *"token + i 64 righe di stile"*: sono già scritti e già cablati dalla sonda tema — `src/Recordum.UI/Themes/Tokens.axaml` (43 chiavi, stessi nomi e stessi esadecimali di WPF, con le due varianti sotto `ThemeDictionaries`) e `Themes/RecordumStyles.axaml`, entrambi già inclusi da `App.axaml`. In quest'onda si tocca **una sola riga** di `Tokens.axaml`, il token del font, al Task 9. Lo stesso vale per `HeatmapStrip`, portato dalla sonda controlli: il Task 10 porta i due che mancano, non lo rifà.

---

### Task 1: `Recordum.Avalonia` si chiama `Recordum.UI`

Il progetto Avalonia contiene un segmento di namespace chiamato `Avalonia` sotto `Recordum`, quindi dentro qualunque tipo `Recordum.*` il nome `Avalonia` risolve a **quello** invece che al namespace radice del framework, e serve `global::`. Oggi il progetto ha 25 file; a `F1` ne avrà sessanta. Si rinomina adesso.

**Files:**
- Rename: `src/Recordum.Avalonia/` → `src/Recordum.UI/` (e il `.csproj` dentro)
- Rename: `tests/Recordum.Avalonia.Tests/` → `tests/Recordum.UI.Tests/` (e il `.csproj` dentro)
- Modify: `Recordum.slnx`
- Create: `tests/Recordum.UI.Tests/NamespaceShadowingTests.cs`
- Modify: i 25 file `.cs`/`.axaml` che nominano `Recordum.Avalonia`

**Interfaces:**
- Consuma: niente (primo task).
- Produce: i namespace `Recordum.UI`, `Recordum.UI.Controls`, `Recordum.UI.Views`, `Recordum.UI.Themes` e `Recordum.UI.Tests`. **Ogni task successivo usa questi nomi**, mai `Recordum.Avalonia`.

- [ ] **Step 1: Verificare la baseline**

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
cd /home/lucafrog/dev/recordum
git rev-parse --abbrev-ref HEAD          # deve stampare: avalonia-linux-slice
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
dotnet test tests/Recordum.Avalonia.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: `0 Error(s)`, e `Passed! - Failed: 0, Passed: 12`.

- [ ] **Step 2: Scrivere il test che oggi NON compila**

Crea `tests/Recordum.Avalonia.Tests/NamespaceShadowingTests.cs` (nella cartella **vecchia**, di proposito):

```csharp
using Xunit;

namespace Recordum.Avalonia.Tests;

public class NamespaceShadowingTests
{
    [Fact]
    public void Root_Avalonia_namespace_resolves_without_a_global_prefix()
    {
        // Dentro un tipo di un namespace Recordum.*, il nome `Avalonia` deve risolvere al namespace
        // radice del framework. Finché il progetto si chiama Recordum.Avalonia, la ricerca trova
        // PRIMA quel namespace, che non ha un membro `Threading`, e questa riga non compila:
        // serve `global::Avalonia.Threading.Dispatcher`. Dopo la rinomina compila da sé.
        // L'assert è deliberatamente su typeof e non su Dispatcher.UIThread: la prova che questo
        // test porta è di COMPILAZIONE (prima della rinomina il file non compilava affatto), e un
        // assert a runtime potrebbe fallire per una ragione diversa — nessuna piattaforma Avalonia
        // inizializzata in un [Fact] semplice — rendendo il rosso ambiguo.
        Assert.Equal("Avalonia.Threading.Dispatcher", typeof(Avalonia.Threading.Dispatcher).FullName);
    }
}
```

- [ ] **Step 3: Eseguire e vedere il rosso**

```bash
dotnet build tests/Recordum.Avalonia.Tests -p:EnableWindowsTargeting=true --nologo 2>&1 | grep -E 'error|Error' | head -3
```

Atteso: un errore di compilazione `CS0234` — *"The type or namespace name 'Threading' does not exist in the namespace 'Recordum.Avalonia'"*. **È la prova che il difetto esiste.** Se compila, fermarsi: l'ombra non c'è e questo task non serve.

- [ ] **Step 4: Rinominare cartelle e progetti**

```bash
cd /home/lucafrog/dev/recordum
git mv src/Recordum.Avalonia src/Recordum.UI
git mv src/Recordum.UI/Recordum.Avalonia.csproj src/Recordum.UI/Recordum.UI.csproj
git mv tests/Recordum.Avalonia.Tests tests/Recordum.UI.Tests
git mv tests/Recordum.UI.Tests/Recordum.Avalonia.Tests.csproj tests/Recordum.UI.Tests/Recordum.UI.Tests.csproj
```

- [ ] **Step 5: Sostituire il namespace ovunque**

```bash
cd /home/lucafrog/dev/recordum
grep -rl 'Recordum\.Avalonia' src/Recordum.UI tests/Recordum.UI.Tests Recordum.slnx \
  --include='*.cs' --include='*.axaml' --include='*.csproj' --include='*.slnx' \
  | grep -v '/obj/' | grep -v '/bin/' \
  | xargs sed -i 's/Recordum\.Avalonia/Recordum.UI/g'
# Il file di test appena creato porta ancora il vecchio nome nel commento: aggiornalo a mano.
sed -i 's/Finché il progetto si chiama Recordum.UI/Finché il progetto si chiamava Recordum.Avalonia/' \
  tests/Recordum.UI.Tests/NamespaceShadowingTests.cs
grep -rn 'Recordum\.Avalonia' src tests Recordum.slnx --include='*.cs' --include='*.axaml' --include='*.csproj' --include='*.slnx' | grep -v '/obj/' | grep -v '/bin/'
```

Atteso dall'ultimo `grep`: **esattamente un hit**, la riga di commento in `NamespaceShadowingTests.cs` che nomina il nome storico al passato — la sed correttiva qui sopra ce l'ha appena rimessa apposta. Non è un refuso da inseguire: è il riferimento giusto, e non deve essere "corretto" per far tornare il conto a zero. Che non ce ne siano altri lo dice:

```bash
grep -rn 'Recordum\.Avalonia' src tests Recordum.slnx --include='*.cs' --include='*.axaml' --include='*.csproj' --include='*.slnx' \
  | grep -v '/obj/' | grep -v '/bin/' | grep -v 'NamespaceShadowingTests.cs'
```

Atteso: **nessun output**.

- [ ] **Step 6: Ripulire gli artefatti di build della vecchia cartella**

```bash
rm -rf src/Recordum.UI/obj src/Recordum.UI/bin tests/Recordum.UI.Tests/obj tests/Recordum.UI.Tests/bin
```

(`obj/` conserva il nome vecchio dell'assembly nei file generati: senza questo, il primo build può risolvere riferimenti stantii.)

- [ ] **Step 7: Eseguire e vedere il verde**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
dotnet test tests/Recordum.UI.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: `0 Error(s)`; `Passed! - Failed: 0, Passed: 13` (i 12 di prima più il nuovo).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor(ui): il progetto Avalonia si chiama Recordum.UI, e smette di fare ombra

Recordum.Avalonia introduceva un segmento di namespace chiamato Avalonia sotto
Recordum, quindi dentro qualunque tipo Recordum.* il nome risolveva a quello
invece che al namespace radice del framework, e serviva global:: ovunque. Con 25
file era un fastidio; a F1 il progetto ne avrà sessanta.

Il nome non porta più il framework: Avalonia è LA piattaforma, quindi dirlo nel
nome è informazione temporanea, e Recordum.UI lascia spazio alla forma standard
futura (Recordum.UI libreria + .Desktop/.Browser come teste).

NamespaceShadowingTests è il guard: nomina Avalonia.Threading.Dispatcher senza
global:: dentro un tipo Recordum.*, cosa che prima della rinomina non compilava
(CS0234, verificato).

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 2: `Recordum.Presentation` nasce, e `Formatting/` trasloca per primo

Due progetti vuoti non si possono verificare. Questo task li crea **e** ci sposta la cosa più piccola che esista — le due classi di `Formatting/`, che non dipendono da nulla — così il tubo è provato end-to-end da un test che gira su Linux prima che gli passi dentro qualcosa di grosso.

**Files:**
- Create: `src/Recordum.Presentation/Recordum.Presentation.csproj`
- Create: `tests/Recordum.Presentation.Tests/Recordum.Presentation.Tests.csproj`
- Create: `tests/Recordum.Presentation.Tests/NoWpfReferenceTests.cs`
- Move: `src/Recordum.WPF/Formatting/{CounterTotalDisplay,DurationFormat}.cs` → `src/Recordum.Presentation/Formatting/`
- Move: `tests/Recordum.WPF.Tests/Formatting/*.cs` (3 file) → `tests/Recordum.Presentation.Tests/Formatting/`
- Modify: `Recordum.slnx`, `src/Recordum.WPF/Recordum.WPF.csproj`, `src/Recordum.UI/Recordum.UI.csproj`

**Interfaces:**
- Consuma: i namespace `Recordum.UI.*` del Task 1.
- Produce: il progetto `Recordum.Presentation` con namespace radice `Recordum.Presentation`; `Recordum.Presentation.Formatting.DurationFormat` e `.CounterTotalDisplay` (erano `Recordum.WPF.Formatting.*`); il progetto di test `Recordum.Presentation.Tests`. **I task 3-10 assumono che entrambi esistano e siano in `Recordum.slnx`.**

- [ ] **Step 1: Creare il progetto Presentation**

`src/Recordum.Presentation/Recordum.Presentation.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- net10.0 SENZA -windows, deliberatamente: è questo che rende il confine
         dell'estrazione auto-verificante. Qualunque file che tocchi System.Windows
         non compila qui, quindi non può entrarci per distrazione. -->
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Recordum.Core\Recordum.Core.csproj" />
    <ProjectReference Include="..\Recordum.Data\Recordum.Data.csproj" />
    <ProjectReference Include="..\Recordum.Plugins\Recordum.Plugins.csproj" />
    <ProjectReference Include="..\Recordum.Plugins.Contracts\Recordum.Plugins.Contracts.csproj" />
  </ItemGroup>

</Project>
```

(I quattro riferimenti non sono un'ipotesi: i ViewModel importano `Recordum.Core`, `Recordum.Data` — tre di loro usano `Recordum.Data.Services.Queries` — e `Recordum.Plugins`; `ChartBuilderViewModel` usa `IExtensionPointRegistry` da Plugins.Contracts.)

- [ ] **Step 2: Creare il progetto di test**

`tests/Recordum.Presentation.Tests/Recordum.Presentation.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="7.2.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Recordum.Presentation\Recordum.Presentation.csproj" />
  </ItemGroup>

</Project>
```

(Stessi pacchetti e stesse versioni di `Recordum.WPF.Tests`, **meno `Xunit.StaFact`**: qui non c'è un thread STA da chiedere. È xunit **2.9.3**, non v3 — la v3 serve solo a `Recordum.UI.Tests` per `Avalonia.Headless.XUnit`.)

- [ ] **Step 3: Registrare i due progetti nella soluzione**

In `Recordum.slnx`, aggiungi una riga in `/src/` e una in `/tests/`, mantenendo l'ordine alfabetico esistente:

```xml
    <Project Path="src/Recordum.Presentation/Recordum.Presentation.csproj" />
```
```xml
    <Project Path="tests/Recordum.Presentation.Tests/Recordum.Presentation.Tests.csproj" />
```

- [ ] **Step 4: Far referenziare Presentation dalle due shell**

In `src/Recordum.WPF/Recordum.WPF.csproj`, dentro l'`ItemGroup` dei `ProjectReference`:

```xml
    <ProjectReference Include="..\Recordum.Presentation\Recordum.Presentation.csproj" />
```

In `src/Recordum.UI/Recordum.UI.csproj`, dentro l'`ItemGroup` dei `ProjectReference`:

```xml
    <ProjectReference Include="../Recordum.Presentation/Recordum.Presentation.csproj" />
```

- [ ] **Step 5: Scrivere il test del confine**

`tests/Recordum.Presentation.Tests/NoWpfReferenceTests.cs`:

```csharp
using System.Reflection;
using Recordum.Presentation.Formatting;

namespace Recordum.Presentation.Tests;

public class NoWpfReferenceTests
{
    /// <summary>Il confine dell'estrazione non è una convenzione, è una proprietà dell'assembly:
    /// Recordum.Presentation è net10.0 senza -windows, quindi non PUÒ referenziare WPF. Questo test
    /// lo rende esplicito e lo tiene vero anche se un domani qualcuno cambiasse il TFM.</summary>
    [Fact]
    public void Presentation_references_no_WPF_assembly()
    {
        var referenced = typeof(DurationFormat).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("PresentationFramework", referenced);
        Assert.DoesNotContain("PresentationCore", referenced);
        Assert.DoesNotContain("WindowsBase", referenced);
    }

    /// <summary>Il TFM stesso: se qualcuno aggiungesse -windows, il test sopra continuerebbe a
    /// passare finché nessuno USA davvero un tipo WPF. Questo scatta subito.
    /// <para>Guarda <c>TargetPlatformAttribute</c> e NON <c>TargetFrameworkAttribute.FrameworkName</c>:
    /// quest'ultimo vale <c>.NETCoreApp,Version=v10.0</c> per ENTRAMBI i TFM — misurato sugli assembly
    /// di questo repo, `Recordum.Core` (net10.0) non porta attributi di piattaforma mentre
    /// `Recordum.WPF` (net10.0-windows) porta <c>TargetPlatformAttribute("Windows7.0")</c> — quindi
    /// un'asserzione su FrameworkName sarebbe verde in entrambi i casi, cioè inerte.</para>
    /// <para>I cancelli contro un `-windows` di troppo sono in realtà DUE, in serie, e questo è il
    /// secondo: mettendo `-windows` sul solo `Recordum.Presentation`, il restore fallisce prima con
    /// <c>NU1201</c> — un progetto di test `net10.0` non può referenziare un progetto
    /// `net10.0-windows` — e il test non arriva nemmeno a girare. Questa asserzione serve quando
    /// qualcuno aggira il primo cancello mettendo `-windows` a entrambi i progetti, che è
    /// esattamente il modo in cui un errore di confine verrebbe "fatto sparire".</para></summary>
    [Fact]
    public void Presentation_targets_a_platform_neutral_framework()
    {
        var platform = typeof(DurationFormat).Assembly
            .GetCustomAttribute<System.Runtime.Versioning.TargetPlatformAttribute>();

        Assert.Null(platform);
    }
}
```

- [ ] **Step 6: Spostare `Formatting/` e i suoi test**

```bash
cd /home/lucafrog/dev/recordum
mkdir -p src/Recordum.Presentation/Formatting tests/Recordum.Presentation.Tests/Formatting
git mv src/Recordum.WPF/Formatting/CounterTotalDisplay.cs src/Recordum.Presentation/Formatting/
git mv src/Recordum.WPF/Formatting/DurationFormat.cs     src/Recordum.Presentation/Formatting/
git mv tests/Recordum.WPF.Tests/Formatting/CounterTotalDisplayTests.cs   tests/Recordum.Presentation.Tests/Formatting/
git mv tests/Recordum.WPF.Tests/Formatting/DurationFormatTests.cs        tests/Recordum.Presentation.Tests/Formatting/
git mv tests/Recordum.WPF.Tests/Formatting/DurationFormatElapsedTests.cs tests/Recordum.Presentation.Tests/Formatting/
rmdir src/Recordum.WPF/Formatting tests/Recordum.WPF.Tests/Formatting
```

- [ ] **Step 7: Riscrivere i namespace dei file spostati e dei loro consumatori**

```bash
cd /home/lucafrog/dev/recordum
# I file spostati dichiarano il nuovo namespace.
sed -i 's/^namespace Recordum\.WPF\.Formatting;/namespace Recordum.Presentation.Formatting;/' \
  src/Recordum.Presentation/Formatting/*.cs
sed -i 's/^namespace Recordum\.WPF\.Tests\.Formatting;/namespace Recordum.Presentation.Tests.Formatting;/' \
  tests/Recordum.Presentation.Tests/Formatting/*.cs
# Chi li usa (6 ViewModel + i test spostati + eventuali view) cambia using.
grep -rl 'using Recordum\.WPF\.Formatting' src tests --include='*.cs' | grep -v '/obj/' \
  | xargs -r sed -i 's/using Recordum\.WPF\.Formatting/using Recordum.Presentation.Formatting/'
grep -rn 'Recordum\.WPF\.Formatting' src tests --include='*.cs' --include='*.xaml' | grep -v '/obj/'
```

Atteso dall'ultimo `grep`: **nessun output**.

- [ ] **Step 8: Eseguire e vedere il verde**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
dotnet test tests/Recordum.Presentation.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: `0 Error(s)`; il progetto nuovo passa (2 test del confine + i test di formattazione spostati, tutti verdi) **e gira su Linux**, che è il punto.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor(presentation): il progetto senza UI nasce, e Formatting/ è il primo a traslocare

Recordum.Presentation è net10.0 SENZA -windows, ed è questo a rendere il confine
dell'estrazione auto-verificante: un file che tocca System.Windows non compila
qui, quindi non ci entra per distrazione. La regola non è una convenzione da
ricordare, la impone il compilatore.

Due progetti vuoti non si possono verificare, quindi il task ci sposta dentro la
cosa più piccola che esista — le due classi di Formatting/, che non dipendono da
nulla — così il tubo è provato end-to-end prima che ci passi qualcosa di grosso.
NoWpfReferenceTests guarda sia gli assembly referenziati sia il TFM: il primo
passerebbe comunque finché nessuno USA WPF, il secondo scatta subito.

I test di formattazione girano ora su Linux. Sono i primi tre dei 178.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 3: `ITickSource` — quattro `DispatcherTimer` diventano un'astrazione

Quattro classi di ViewModel costruiscono un `System.Windows.Threading.DispatcherTimer`, ognuna dietro la stessa guardia `if (Application.Current is not null)`, perché in headless il dispatcher non c'è. Sono quattro dei sette agganci a WPF. Questo task crea l'astrazione e le due implementazioni; il Task 4 la fa arrivare dove serve.

**Files:**
- Create: `src/Recordum.Core/Abstractions/ITickSource.cs`
- Create: `src/Recordum.Core/Abstractions/NullTickSource.cs`
- Create: `tests/Recordum.Core.Tests/Abstractions/FakeTickSource.cs`
- Create: `tests/Recordum.Core.Tests/Abstractions/TickSourceTests.cs`
- Create: `src/Recordum.WPF/Services/DispatcherTickSource.cs`
- Create: `src/Recordum.UI/Services/DispatcherTickSource.cs`

**Interfaces:**
- Consuma: i due progetti del Task 2 (non direttamente, ma il piano assume che esistano).
- Produce, e il Task 4 li usa con **questi nomi esatti**:
  - `Recordum.Core.Abstractions.ITickSource : IDisposable` — `event EventHandler Tick`, `void Start()`, `void Stop()`
  - `Recordum.Core.Abstractions.ITickSourceFactory` — `ITickSource Create(TimeSpan interval)`
  - `Recordum.Core.Abstractions.NullTickSourceFactory.Instance` — non ticchetta mai
  - `Recordum.WPF.Services.DispatcherTickSourceFactory` e `Recordum.UI.Services.DispatcherTickSourceFactory` — entrambe `ITickSourceFactory`
  - `Recordum.Core.Tests.Abstractions.FakeTickSourceFactory` — con `void TickAll()` per far scattare a comando

- [ ] **Step 1: Scrivere il test che fallisce**

`tests/Recordum.Core.Tests/Abstractions/TickSourceTests.cs`:

```csharp
using Recordum.Core.Abstractions;

namespace Recordum.Core.Tests.Abstractions;

public class TickSourceTests
{
    [Fact]
    public void Null_factory_produces_a_source_that_never_ticks()
    {
        var ticks = 0;
        using var source = NullTickSourceFactory.Instance.Create(TimeSpan.FromMilliseconds(1));
        source.Tick += (_, _) => ticks++;

        source.Start();

        // Non è un test sul tempo che passa: NullTickSource non ha un timer, quindi non c'è nulla
        // da attendere. Start() su di lui è un no-op per costruzione, ed è ciò che serve a un
        // ViewModel in un test headless o a un'anteprima inerte del builder.
        Assert.Equal(0, ticks);
    }

    [Fact]
    public void Fake_factory_ticks_only_when_told_to()
    {
        var factory = new FakeTickSourceFactory();
        var ticks = 0;
        using var source = factory.Create(TimeSpan.FromSeconds(1));
        source.Tick += (_, _) => ticks++;

        factory.TickAll();          // non avviato: nessun tick
        source.Start();
        factory.TickAll();
        factory.TickAll();
        source.Stop();
        factory.TickAll();          // fermato: nessun tick

        Assert.Equal(2, ticks);
    }

    [Fact]
    public void Fake_factory_records_the_requested_interval()
    {
        var factory = new FakeTickSourceFactory();
        using var a = factory.Create(TimeSpan.FromSeconds(1));
        using var b = factory.Create(TimeSpan.FromMilliseconds(300));

        // Il debounce dell'anteprima grafici chiede 300ms, gli orologi 1s: chi cuce l'astrazione
        // deve poter provare che l'intervallo giusto è arrivato fin qui.
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(300)], factory.RequestedIntervals);
    }

    [Fact]
    public void Disposing_a_source_stops_it()
    {
        var factory = new FakeTickSourceFactory();
        var ticks = 0;
        var source = factory.Create(TimeSpan.FromSeconds(1));
        source.Tick += (_, _) => ticks++;
        source.Start();

        source.Dispose();
        factory.TickAll();

        Assert.Equal(0, ticks);
    }
}
```

- [ ] **Step 2: Scrivere il fake**

`tests/Recordum.Core.Tests/Abstractions/FakeTickSource.cs`:

```csharp
using Recordum.Core.Abstractions;

namespace Recordum.Core.Tests.Abstractions;

/// <summary>Un ITickSourceFactory pilotato a mano: i test decidono QUANDO passa un secondo, invece
/// di aspettarlo. Sostituisce la vecchia condizione "Application.Current è null, quindi non
/// ticchetta e questo pezzo non si prova".</summary>
public sealed class FakeTickSourceFactory : ITickSourceFactory
{
    private readonly List<FakeTickSource> _created = [];

    public IReadOnlyList<TimeSpan> RequestedIntervals => _created.Select(s => s.Interval).ToArray();

    public ITickSource Create(TimeSpan interval)
    {
        var source = new FakeTickSource(interval);
        _created.Add(source);
        return source;
    }

    /// <summary>Fa scattare un tick su ogni sorgente avviata e non disposta.</summary>
    public void TickAll()
    {
        foreach (var s in _created.ToArray())
            s.RaiseIfRunning();
    }

    private sealed class FakeTickSource(TimeSpan interval) : ITickSource
    {
        private bool _running;
        private bool _disposed;

        public TimeSpan Interval { get; } = interval;
        public event EventHandler? Tick;

        public void Start() => _running = !_disposed;
        public void Stop() => _running = false;

        public void RaiseIfRunning()
        {
            if (_running && !_disposed)
                Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _running = false;
            _disposed = true;
            Tick = null;
        }
    }
}
```

- [ ] **Step 3: Eseguire e vedere il rosso**

```bash
dotnet build tests/Recordum.Core.Tests --nologo 2>&1 | grep -E 'error CS' | head -5
```

Atteso: errori `CS0246` — `ITickSource`, `ITickSourceFactory`, `NullTickSourceFactory` non esistono.

- [ ] **Step 4: Scrivere l'astrazione**

`src/Recordum.Core/Abstractions/ITickSource.cs`:

```csharp
namespace Recordum.Core.Abstractions;

/// <summary>Un battito ripetuto consegnato sul thread dell'interfaccia. Un'istanza = un timer.
/// Astrae il DispatcherTimer, che esiste con lo stesso nome e la stessa forma sia in
/// System.Windows.Threading (WPF) sia in Avalonia.Threading, ma sono due tipi diversi in due
/// assembly diversi — e uno dei due non esiste fuori da Windows.</summary>
public interface ITickSource : IDisposable
{
    event EventHandler Tick;

    /// <summary>Avvia il battito. Chiamarlo su una sorgente già avviata non ha effetto.</summary>
    void Start();

    // CA1716: `Stop` collide con una parola riservata di VB, e con AnalysisLevel=latest-recommended
    // + TreatWarningsAsErrors quell'analizzatore è un ERRORE di build. Si sopprime invece di
    // rinominare: `Stop()` è il nome che il Task 4 userà su quattro classi, ed è il nome che la
    // stessa forma ha già in DispatcherTimer. Stesso pattern di IAbstinenceService.cs:22.
#pragma warning disable CA1716
    /// <summary>Ferma il battito senza disporre la sorgente: si può riavviare.</summary>
    void Stop();
#pragma warning restore CA1716
}

/// <summary>Crea sorgenti di battito. Iniettato al posto di <c>new DispatcherTimer(...)</c>:
/// è la fabbrica, non la sorgente, a essere un servizio, perché ogni consumatore vuole il proprio
/// timer con il proprio intervallo.</summary>
public interface ITickSourceFactory
{
    ITickSource Create(TimeSpan interval);
}
```

- [ ] **Step 5: Scrivere l'implementazione nulla**

`src/Recordum.Core/Abstractions/NullTickSource.cs`:

```csharp
namespace Recordum.Core.Abstractions;

/// <summary>La fabbrica che produce sorgenti che non battono mai. È la forma esplicita di ciò che
/// prima era implicito nella guardia <c>if (Application.Current is not null)</c>: un ViewModel in
/// un test headless, o un'anteprima deliberatamente inerte come quella del builder, riceve questa
/// invece di null-e-una-guardia sparsa in quattro classi.</summary>
public sealed class NullTickSourceFactory : ITickSourceFactory
{
    public static NullTickSourceFactory Instance { get; } = new();

    private NullTickSourceFactory() { }

    public ITickSource Create(TimeSpan interval) => new NullTickSource();

    private sealed class NullTickSource : ITickSource
    {
        // Il campo non è mai invocato: esiste perché ITickSource lo richiede. CS0067 (evento mai
        // usato) sarebbe un errore con TreatWarningsAsErrors, quindi lo si sopprime qui e solo qui.
#pragma warning disable CS0067
        public event EventHandler? Tick;
#pragma warning restore CS0067

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
```

- [ ] **Step 6: Eseguire e vedere il verde**

```bash
dotnet test tests/Recordum.Core.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: `Passed! - Failed: 0, Passed: 149` (145 di baseline + 4 nuovi).

- [ ] **Step 7: Scrivere le due implementazioni reali**

`src/Recordum.WPF/Services/DispatcherTickSource.cs`:

```csharp
using System.Windows.Threading;
using Recordum.Core.Abstractions;

namespace Recordum.WPF.Services;

/// <summary>ITickSourceFactory su DispatcherTimer di WPF. Muore con la shell WPF: la gemella
/// Avalonia è src/Recordum.UI/Services/DispatcherTickSource.cs, identica salvo il namespace del
/// timer.</summary>
public sealed class DispatcherTickSourceFactory : ITickSourceFactory
{
    public ITickSource Create(TimeSpan interval) => new DispatcherTickSource(interval);

    private sealed class DispatcherTickSource : ITickSource
    {
        private readonly DispatcherTimer _timer;

        public DispatcherTickSource(TimeSpan interval)
        {
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += OnTick;
        }

        public event EventHandler? Tick;

        private void OnTick(object? sender, EventArgs e) => Tick?.Invoke(this, EventArgs.Empty);

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            // Un DispatcherTimer in moto è radicato dal dispatcher: senza Stop() + il distacco
            // dell'handler continua a battere e tiene in vita il proprio proprietario.
            _timer.Stop();
            _timer.Tick -= OnTick;
            Tick = null;
        }
    }
}
```

`src/Recordum.UI/Services/DispatcherTickSource.cs`:

```csharp
using Avalonia.Threading;
using Recordum.Core.Abstractions;

namespace Recordum.UI.Services;

/// <summary>ITickSourceFactory su DispatcherTimer di Avalonia. Gemella di quella WPF: stesso nome
/// di tipo del timer, altro assembly. È tutta la differenza che c'era da astrarre.</summary>
public sealed class DispatcherTickSourceFactory : ITickSourceFactory
{
    public ITickSource Create(TimeSpan interval) => new DispatcherTickSource(interval);

    private sealed class DispatcherTickSource : ITickSource
    {
        private readonly DispatcherTimer _timer;

        public DispatcherTickSource(TimeSpan interval)
        {
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += OnTick;
        }

        public event EventHandler? Tick;

        private void OnTick(object? sender, EventArgs e) => Tick?.Invoke(this, EventArgs.Empty);

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            Tick = null;
        }
    }
}
```

- [ ] **Step 8: Eseguire e vedere il verde**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
dotnet test tests/Recordum.Core.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: `0 Error(s)`; Core `Passed: 149`.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
feat(core): ITickSource — quattro DispatcherTimer diventano un'astrazione

Quattro classi di ViewModel costruiscono un System.Windows.Threading.DispatcherTimer,
ognuna dietro la stessa guardia `if (Application.Current is not null)`. Sono quattro
dei sette agganci a WPF, e nessuno di loro può entrare in Recordum.Presentation, che
è net10.0.

Avalonia ha DispatcherTimer con lo stesso nome e la stessa forma in Avalonia.Threading:
la differenza da astrarre è solo l'assembly, quindi ITickSource è sottile per davvero
(Tick/Start/Stop) e le due implementazioni sono gemelle.

NullTickSourceFactory rende esplicito ciò che la guardia diceva implicitamente
("qui non si batte"), e FakeTickSourceFactory ribalta la situazione nei test: prima
un test headless NON poteva provare il battito perché il timer non nasceva; ora
decide lui quando passa un secondo. Il quarto test prova che l'intervallo richiesto
arriva a destinazione — serve al Task 4, dove il debounce dei grafici chiede 300ms
e gli orologi 1s.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 4: Le quattro classi ricevono il battito invece di crearlo

La cascata è **tre anelli e poi la DI**, mappata sul codice e non dedotta:

| Anello | Chi |
|---|---|
| 1 — chi crea un timer | `HomeClockTicker`, `LiveElapsedClock`, `AbstinenceQuickAddViewModel`, `ChartBuilderViewModel` |
| 2 — chi costruisce l'anello 1 | `HomeViewModel` (campo `= new()`), `AbstinencePeriodListViewModel`, `DetailViewModel`, `TodayStripViewModel`, `SamplePreviewFactory`, `MeasuredQuickAddViewModel`, `CounterSeriesQuickAddViewModel` |
| 3 — chi costruisce l'anello 2 | `HabitScreenViewModel` (cinque di loro), `HabitBuilderViewModel` (`SamplePreviewFactory`) |
| 4 | `ServiceRegistrar`: `HomeViewModel`, `HabitScreenViewModel`, `HabitBuilderViewModel`, `ChartBuilderViewModel` sono risolti dal contenitore. **`AbstinenceQuickAddViewModel` NO**: non è registrato da nessuna parte, lo costruisce `HabitScreenViewModel:749` con un `new` nudo — è un **sesto** sito di costruzione, e va nell'anello 3 insieme agli altri cinque che quella classe crea (verificato: `grep AbstinenceQuickAddViewModel ServiceRegistrar.cs` non trova nulla) |

**Regola di forma, che decide il rischio.** Sull'**anello 1** il parametro è **obbligatorio** (posizionale, tipo nullable `ITickSourceFactory?`): così il compilatore elenca ogni singolo sito di costruzione e nessun ramo di produzione può restare senza battito in silenzio. Sugli **anelli 2 e 3** è **opzionale con default `null`**, così i circa trenta siti di costruzione nei test WPF continuano a compilare invariati. `null` significa esattamente ciò che significava `Application.Current is null`: questa istanza non batte.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Home/HomeClockTicker.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/Charts/ChartBuilderViewModel.cs`
- Modify (anello 2): `HomeViewModel.cs`, `AbstinencePeriodListViewModel.cs`, `HabitScreen/Detail/DetailViewModel.cs`, `HabitScreen/Today/TodayStripViewModel.cs`, `HabitBuilder/Preview/SamplePreviewFactory.cs`, `HabitScreen/QuickAdd/MeasuredQuickAddViewModel.cs`, `HabitScreen/QuickAdd/CounterSeriesQuickAddViewModel.cs`, `HabitScreen/QuickAdd/SeriesQuickAddViewModelBase.cs`, `HabitScreen/AbstinencePeriodRowViewModel.cs`, `HabitScreen/Detail/StreakSummaryViewModel.cs`, `HabitScreen/Today/ResistanceTodayContent.cs`
- Modify (anello 3): `HabitScreen/HabitScreenViewModel.cs`, `HabitBuilder/HabitBuilderViewModel.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Modify: i siti di costruzione in `tests/Recordum.WPF.Tests/` che il compilatore segnalerà (anello 1 soltanto)

**Interfaces:**
- Consuma: `ITickSource`, `ITickSourceFactory`, `NullTickSourceFactory.Instance`, `DispatcherTickSourceFactory` (Task 3).
- Produce: nessun file sotto `src/Recordum.WPF/ViewModels/` nomina più `System.Windows.Threading` né `Application.Current` **per un timer**. Il Task 7 lo verifica con un `grep` che deve tornare vuoto.

- [ ] **Step 1: Anello 1 — `HomeClockTicker`**

Sostituisci l'intero corpo di `src/Recordum.WPF/ViewModels/Home/HomeClockTicker.cs` con:

```csharp
using Recordum.Core.Abstractions;

namespace Recordum.WPF.ViewModels.Home;

/// <summary>
/// Un solo battito da 1 secondo condiviso da tutta la lista Home: le righe di astinenza registrano
/// una callback di refresh e una sola sorgente le serve tutte (spec §2 — un timer per la lista, non
/// uno per riga). <paramref name="ticks"/> null = non si batte: è il caso dei test headless, dove
/// <see cref="TickNow"/> pilota il fan-out a mano. Una sorgente in moto è radicata dal dispatcher,
/// quindi il proprietario DEVE <see cref="Clear"/> a ogni ricarica e <see cref="Dispose"/> allo
/// smontaggio, o continua a battere e perde memoria.
/// </summary>
public sealed class HomeClockTicker : IDisposable
{
    private readonly ITickSource? _tick;
    private readonly List<Action> _callbacks = new();

    public HomeClockTicker(ITickSourceFactory? ticks)
    {
        _tick = ticks?.Create(TimeSpan.FromSeconds(1));
        if (_tick is not null)
            _tick.Tick += OnTick;
    }

    /// <summary>Registra una callback per battito (il refresh dell'orologio di una riga di astinenza) e avvia la sorgente.</summary>
    public void Register(Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        _callbacks.Add(tick);
        _tick?.Start();
    }

    /// <summary>Scarta tutte le callback e ferma la sorgente. Chiamata prima di ogni ricostruzione della Home.</summary>
    public void Clear()
    {
        _callbacks.Clear();
        _tick?.Stop();
    }

    /// <summary>Invoca una volta ogni callback registrata. Cablata al battito, ed è anche l'ingresso
    /// manuale per i test. Una copia difende dalle callback che mutano la lista.</summary>
    public void TickNow()
    {
        foreach (var cb in _callbacks.ToArray())
            cb();
    }

    private void OnTick(object? sender, EventArgs e) => TickNow();

    public void Dispose()
    {
        Clear();
        if (_tick is not null)
        {
            _tick.Tick -= OnTick;
            _tick.Dispose();
        }
    }
}
```

- [ ] **Step 2: Anello 1 — `LiveElapsedClock`**

In `src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs`: togli `using System.Windows.Threading;`, aggiungi `using Recordum.Core.Abstractions;`, e sostituisci campo e costruttore:

```csharp
    private readonly IClock _clock;
    private readonly ITickSource? _tick;
    private long? _startedMs;

    [ObservableProperty] private string _display = string.Empty;

    /// <summary><paramref name="ticks"/> null = l'orologio mostra il valore al momento di Start()
    /// e non si aggiorna più da solo (test headless, anteprime inerti).</summary>
    public LiveElapsedClock(IClock clock, ITickSourceFactory? ticks)
    {
        _clock = clock;
        _tick = ticks?.Create(TimeSpan.FromSeconds(1));
        if (_tick is not null)
            _tick.Tick += OnTick;
    }
```

e in `Start`/`Stop`/`Dispose` sostituisci `_timer` con `_tick`:

```csharp
    public void Start(long startedAtMs)
    {
        _startedMs = startedAtMs;
        Refresh();
        _tick?.Start();
    }

    public void Stop()
    {
        _tick?.Stop();
        _startedMs = null;
        Display = string.Empty;
    }

    /// <summary>Ferma la sorgente e ne stacca l'handler, così l'orologio (e il suo proprietario) può essere raccolto.</summary>
    public void Dispose()
    {
        if (_tick is not null)
        {
            _tick.Stop();
            _tick.Tick -= OnTick;
            _tick.Dispose();
        }
        _startedMs = null;
        Display = string.Empty;
    }
```

- [ ] **Step 3: Anello 1 — `AbstinenceQuickAddViewModel`**

In `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs`: togli `using System.Windows.Threading;`, aggiungi `using Recordum.Core.Abstractions;` se manca, e sostituisci campo e costruttore:

```csharp
    private readonly IAbstinenceService _abstinence;
    private readonly ITickSource? _tick;
    private long? _startedMs;

    public AbstinenceQuickAddViewModel(
        ICompletionService completions,
        IAbstinenceService abstinence,
        IClock clock,
        ITickSourceFactory? ticks)
        : base(completions, clock)
    {
        ArgumentNullException.ThrowIfNull(abstinence);
        _abstinence = abstinence;

        _tick = ticks?.Create(TimeSpan.FromSeconds(1));
        if (_tick is not null)
            _tick.Tick += (_, _) => RefreshElapsed();
    }
```

- [ ] **Step 4: Anello 1 — `ChartBuilderViewModel`**

In `src/Recordum.WPF/ViewModels/Charts/ChartBuilderViewModel.cs`: togli `using System.Windows.Threading;`, aggiungi `using Recordum.Core.Abstractions;` se manca, e sostituisci campo e costruttore:

```csharp
    // Smorza l'anteprima viva: accorpa una raffica di modifiche in un solo ri-render ~300ms dopo
    // l'ultima. null = nessuno smorzamento, i test chiamano RefreshPreviewAsync direttamente.
    private readonly ITickSource? _previewTick;
```

```csharp
    public ChartBuilderViewModel(
        IChartService charts, IHabitTreeQueryService tree, IMetricDefinitionService metrics,
        IChartQueryService query, IExtensionPointRegistry registry, ITickSourceFactory? ticks)
    {
        _charts = charts;
        _tree = tree;
        _metrics = metrics;
        _query = query;
        _registry = registry;

        _previewTick = ticks?.Create(TimeSpan.FromMilliseconds(300));
        if (_previewTick is not null)
            _previewTick.Tick += OnPreviewTick;
    }
```

Poi sostituisci ogni altro uso di `_previewTimer` con `_previewTick` nel resto del file:

```bash
sed -i 's/_previewTimer/_previewTick/g' src/Recordum.WPF/ViewModels/Charts/ChartBuilderViewModel.cs
```

(La firma di `OnPreviewTick` — `(object? sender, EventArgs e)` — è già compatibile con `EventHandler`, quindi non cambia.)

- [ ] **Step 5: Vedere il rosso, e usarlo come elenco**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo 2>&1 | grep -E 'error CS' | sed 's/.*[\\/]\([A-Za-z]*\.cs\)/\1/' | sort -u
```

Atteso: un elenco di `CS7036` (*"There is no argument given that corresponds to the required parameter 'ticks'"*) su ogni sito di costruzione delle quattro classi — **è l'elenco di lavoro dei prossimi due step**, prodotto dal compilatore invece che da una ricerca a mano.

- [ ] **Step 6: Anelli 2 e 3 — infilare la fabbrica**

Ogni classe dell'anello 2 e 3 guadagna un parametro finale **opzionale** `ITickSourceFactory? ticks = null`, lo tiene in un campo `private readonly ITickSourceFactory? _ticks;` e lo passa a chi costruisce. Le sostituzioni esatte:

| File | Cosa passa, e a chi |
|---|---|
| `ViewModels/HomeViewModel.cs` | il campo `private readonly HomeClockTicker _clockTicker = new();` diventa `private readonly HomeClockTicker _clockTicker;`, assegnato nel costruttore con `_clockTicker = new HomeClockTicker(ticks);` |
| `ViewModels/HabitScreen/AbstinencePeriodRowViewModel.cs` | `Clock = new LiveElapsedClock(clock, ticks);` |
| `ViewModels/HabitScreen/AbstinencePeriodListViewModel.cs` | `new AbstinencePeriodRowViewModel(p, _abstinence, _confirm, _clock, DayBoundary, r => Rows.Remove(r), _ticks)` |
| `ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs` | `_liveClock = new LiveElapsedClock(clock, ticks);` |
| `ViewModels/HabitScreen/Detail/DetailViewModel.cs` | `StreakSummary = new StreakSummaryViewModel(streaks, abstinence, clock, ticks);` |
| `ViewModels/HabitScreen/Today/ResistanceTodayContent.cs` | `Clock = new LiveElapsedClock(clock, ticks);` |
| `ViewModels/HabitScreen/Today/TodayStripViewModel.cs` | `HabitType.Resistance => new ResistanceTodayContent(_abstinence, _clock, _ticks),` |
| `ViewModels/HabitScreen/QuickAdd/SeriesQuickAddViewModelBase.cs` | `ChronoClock = new LiveElapsedClock(Clock, ticks);` |
| `ViewModels/HabitScreen/QuickAdd/MeasuredQuickAddViewModel.cs` | inoltra `ticks` a `SeriesQuickAddViewModelBase(completions, clock, timers, ticks)` |
| `ViewModels/HabitScreen/QuickAdd/CounterSeriesQuickAddViewModel.cs` | idem |
| `ViewModels/HabitBuilder/Preview/SamplePreviewFactory.cs` | `new ResistanceTodayContent(new SampleServices.NoAbstinence(), _clock, NullTickSourceFactory.Instance)` — **qui la fabbrica nulla è deliberata, non una svista**: il commento accanto dice già *"Set, never Start(): un DispatcherTimer in moto in un'anteprima perderebbe memoria"*. Ora quella promessa è nel tipo, non in un commento |
| `ViewModels/HabitScreen/HabitScreenViewModel.cs` | passa `ticks` a `AbstinencePeriodListViewModel`, `DetailViewModel`, `TodayStripViewModel`, `MeasuredQuickAddViewModel`, `CounterSeriesQuickAddViewModel` |
| `ViewModels/HabitBuilder/HabitBuilderViewModel.cs` | costruisce `SamplePreviewFactory(clock)` — nessun parametro da aggiungere, la fabbrica nulla è dentro |

- [ ] **Step 6-bis: Chiudere la lacuna `Start()` dopo `Dispose()`**

Trovata dalla review del Task 3, e da chiudere **adesso** perché è ora che quattro classi cominciano a dipendere da questo contratto. In entrambe le `DispatcherTickSource` (WPF e `Recordum.UI`), `Start()` non ha una guardia post-`Dispose()`: chiamarlo dopo aver disposto **riavvia il timer nativo**. La catena `Tick` resta staccata, quindi non c'è un difetto funzionale visibile — ma il timer di piattaforma continua a battere, radicato dal dispatcher, per sempre. È esattamente la perdita contro cui i commenti di quelle classi mettono in guardia.

Non è una regressione dell'onda: `LiveElapsedClock.Start()` ha **la stessa lacuna già spedita in produzione** (verificato). Ma l'astrazione è il posto dove si chiude una volta invece che quattro, ed è per questo che si fa qui e non nelle quattro classi.

In entrambi i file, aggiungi il campo e la guardia:

```csharp
        private bool _disposed;

        public void Start()
        {
            if (_disposed) return;
            _timer.Start();
        }
```

e in `Dispose()`, prima di tutto il resto, `_disposed = true;`. `FakeTickSource` (Task 3) ha già questa semantica, quindi dopo questo step il fake e la produzione dicono la stessa cosa — che è la sola condizione per cui un test verde contro il fake significhi qualcosa sulla produzione.

Il test che lo copre vive in `Recordum.Core.Tests`, accanto agli altri quattro:

```csharp
    [Fact]
    public void A_disposed_source_does_not_restart()
    {
        var factory = new FakeTickSourceFactory();
        var ticks = 0;
        var source = factory.Create(TimeSpan.FromSeconds(1));
        source.Tick += (_, _) => ticks++;

        source.Dispose();
        source.Start();      // misuso: dopo Dispose non deve riaccendere niente
        factory.TickAll();

        Assert.Equal(0, ticks);
    }
```

- [ ] **Step 7: Anello 4 — registrare la fabbrica nella DI**

In `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`, accanto alle altre registrazioni di servizio:

```csharp
        // Il battito dell'interfaccia: una sola fabbrica per il processo, ogni consumatore crea la
        // propria sorgente col proprio intervallo. La gemella Avalonia si registra nella sua shell.
        services.AddSingleton<ITickSourceFactory, DispatcherTickSourceFactory>();
```

con `using Recordum.Core.Abstractions;` e `using Recordum.WPF.Services;` in testa se mancano.

- [ ] **Step 8: Aggiustare i siti di costruzione nei test WPF**

I test che costruiscono direttamente una classe dell'**anello 1** vanno aggiornati: passano `null` (nessun battito, che è ciò che facevano prima) oppure un `FakeTickSourceFactory` se vogliono provare il battito.

```bash
cd /home/lucafrog/dev/recordum
sed -i 's/new HomeClockTicker()/new HomeClockTicker(null)/g' tests/Recordum.WPF.Tests/ViewModels/Home/HomeClockTickerTests.cs
sed -i 's/new LiveElapsedClock(\(clock\|Clock\))/new LiveElapsedClock(\1, null)/g' tests/Recordum.WPF.Tests/ViewModels/HabitScreen/LiveElapsedClockTests.cs
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo 2>&1 | grep -E 'error CS' | head -10
```

Ripeti finché il `grep` non torna vuoto: ogni errore residuo nomina il file e la riga da correggere allo stesso modo.

- [ ] **Step 9: Verificare che i timer siano spariti dai ViewModel**

```bash
grep -rn 'System\.Windows\.Threading\|DispatcherTimer' src/Recordum.WPF/ViewModels --include='*.cs'
```

Atteso: **nessun output, commenti inclusi.**

**Niente filtri**, e la ragione è doppia. La prima: un commento che descrive un meccanismo che non c'è più è una trappola per il prossimo che legge, quindi va riscritto, non escluso. La seconda è che il filtro non funzionerebbe comunque — `grep -rn` prefissa ogni riga con `file:riga:`, quindi un `| grep -v '^\s*//'` a valle **non matcha mai nulla** e lascia passare tutto (provato). Un commento in coda a una riga di codice sfuggirebbe due volte.

- [ ] **Step 10: Eseguire e vedere il verde**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
dotnet test tests/Recordum.Core.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: `0 Error(s)`; Core `Passed: 149`. **La suite WPF non è eseguibile qui**: la sua verifica è la compilazione, e i suoi test tornano a girare — su Linux — al Task 8.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor(vm): i quattro orologi ricevono il battito invece di costruirlo

Cascata mappata sul codice, non dedotta: tre anelli e poi la DI. Anello 1 sono le
quattro classi che creavano un DispatcherTimer; anello 2 i sette che le costruiscono;
anello 3 HabitScreenViewModel e HabitBuilderViewModel; anello 4 ServiceRegistrar.

La forma decide il rischio, e non è simmetrica di proposito. Sull'anello 1 il
parametro è OBBLIGATORIO: così il compilatore elenca ogni sito di costruzione e
nessun ramo di produzione può restare senza battito in silenzio — che è esattamente
il modo in cui questo repo si è già fatto male quattro volte (i tre VM del WIP di R9
e StateTodayContent in C1, tutti con una firma giusta e un corpo che buttava via il
valore). Sugli anelli 2 e 3 è opzionale, così i ~30 siti nei test WPF compilano
invariati.

SamplePreviewFactory riceve NullTickSourceFactory.Instance, e non è una svista: il
commento accanto prometteva già "Set, never Start(): un timer in moto in un'anteprima
perderebbe memoria". Ora la promessa è nel tipo invece che nel commento.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 5: Le ultime due dipendenze da `Application.Current`

Restano due agganci, di natura diversa: uno è un'**azione** che il ViewModel chiede (uscire), l'altro è un **valore** che legge (un colore del tema). Dopo questo task nessun ViewModel nomina più `Application.Current`.

Il secondo è anche un difetto: `HabitScreenViewModel` risolve `Brush.Danger` **una volta**, al caricamento dei marker del calendario, e ne fotografa l'esadecimale dentro `CalendarDayMarker.TintColorHex`. Se l'utente cambia tema, il calendario tiene il colore vecchio finché non ricarica. La correzione — il ViewModel dichiara *quale* tinta, la view la risolve — toglie l'aggancio **e** il difetto.

**Files:**
- Create: `src/Recordum.Core/Abstractions/IApplicationLifetime.cs`
- Create: `src/Recordum.WPF/Services/WpfApplicationLifetime.cs`
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs:144`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs:13,242,255`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs:960-1032`
- Modify: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml:58,121,182`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Create: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CalendarDangerTintTests.cs`

**Interfaces:**
- Consuma: niente dai task precedenti.
- Produce:
  - `Recordum.Core.Abstractions.IApplicationLifetime` — `void Shutdown()`
  - `CalendarDayMarker` con `MarkerTint Tint` al posto di `string? TintColorHex`
  - `Recordum.Core.Abstractions.MarkerTint` — `enum { Default, Danger }`
  - `CalendarCellViewModel.Tint` (era `TintColorHex`), letta dalla view

- [ ] **Step 1: Scrivere il test che fallisce**

`tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CalendarDangerTintTests.cs`:

```csharp
using Recordum.Core.Abstractions;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class CalendarDangerTintTests
{
    [Fact]
    public void A_lapse_day_declares_the_danger_tint_without_naming_a_color()
    {
        // Il giorno di una ricaduta dichiara la TINTA, non l'esadecimale: risolverlo qui
        // significherebbe (a) chiedere un brush a Application.Current da un ViewModel, e
        // (b) fotografare il colore al caricamento, così che un cambio di tema non lo aggiorni
        // finché il calendario non ricarica.
        var marker = new CalendarDayMarker(
            new DateOnly(2026, 9, 10), TintStep: 4, Number: null, NoteCount: 0, Tint: MarkerTint.Danger);

        Assert.Equal(MarkerTint.Danger, marker.Tint);
    }

    [Fact]
    public void An_ordinary_day_defaults_to_the_neutral_tint()
    {
        var marker = new CalendarDayMarker(new DateOnly(2026, 9, 10), TintStep: 2, Number: null, NoteCount: 0);

        Assert.Equal(MarkerTint.Default, marker.Tint);
    }
}
```

- [ ] **Step 2: Eseguire e vedere il rosso**

```bash
dotnet build tests/Recordum.WPF.Tests -p:EnableWindowsTargeting=true --nologo 2>&1 | grep -E 'error CS' | head -3
```

Atteso: `CS0246` su `MarkerTint` e/o `CS1739` sul parametro `Tint`, che non esistono.

- [ ] **Step 3: Introdurre `MarkerTint` e cambiare il marker**

In `src/Recordum.Core/Abstractions/IApplicationLifetime.cs` (lo stesso file ospita entrambe le astrazioni di questo task — sono le due ultime dipendenze da `Application.Current`, e stanno insieme perché muoiono insieme):

```csharp
namespace Recordum.Core.Abstractions;

/// <summary>Ciò che un ViewModel può chiedere all'applicazione ospitante. Oggi solo l'uscita:
/// prima era `Application.Current?.Shutdown()`, che lega il ViewModel a WPF per una riga.</summary>
public interface IApplicationLifetime
{
    void Shutdown();
}

/// <summary>Quale tinta un marker del calendario chiede, senza sapere di che colore sia. La view
/// la risolve contro il tema corrente, quindi un cambio di tema si vede subito invece che alla
/// prossima ricarica.</summary>
public enum MarkerTint
{
    /// <summary>La tinta di completamento della propria abitudine (il default del navigatore).</summary>
    Default,

    /// <summary>Il giorno di una ricaduta.</summary>
    Danger,
}
```

In `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs:13`:

```csharp
public sealed record CalendarDayMarker(DateOnly Date, int TintStep, string? Number, int NoteCount, MarkerTint Tint = MarkerTint.Default);
```

con `using Recordum.Core.Abstractions;` in testa, e il commento XML sopra il record aggiornato: `Tint` sceglie fra il colore di completamento del navigatore e quello di pericolo — non "sovrascrive `CompletedColorHex`".

- [ ] **Step 4: Il ViewModel dichiara la tinta invece di risolverla**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`, **cancella** il blocco:

```csharp
        // Resolve danger color from the WPF resource dictionary (null in headless/unit-test context).
        const string FallbackDangerHex = "#FFF87171";
        var dangerHex = FallbackDangerHex;
        var dangerBrush = System.Windows.Application.Current?.TryFindResource("Brush.Danger")
            as System.Windows.Media.SolidColorBrush;
        if (dangerBrush is not null)
            dangerHex = dangerBrush.Color.ToString(System.Globalization.CultureInfo.InvariantCulture); // #AARRGGBB e.g. "#FFF87171"
```

e sostituisci i tre usi di `dangerHex`:

```csharp
                    days.Add(new CalendarDayMarker(day, TintStep: 4, Number: null, NoteCount: noteCount, Tint: MarkerTint.Danger));
```
```csharp
                    days.Add(new CalendarDayMarker(day, TintStep: 3, Number: null, NoteCount: noteCount));
```
```csharp
                days.Add(new CalendarDayMarker(day, TintStep: 1, Number: null, NoteCount: noteCount, Tint: MarkerTint.Danger));
```

- [ ] **Step 5: La cella porta la tinta ACCANTO al colore, non al suo posto**

Attenzione a un dettaglio del modello che è facile sbagliare: `CalendarCellViewModel.TintColorHex` **non** era solo il colore di pericolo. Per un giorno normale è `CompletedColorHex`, cioè **il colore dell'abitudine scelto dall'utente** — dato vero, non un token di tema, e deve restare un esadecimale. Solo il ramo "ricaduta" lo sostituiva con un colore preso dal dizionario risorse.

Quindi la cella **tiene** `TintColorHex` e **guadagna** `MarkerTint Tint`. In `CalendarNavigatorViewModel.cs`, alle righe ~242 e ~255:

```csharp
            cell.TintColorHex = CompletedColorHex;
            cell.Tint = MarkerTint.Default;
```
```csharp
                cell.TintColorHex = CompletedColorHex;   // sempre il colore dell'abitudine
                cell.Tint = m.Tint;                      // la tinta decide se sovrascriverlo
```

Il converter è **`HabitColorTintConverter`** (`src/Recordum.WPF/Converters/HabitColorTintConverter.cs`), un `IMultiValueConverter` che oggi riceve `[string hex, int step]` e restituisce il colore dell'abitudine ad alpha crescente. Guadagna un terzo valore e un ramo:

```csharp
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string hex || values[1] is not int step)
            return Brushes.Transparent;
        step = Math.Clamp(step, 0, 4);
        if (step == 0) return Brushes.Transparent;

        // Terzo valore: la tinta richiesta. Danger risolve Brush.Danger contro il tema CORRENTE,
        // invece di ricevere un esadecimale fotografato al caricamento dei marker — che è il
        // motivo per cui, cambiando tema, il calendario teneva il colore vecchio.
        if (values.Length > 2 && values[2] is MarkerTint.Danger)
        {
            if (Application.Current?.TryFindResource("Brush.Danger") is SolidColorBrush danger)
                hex = danger.Color.ToString(CultureInfo.InvariantCulture);
        }

        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(Color.FromArgb(Alpha[step], c.R, c.G, c.B));
            brush.Freeze();
            return brush;
        }
        catch (FormatException) { return Brushes.Transparent; }
    }
```

In `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`, ai **tre** punti (righe ~58, ~121, ~182) il MultiBinding guadagna un terzo binding:

```xml
                            <MultiBinding Converter="{StaticResource HabitColorTint}">
                                <Binding Path="TintColorHex"/>
                                <Binding Path="TintStep"/>
                                <Binding Path="Tint"/>
                            </MultiBinding>
```

`tests/Recordum.WPF.Tests/Converters/HabitColorTintConverterTests.cs` chiama `Convert` con un array di due elementi: continua a compilare e a passare (il ramo nuovo è dietro `values.Length > 2`), ma **aggiungi un caso** che passa `MarkerTint.Danger` e verifica che il colore restituito non sia quello dell'abitudine. Quel file è uno dei 23 che restano in `Recordum.WPF.Tests`, quindi la sua verifica è la compilazione qui e l'esecuzione sulla VM.

- [ ] **Step 6: Il comando Quit non conosce più WPF**

`src/Recordum.WPF/Services/WpfApplicationLifetime.cs`:

```csharp
using Recordum.Core.Abstractions;

namespace Recordum.WPF.Services;

/// <summary>IApplicationLifetime su WPF. Muore con la shell; la gemella Avalonia chiuderà
/// l'IClassicDesktopStyleApplicationLifetime, che è un oggetto diverso con lo stesso mestiere.</summary>
public sealed class WpfApplicationLifetime : IApplicationLifetime
{
    public void Shutdown() => System.Windows.Application.Current?.Shutdown();
}
```

In `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs`, il campo e il comando:

```csharp
    private readonly IApplicationLifetime _lifetime;
```
```csharp
    [RelayCommand]
    private void Quit() => _lifetime.Shutdown();
```

(Il comando smette di essere `static`: ora usa un campo di istanza. `[RelayCommand]` genera `QuitCommand` esattamente come prima, quindi nessun binding cambia.) Assegna `_lifetime` nel costruttore da un parametro `IApplicationLifetime lifetime`.

In `ServiceRegistrar.cs`:

```csharp
        services.AddSingleton<IApplicationLifetime, WpfApplicationLifetime>();
```

- [ ] **Step 7: Aggiustare i siti di costruzione di `MainWindowViewModel` nei test**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo 2>&1 | grep -E 'error CS' | head -10
```

Ogni errore nomina il file e la riga. Nei test passa una lambda-fake:

```csharp
private sealed class FakeApplicationLifetime : IApplicationLifetime
{
    public int ShutdownCount { get; private set; }
    public void Shutdown() => ShutdownCount++;
}
```

Mettila in `tests/Recordum.WPF.Tests/Fixtures/FakeApplicationLifetime.cs` (si sposta al Task 8 con le altre fixture).

- [ ] **Step 8: Verificare che `Application.Current` sia sparito dai ViewModel**

```bash
grep -rn 'Application\.Current' src/Recordum.WPF/ViewModels --include='*.cs'
```

Atteso: **nessun output**.

- [ ] **Step 9: Eseguire e vedere il verde**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
```

Atteso: `0 Error(s)`.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor(vm): le ultime due dipendenze da Application.Current, e un difetto per strada

Due agganci di natura diversa. Quit chiedeva un'AZIONE all'applicazione ospitante
(uscire) e ora la chiede a IApplicationLifetime. Il calendario leggeva un VALORE —
il brush Brush.Danger — dal dizionario risorse di WPF, dentro un ViewModel.

Il secondo era anche un difetto: HabitScreenViewModel risolveva il colore UNA volta,
al caricamento dei marker, e ne fotografava l'esadecimale dentro
CalendarDayMarker.TintColorHex. Cambiando tema, il calendario teneva il colore
vecchio finché non ricaricava. Ora il ViewModel dichiara QUALE tinta (MarkerTint.
Danger) e la view la risolve contro il tema corrente: sparisce l'aggancio, sparisce
il difetto, e sparisce anche l'esadecimale di ripiego "#FFF87171" scritto a mano
accanto a un token che esisteva già.

Nessun ViewModel nomina più Application.Current: `grep -rn 'Application\.Current'
src/Recordum.WPF/ViewModels` torna vuoto. Restano le sei cuciture chiuse su sette.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 6: `DropPlan` esce dal behavior

La settima cucitura, e la più piccola: `HomeViewModel.ApplyDropAsync` prende un `TreeDragDropBehavior.DropPlan`, cioè un tipo annidato in una classe piena di `System.Windows`. Ma `DropPlan` è un `readonly record struct` di tre campi e `ResolveDrop` è uno `switch` su un enum: **non c'è niente di WPF dentro**, solo il file sbagliato. Sono anche gli ultimi due test di ViewModel che il Task 8 non potrebbe altrimenti liberare.

**Files:**
- Create: `src/Recordum.WPF/ViewModels/DropPlan.cs`
- Modify: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs:13-52`
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs:244`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HomeDropDispatchTests.cs`, `tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs`

**Interfaces:**
- Consuma: niente.
- Produce: `Recordum.WPF.ViewModels.DropZone` (enum), `Recordum.WPF.ViewModels.DropPlan` (record struct), `Recordum.WPF.ViewModels.DropResolver.ResolveDrop(DropZone, bool, Guid, Guid?)`. Al Task 7 diventano `Recordum.Presentation.ViewModels.*`.

- [ ] **Step 1: Scrivere il file nuovo**

`src/Recordum.WPF/ViewModels/DropPlan.cs`:

```csharp
namespace Recordum.WPF.ViewModels;

/// <summary>Dove sta il cursore rispetto alla riga sotto di esso.</summary>
public enum DropZone
{
    Before,
    Onto,
    After,
}

/// <summary>Intenzione di rilascio risolta: nuovo genitore, il fratello a cui ancorarsi
/// (null = in coda), e da che lato.</summary>
public readonly record struct DropPlan(Guid? Parent, Guid? Anchor, bool PlaceAfter);

/// <summary>La mappatura pura da zona+bersaglio a piano di rilascio. Viveva dentro
/// TreeDragDropBehavior, che è pieno di System.Windows, ma non ha mai avuto niente di WPF dentro:
/// è uno switch su un enum. Il behavior resta nella shell perché maneggia eventi del mouse; questo
/// va con i ViewModel, che è dove viene consumato.</summary>
public static class DropResolver
{
    /// <summary>Su un gruppo → in coda dentro di esso. Su un'abitudine → fratello subito dopo.
    /// Before/After → riordino attorno al bersaglio.</summary>
    public static DropPlan ResolveDrop(DropZone zone, bool targetIsGroup, Guid targetId, Guid? targetParentId) => zone switch
    {
        DropZone.Onto when targetIsGroup => new DropPlan(targetId, null, false),
        DropZone.Before => new DropPlan(targetParentId, targetId, false),
        // After, e Onto-su-un'abitudine, mettono entrambi la riga subito dopo il bersaglio fra i suoi fratelli.
        _ => new DropPlan(targetParentId, targetId, true),
    };
}
```

- [ ] **Step 2: Togliere i tre membri dal behavior**

In `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs`: cancella l'`enum DropZone`, il `readonly record struct DropPlan` e il metodo `ResolveDrop`. Aggiungi `using Recordum.WPF.ViewModels;` in testa (c'è già). Il resto del file continua a usare `DropZone` e `DropPlan` con lo stesso nome, ora risolti dal namespace dei ViewModel.

- [ ] **Step 3: Correggere il consumatore**

In `src/Recordum.WPF/ViewModels/HomeViewModel.cs:244`:

```csharp
    public async Task ApplyDropAsync(HomeRowViewModel src, DropPlan plan)
```

- [ ] **Step 4: Correggere i test**

```bash
cd /home/lucafrog/dev/recordum
sed -i 's/TreeDragDropBehavior\.DropPlan/DropPlan/g; s/TreeDragDropBehavior\.DropZone/DropZone/g; s/TreeDragDropBehavior\.ResolveDrop/DropResolver.ResolveDrop/g' \
  tests/Recordum.WPF.Tests/ViewModels/HomeDropDispatchTests.cs \
  tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs
grep -rn 'TreeDragDropBehavior\.\(DropPlan\|DropZone\|ResolveDrop\)' src tests --include='*.cs' | grep -v '/obj/'
```

Atteso dall'ultimo `grep`: **nessun output**. Aggiungi `using Recordum.WPF.ViewModels;` ai due file di test se il compilatore lo chiede.

- [ ] **Step 5: Verificare che `HomeDropDispatchTests` non tocchi più WPF**

```bash
grep -c 'System\.Windows\|Recordum\.WPF\.Behaviors' tests/Recordum.WPF.Tests/ViewModels/HomeDropDispatchTests.cs
```

Atteso: `0`. È il criterio per cui questo task esiste — quel file e `HomeViewModelTests` erano gli **unici due** dei 101 test di ViewModel legati a Windows.

- [ ] **Step 6: Eseguire e vedere il verde**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
```

Atteso: `0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor(vm): DropPlan esce dal behavior, ed è l'ultima delle sette cuciture

HomeViewModel.ApplyDropAsync prendeva un TreeDragDropBehavior.DropPlan, cioè un tipo
annidato in una classe piena di System.Windows. Ma DropPlan è un readonly record
struct di tre campi e ResolveDrop è uno switch su un enum: non c'è mai stato niente
di WPF dentro, solo il file sbagliato. Il behavior resta nella shell perché maneggia
eventi del mouse; la mappatura pura va con chi la consuma.

Il guadagno misurato: HomeViewModelTests e HomeDropDispatchTests erano gli UNICI due
file, su 101 test di ViewModel, a nominare un tipo Windows. Con questo il conto
scende a zero, e il Task 8 può spostarli tutti.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 7: La traslocazione del codice

Le sette cuciture sono tagliate. Ora si sposta. La regola del confine è **"tocca `System.Windows` → resta nella shell"**, e la impone il compilatore: `Recordum.Presentation` è `net10.0`, quindi un file sbagliato non compila.

**Files:**
- Move: `src/Recordum.WPF/ViewModels/**` (126 file) → `src/Recordum.Presentation/ViewModels/`
- Move: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs` → `src/Recordum.Presentation/Resources/`
- Move: le 10 interfacce di `src/Recordum.WPF/Services/` + le 8 implementazioni pure → `src/Recordum.Presentation/Services/`
- Move: `src/Recordum.WPF/Bootstrap/{ApplicationInitializer,ServiceRegistrar,RecordumOptions,DemoDataSeeder,BackupSchedulerHost,BackupSchedulerOptions}.cs` → `src/Recordum.Presentation/Bootstrap/`
- Modify: ogni file di `src/Recordum.WPF/` e `src/Recordum.UI/` che importa quei namespace

**Interfaces:**
- Consuma: tutto ciò che i task 1-6 hanno prodotto.
- Produce: i namespace `Recordum.Presentation.ViewModels.*`, `.Resources`, `.Services`, `.Bootstrap`. Il Task 8 sposta i test contro **questi** nomi.

- [ ] **Step 1: Spostare i ViewModel**

```bash
cd /home/lucafrog/dev/recordum
git mv src/Recordum.WPF/ViewModels src/Recordum.Presentation/ViewModels
```

- [ ] **Step 2: Spostare le stringhe**

```bash
cd /home/lucafrog/dev/recordum
mkdir -p src/Recordum.Presentation/Resources
git mv src/Recordum.WPF/Resources/Strings.resx          src/Recordum.Presentation/Resources/
git mv src/Recordum.WPF/Resources/Strings.it.resx       src/Recordum.Presentation/Resources/
git mv src/Recordum.WPF/Resources/Strings.Designer.cs   src/Recordum.Presentation/Resources/
```

(I cinque dizionari XAML — `BaseStyles`, `Tokens`, `Light`, `Dark`, `Icons`, `WindowChromeStyles` — **restano** in `src/Recordum.WPF/Resources/`: sono WPF e muoiono con lui.)

- [ ] **Step 3: Spostare i servizi portabili**

```bash
cd /home/lucafrog/dev/recordum
mkdir -p src/Recordum.Presentation/Services
for f in IAutoBackupKeyStore IBackupSaveFileDialog IClipboard IConfirmationDialog IFavoriteColorStore \
         IOpenFileDialog IRecentColorStore ISaveFileDialog IScreenColorPicker ISettingsWindowLauncher \
         BackupSchedulerRunnerAdapter DebouncedColorRecorder FavoriteColorStore HsvColor IconCatalog \
         RecentColorStore ShadeRamp TimerRecoveryService; do
  git mv "src/Recordum.WPF/Services/$f.cs" "src/Recordum.Presentation/Services/$f.cs"
done
ls src/Recordum.WPF/Services/
```

Atteso: restano **10** file, tutti quelli che toccano `System.Windows` — `ScreenColorPicker`, `MessageBoxConfirmationDialog`, `UiEventSubscriptionExtensions`, le sei `Wpf*`, più `DispatcherTickSource` e `WpfApplicationLifetime` creati ai task 3 e 5.

- [ ] **Step 4: Spostare il bootstrap portabile**

```bash
cd /home/lucafrog/dev/recordum
mkdir -p src/Recordum.Presentation/Bootstrap
for f in ApplicationInitializer BackupSchedulerHost BackupSchedulerOptions DemoDataSeeder RecordumOptions ServiceRegistrar; do
  git mv "src/Recordum.WPF/Bootstrap/$f.cs" "src/Recordum.Presentation/Bootstrap/$f.cs"
done
ls src/Recordum.WPF/Bootstrap/
```

Atteso: restano **`CultureBootstrap.cs`** e **`IWebView2Availability.cs`**. Il secondo muore a `F4`.

- [ ] **Step 5: Riscrivere i namespace ovunque**

(La sostituzione è **globale sul file**, non limitata alle righe `using`: un riferimento pienamente qualificato in mezzo a un'espressione — il Task 2 ne ha trovato uno in `StreakSummaryViewModelTests.cs:150` — verrebbe altrimenti mancato in silenzio.)

```bash
cd /home/lucafrog/dev/recordum
for ns in ViewModels Resources Services Bootstrap; do
  grep -rl "Recordum\.WPF\.$ns" src tests --include='*.cs' --include='*.xaml' --include='*.axaml' --include='*.resx' \
    | grep -v '/obj/' | grep -v '/bin/' \
    | xargs -r sed -i "s/Recordum\.WPF\.$ns/Recordum.Presentation.$ns/g"
done
```

**Attenzione ai due casi che questa sostituzione NON copre**, e che vanno fatti a mano:

1. `src/Recordum.WPF/Resources/Strings.Designer.cs` è ora in Presentation ma il suo `ResourceManager` nomina il vecchio assembly. Correggi la stringa di risorsa dentro il file, **via shell** (`guard-generated.js` blocca Edit/Write su `*.Designer.cs`):

```bash
sed -i 's/"Recordum\.WPF\.Resources\.Strings"/"Recordum.Presentation.Resources.Strings"/' \
  src/Recordum.Presentation/Resources/Strings.Designer.cs
grep -n 'ResourceManager\|namespace' src/Recordum.Presentation/Resources/Strings.Designer.cs | head -5
```

2. Le view WPF e le `.axaml` che dichiarano `xmlns:vm="clr-namespace:Recordum.WPF.ViewModels..."` hanno bisogno anche di `;assembly=Recordum.Presentation`, perché il namespace non vive più nell'assembly della view:

```bash
grep -rn 'clr-namespace:Recordum\.Presentation' src --include='*.xaml' --include='*.axaml' | grep -v ';assembly=' | head -20
```

Ogni riga trovata va completata con `;assembly=Recordum.Presentation`.

- [ ] **Step 6: Compilare, e usare gli errori come elenco**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo 2>&1 | grep -E 'error' | head -20
```

Ogni errore nomina un file. Le **tre** famiglie attese:

- `CS0246` in `src/Recordum.Presentation/`: **un file è entrato e non doveva** — usa un tipo WPF. Riportalo in `src/Recordum.WPF/` e annota perché.
- `CS0246`/`MC3074` in `src/Recordum.WPF/` o `.axaml`: manca un `using` o un `;assembly=`. Aggiungilo.
- **`CS0103`/`CS0246` su un nome scritto senza `using` e senza qualificazione completa** — es. `Converters.HabitColorTint` dentro un file del namespace `Recordum.Presentation.ViewModels.*`, o `ViewModels.HomeRowViewModel` dentro un file rimasto in `Recordum.WPF.*`. **La sed dello Step 5 è cieca a questi**, perché nel testo non compare mai la stringa `Recordum.WPF.` da sostituire: il riferimento funzionava per la **ricerca nel namespace contenitore** di C#, cioè risaliva da `Recordum.WPF.ViewModels.Foo` a `Recordum.WPF` e trovava lì il fratello. Spezzato l'albero in due assembly, quella risalita non arriva più a destinazione. **Si corregge aggiungendo lo `using` esplicito**, mai spostando il file per far tornare la risalita.

  *Non è un'ipotesi*: il Task 2, che ha mosso **due soli file**, ne ha incontrati tre — `HomeRowViewModel.cs`, `HabitScreenViewModel.cs`, `CounterTodayContent.cs` nominavano `Formatting.CounterTotalDisplay` esattamente così. Muovendo 126 ViewModel più servizi, stringhe e bootstrap, aspettatene molti di più.

  Le coppie a rischio sono quelle che questo task **separa**: un ViewModel che risale a `Converters`, `Behaviors`, `Selectors`, `Theming`, `Helpers`, `Interop`; e all'inverso una view, un converter o un behavior rimasti in WPF che risalgono a `ViewModels`. Ciò che trasloca *insieme* (ViewModel, `Resources`, `Formatting`, e le metà portabili di `Services` e `Bootstrap`) si ripara da sé, perché la risalita trova il fratello sotto il nuovo tetto `Recordum.Presentation`.

Ripeti finché la build è pulita. **Non aggiungere `-windows` a `Recordum.Presentation` per far sparire un errore**: quell'errore è il confine che fa il suo lavoro.

- [ ] **Step 7: Verificare che le sette cuciture siano davvero chiuse**

```bash
cd /home/lucafrog/dev/recordum
echo "--- System.Windows dentro Presentation (deve essere vuoto) ---"
grep -rn 'System\.Windows' src/Recordum.Presentation --include='*.cs' | grep -v '/obj/'
echo "--- quanti file sono in Presentation ---"
find src/Recordum.Presentation -name '*.cs' -not -path '*/obj/*' | wc -l
```

Atteso: nessun output dal primo; circa **160** file dal secondo (126 ViewModel + 18 servizi + 6 bootstrap + 2 formatting + Strings.Designer + i nuovi).

- [ ] **Step 8: Eseguire e vedere il verde**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
dotnet test tests/Recordum.Core.Tests --nologo -v q 2>&1 | tail -2
dotnet test tests/Recordum.Data.Tests --nologo -v q 2>&1 | tail -2
dotnet test tests/Recordum.UI.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: `0 Error(s)`; Core 149; Data 478 passati + 18 falliti DPAPI (Windows-only per natura, **è la baseline**, non una regressione); UI 13.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor(presentation): 126 ViewModel, 504 stringhe e il bootstrap portabile traslocano

Le sette cuciture sono tagliate (task 3-6), quindi si sposta. Il confine non è una
convenzione da ricordare: Recordum.Presentation è net10.0 senza -windows, quindi un
file che tocca System.Windows semplicemente non compila lì. La regola "tocca
System.Windows → resta nella shell" la impone il compilatore.

Cosa resta in Recordum.WPF, e perché: i 37 converter (restituiscono Brush e
Visibility), i 4 behavior, le 10 implementazioni di servizio che parlano con Win32 o
con i dialog di sistema, CultureBootstrap (FrameworkElement.Language è un meccanismo
WPF) e IWebView2Availability, che muore a F4 quando l'editor perde WebView2.

I cinque dizionari XAML restano dov'erano: sono WPF, muoiono con lui. Le stringhe no
— 504 chiavi importate da 38 ViewModel, che sono la cosa più referenziata di tutte.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 8: I test tornano a girare — su Linux

178 file di test su 201 non nominano un tipo Windows. Si spostano in `Recordum.Presentation.Tests`, che è `net10.0`, e da qui in poi **girano su questa macchina**. È il guadagno che vale a prescindere dall'esito della migrazione.

**Files:**
- Move: 178 file da `tests/Recordum.WPF.Tests/` → `tests/Recordum.Presentation.Tests/`
- Modify: `tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj` (aggiunge il riferimento a Presentation)
- Create: `tests/Recordum.Presentation.Tests/TickingClockTests.cs`

**Interfaces:**
- Consuma: i namespace `Recordum.Presentation.*` del Task 7; `FakeTickSourceFactory` del Task 3.
- Produce: un conteggio di test verde su Linux, e la nuova riga di baseline da scrivere nel commit finale.

- [ ] **Step 1: Elencare i file da spostare**

```bash
cd /home/lucafrog/dev/recordum
WPFISH='System\.Windows|Recordum\.WPF\.(Views|Controls|Behaviors|Converters|Selectors|Theming)|STAThread|StaFact'
find tests/Recordum.WPF.Tests -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' \
  | while read -r f; do grep -qE "$WPFISH" "$f" || echo "$f"; done > ./to-move.txt
wc -l < ./to-move.txt
```

(`to-move.txt` sta nella radice del repo e si cancella allo Step 2: è già coperto da `.gitignore`? **No** — cancellalo esplicitamente prima del commit, o `git add -A` se lo porterebbe dietro.)

Atteso: circa **178** righe. (Il filtro include `Theming` e `StaFact` oltre a quelli misurati nella spec: sono file che il compilatore rifiuterebbe comunque, ed è meglio scoprirlo qui che dopo lo spostamento.)

- [ ] **Step 2: Spostarli conservando la struttura di cartelle**

```bash
cd /home/lucafrog/dev/recordum
while read -r f; do
  dest="tests/Recordum.Presentation.Tests/${f#tests/Recordum.WPF.Tests/}"
  mkdir -p "$(dirname "$dest")"
  git mv "$f" "$dest"
done < ./to-move.txt
rm -f ./to-move.txt
find tests/Recordum.WPF.Tests -type d -empty -delete
```

- [ ] **Step 3: Riscrivere i namespace dei file spostati**

```bash
cd /home/lucafrog/dev/recordum
grep -rl 'Recordum\.WPF\.Tests' tests/Recordum.Presentation.Tests --include='*.cs' \
  | xargs -r sed -i 's/Recordum\.WPF\.Tests/Recordum.Presentation.Tests/g'
# I file rimasti in WPF.Tests che usavano una fixture ora spostata devono importarla.
grep -rn 'Recordum\.Presentation\.Tests' tests/Recordum.WPF.Tests --include='*.cs' | head
```

- [ ] **Step 4: Cablare i due riferimenti fra progetti di test**

I 23 file rimasti in `Recordum.WPF.Tests` usano fixture che ora vivono in `Recordum.Presentation.Tests`. In `tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`, aggiungi:

```xml
    <ProjectReference Include="..\Recordum.Presentation.Tests\Recordum.Presentation.Tests.csproj" />
```

E `TickingClockTests` (Step 6) usa `FakeTickSourceFactory`, che vive in `Recordum.Core.Tests` perché è il doppio di un'astrazione di Core ed è lì che il Task 3 la prova. In `tests/Recordum.Presentation.Tests/Recordum.Presentation.Tests.csproj`:

```xml
    <ProjectReference Include="..\Recordum.Core.Tests\Recordum.Core.Tests.csproj" />
```

(Un progetto di test che ne referenzia un altro è lo schema che questo stesso task introduce una riga più su per le fixture: entrambi sono xunit 2.9.3, quindi si vedono. Il vincolo che NON si può violare è mescolare le due major — `Recordum.UI.Tests` è xunit.v3 e resta isolato.)

- [ ] **Step 5: Compilare e correggere**

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo 2>&1 | grep -E 'error' | head -20
```

Se un file spostato non compila in `net10.0`, **è finito nella cartella sbagliata**: riportalo in `tests/Recordum.WPF.Tests/` con `git mv` e ripristina il suo namespace. È il confine che fa il suo lavoro, non un problema da aggirare.

- [ ] **Step 6: Scrivere il test che prima non si poteva scrivere**

`tests/Recordum.Presentation.Tests/TickingClockTests.cs`:

```csharp
using Recordum.Core.Abstractions;
using Recordum.Core.Tests.Abstractions;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.Home;

namespace Recordum.Presentation.Tests;

/// <summary>Prima di ITickSource questi due comportamenti NON erano provabili: il timer nasceva
/// solo sotto un'applicazione WPF viva, quindi in headless non batteva e i test potevano al
/// massimo pilotare il fan-out a mano. Ora il battito è iniettato, quindi si prova davvero.</summary>
public class TickingClockTests
{
    [Fact]
    public void Home_ticker_fans_a_real_tick_out_to_every_registered_row()
    {
        var ticks = new FakeTickSourceFactory();
        using var sut = new HomeClockTicker(ticks);
        var a = 0;
        var b = 0;
        sut.Register(() => a++);
        sut.Register(() => b++);

        ticks.TickAll();

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public void Live_clock_refreshes_its_display_on_a_real_tick()
    {
        var ticks = new FakeTickSourceFactory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-10T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        using var sut = new LiveElapsedClock(clock, ticks);

        sut.Start(clock.UtcNowMs - 5_000);
        var atStart = sut.Display;

        clock.Advance(TimeSpan.FromSeconds(60));
        ticks.TickAll();

        // Il default (nessun battito) darebbe atStart == sut.Display: il test discrimina perché
        // il tempo avanza E la sorgente batte. Senza una delle due cose non proverebbe niente.
        Assert.NotEqual(atStart, sut.Display);
    }
}
```

**Se `FakeClock` non espone `Advance`**, usa il metodo equivalente già presente nelle fixture spostate — il nome esatto si legge in `tests/Recordum.Presentation.Tests/Fixtures/FakeClock.cs`, e il test va adattato a quello, non viceversa.

- [ ] **Step 7: Provare i due test contro il codice rotto**

```bash
cd /home/lucafrog/dev/recordum
# Rompi: la sorgente non viene mai avviata.
sed -i 's/_tick?.Start();/\/\/ROTTO _tick?.Start();/' src/Recordum.Presentation/ViewModels/Home/HomeClockTicker.cs
dotnet test tests/Recordum.Presentation.Tests --filter 'FullyQualifiedName~TickingClockTests' --nologo -v q 2>&1 | tail -3
```

Atteso: **rosso** su `Home_ticker_fans_a_real_tick_out_to_every_registered_row`. Poi ripristina:

```bash
sed -i 's/\/\/ROTTO _tick?.Start();/_tick?.Start();/' src/Recordum.Presentation/ViewModels/Home/HomeClockTicker.cs
dotnet test tests/Recordum.Presentation.Tests --filter 'FullyQualifiedName~TickingClockTests' --nologo -v q 2>&1 | tail -2
```

Atteso: verde. **Un test di regressione che non è stato visto fallire non è un test.**

- [ ] **Step 8: Eseguire tutto e scrivere la nuova baseline**

```bash
cd /home/lucafrog/dev/recordum
for p in Core Data Plugins Plugins.ExportEngine Presentation UI; do
  printf "%-24s " "$p"
  dotnet test "tests/Recordum.$p.Tests" --nologo -v q 2>&1 | grep -oE 'Failed:\s+[0-9]+, Passed:\s+[0-9]+' | tail -1
done
```

Annota i numeri: vanno nel messaggio di commit e diventano la baseline dei task 9-10 e delle onde successive.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
test(presentation): 178 file di test escono da Windows e girano su Linux

Su 201 file di test, 23 nominano un tipo Windows. Gli altri 178 non avevano nessun
motivo di essere inchiodati a net10.0-windows: ci stavano perché i ViewModel che
provano ci stavano. Dentro ViewModels/ il rapporto era 99 su 101, e i due che
mancavano erano esattamente la cucitura DropPlan, chiusa al Task 6.

Da qui in poi la macchina su cui si sviluppa può verificare i ViewModel a cui sta
cablando le view. È il guadagno che vale a prescindere dall'esito della migrazione:
varrebbe la pena anche se Avalonia non esistesse.

TickingClockTests prova due comportamenti che PRIMA non si potevano scrivere: il
timer nasceva solo sotto un'applicazione WPF viva, quindi in headless non batteva e
i test potevano solo pilotare il fan-out a mano. Provati contro il codice rotto —
tolto lo Start(), rosso; ripristinato, verde — perché un test di regressione mai
visto fallire non è un test.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 9: Il font di spedizione

Palatino non esiste su Linux, e un ripiego per-OS farebbe sembrare l'app diversa su ogni macchina — esattamente ciò che l'Onda 3 vuole smettere di fare. Si imbusta **una** faccia serif nell'assembly (spec §3.2). La faccia la sceglie l'autore; il meccanismo lo fissa questo task.

**Files:**
- Create: `src/Recordum.UI/Fonts/<Nome>.ttf` (+ il file di licenza accanto)
- Modify: `src/Recordum.UI/Recordum.UI.csproj`
- Modify: `src/Recordum.UI/Themes/Tokens.axaml`
- Create: `tests/Recordum.UI.Tests/EmbeddedFontTests.cs`

**Interfaces:**
- Consuma: `Recordum.UI` (Task 1), le chiavi `Font.Default`/`Font.Serif` già in `Tokens.axaml`.
- Produce: la risorsa `avares://Recordum.UI/Fonts/#<Family Name>`, usata da `F1` in poi per ogni testo.

- [ ] **Step 1: Scegliere la faccia e verificarne la licenza**

Chiedi all'autore quale serif imbustare. Requisito **non negoziabile**: licenza che consente la ridistribuzione dentro un'applicazione (SIL OFL o Apache-2.0 vanno bene; una licenza desktop-only no). Candidate ragionevoli: **EB Garamond**, **Source Serif 4**, **Libre Baskerville** — tutte OFL. Metti il file di licenza accanto al `.ttf`, non solo un riferimento.

- [ ] **Step 2: Imbustare il font**

```bash
mkdir -p src/Recordum.UI/Fonts
# copia qui il .ttf scelto e il suo OFL.txt
```

In `src/Recordum.UI/Recordum.UI.csproj`:

```xml
  <ItemGroup>
    <!-- Il font viaggia dentro l'assembly: una faccia sola, identica su Linux, Windows e macOS.
         Un ripiego per-OS (Palatino qui, Noto Serif là) darebbe a Recordum un aspetto diverso su
         ogni macchina, che è il contrario di ciò che l'Onda 3 chiede. -->
    <AvaloniaResource Include="Fonts/**" />
  </ItemGroup>
```

- [ ] **Step 3: Puntarci i token**

In `src/Recordum.UI/Themes/Tokens.axaml`, sostituisci lo stack serif di ripiego della sonda con la risorsa imbustata:

```xml
    <!-- Il serif di spedizione, imbustato in Fonts/ e servito da avares://. Lo stack di sistema
         resta come ultima spiaggia se la risorsa non si risolve, mai come prima scelta. -->
    <FontFamily x:Key="Font.Default">avares://Recordum.UI/Fonts/#<Family Name>,Georgia,Noto Serif,Liberation Serif,serif</FontFamily>
```

(`<Family Name>` è il **nome della famiglia dentro il file**, non il nome del file: leggilo con `fc-query src/Recordum.UI/Fonts/<file>.ttf | head -3`.)

- [ ] **Step 4: Scrivere il test**

`tests/Recordum.UI.Tests/EmbeddedFontTests.cs`:

```csharp
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Recordum.UI.Tests;

public class EmbeddedFontTests
{
    [AvaloniaFact]
    public void The_shipped_serif_resolves_from_the_assembly_and_is_not_a_system_fallback()
    {
        var family = (FontFamily)global::Avalonia.Application.Current!.FindResource("Font.Default")!;

        // Il nome della risorsa deve nominare l'assembly: se un domani qualcuno lo sostituisse con
        // uno stack di sistema, l'app tornerebbe ad avere un aspetto diverso su ogni macchina e
        // nessuno se ne accorgerebbe finché non aprisse l'app su un OS diverso.
        Assert.Contains("avares://Recordum.UI/Fonts/", family.Key?.Source?.ToString() ?? family.Name, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 5: Eseguire e vedere il verde**

```bash
dotnet test tests/Recordum.UI.Tests --nologo -v q 2>&1 | tail -2
```

Atteso: 14 test verdi. Se l'assert non regge perché `FontFamily.Key` è null per una famiglia risolta, adatta l'assert a **ciò che Avalonia espone davvero** — leggilo stampando `family.ToString()` in un test usa-e-getta, e correggi il test, non il codice.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
feat(ui): il serif di spedizione viaggia dentro l'assembly

Palatino non esiste su Linux. Un ripiego per-OS — Palatino qui, Noto Serif là — è
la strada che non costa niente e che darebbe a Recordum un aspetto diverso su ogni
macchina: il contrario esatto di ciò che l'Onda 3 (coerenza estetica) chiede.

Una faccia sola, imbustata come AvaloniaResource e servita da avares://. Lo stack di
sistema resta come ultima spiaggia, mai come prima scelta. La licenza consente la
ridistribuzione ed è depositata accanto al file, non solo citata.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

### Task 10: `YearHeatmapControl` e `StreakTimelineControl`

I due controlli non-grafico che mancano (`HeatmapStrip` è già portato dalla sonda). Nessuno dei due usa disegno immediato: il primo è XAML dichiarativo puro, il secondo riempie un `Canvas` in code-behind. I due wrapper LiveCharts restano a `F7`, dove il motore si spezza.

**Files:**
- Create: `src/Recordum.UI/Controls/YearHeatmapControl.axaml` + `.axaml.cs`
- Create: `src/Recordum.UI/Controls/StreakTimelineControl.axaml` + `.axaml.cs`
- Create: `src/Recordum.UI/Converters/IntensityStepToBrushConverter.cs`
- Create: `tests/Recordum.UI.Tests/ControlsScreenshotTests.cs`

**Interfaces:**
- Consuma: `Recordum.Presentation.ViewModels.HabitScreen.HeatmapCellViewModel` e `.Detail.StreakTimelineViewModel` (Task 7); `ScreenshotProbeTests.Capture(Window, string)` già in `tests/Recordum.UI.Tests`.
- Produce: `Recordum.UI.Controls.YearHeatmapControl`, `Recordum.UI.Controls.StreakTimelineControl`. `F3` li monta nella colonna Statistiche.

- [ ] **Step 1: Portare il converter**

`src/Recordum.UI/Converters/IntensityStepToBrushConverter.cs`:

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Recordum.UI.Converters;

/// <summary>Porting diretto del gemello WPF. Due differenze di meccanismo, nessuna di logica:
/// IValueConverter viene da Avalonia.Data.Converters, e la ricerca della risorsa passa da
/// Application.Current.TryFindResource(key, theme, out value), che vuole la variante di tema.</summary>
public sealed class IntensityStepToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            int i and >= 0 and <= 4 => $"Brush.Heatmap.Level{i}",
            _ => "Brush.Heatmap.Level0",
        };

        var app = global::Avalonia.Application.Current;
        if (app is not null && app.TryFindResource(key, app.ActualThemeVariant, out var found) && found is IBrush brush)
            return brush;

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 2: Portare `YearHeatmapControl`**

`src/Recordum.UI/Controls/YearHeatmapControl.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Recordum.Presentation.ViewModels.HabitScreen;assembly=Recordum.Presentation"
             xmlns:conv="clr-namespace:Recordum.UI.Converters"
             x:Class="Recordum.UI.Controls.YearHeatmapControl"
             x:DataType="vm:YearHeatmapViewModel">
  <UserControl.Resources>
    <conv:IntensityStepToBrushConverter x:Key="IntensityStepToBrush"/>
  </UserControl.Resources>
  <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
    <ItemsControl ItemsSource="{Binding Cells}">
      <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
          <UniformGrid Rows="7" Columns="53"/>
        </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
      <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="vm:HeatmapCellViewModel">
          <Border Width="11" Height="11" Margin="1"
                  CornerRadius="2"
                  Background="{Binding IntensityStep, Converter={StaticResource IntensityStepToBrush}}"
                  ToolTip.Tip="{Binding TooltipText}"
                  ToolTip.ShowDelay="300"/>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </ScrollViewer>
</UserControl>
```

(`x:DataType` è **obbligatorio** qui e non lo era in WPF: il progetto ha `AvaloniaUseCompiledBindingsByDefault=true`, quindi ogni binding è verificato a compile-time. Il tipo è `YearHeatmapViewModel` — verificato: `HabitScreen.xaml:783` monta il controllo con `DataContext="{Binding YearHeatmap}"`, e `DetailViewModel:28` dichiara `public YearHeatmapViewModel YearHeatmap { get; }`.)

`src/Recordum.UI/Controls/YearHeatmapControl.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Recordum.UI.Controls;

public partial class YearHeatmapControl : UserControl
{
    // InitializeComponent(), MAI AvaloniaXamlLoader.Load(this): il secondo carica lo XAML ma non
    // cabla i campi x:Name, e il guasto è un NullReferenceException a runtime senza nessun errore
    // di compilazione.
    public YearHeatmapControl() => InitializeComponent();
}
```

- [ ] **Step 3: Portare `StreakTimelineControl`**

`src/Recordum.UI/Controls/StreakTimelineControl.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Recordum.UI.Controls.StreakTimelineControl">
  <Canvas x:Name="LayoutCanvas" Height="24"/>
</UserControl>
```

`src/Recordum.UI/Controls/StreakTimelineControl.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;

namespace Recordum.UI.Controls;

public partial class StreakTimelineControl : UserControl
{
    public StreakTimelineControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
    }

    private void Redraw()
    {
        LayoutCanvas.Children.Clear();
        if (DataContext is not StreakTimelineViewModel vm || Bounds.Width <= 0) return;

        var totalDays = (vm.RangeTo.DayNumber - vm.RangeFrom.DayNumber) + 1;
        if (totalDays <= 0) return;

        var dayWidth = Bounds.Width / totalDays;

        // TryFindResource vuole la variante di tema in Avalonia; FindResource di WPF no.
        var fill = this.TryFindResource("Brush.Accent", ActualThemeVariant, out var found) && found is IBrush b
            ? b
            : Brushes.Transparent;

        foreach (var run in vm.Runs)
        {
            var x = (run.Start.DayNumber - vm.RangeFrom.DayNumber) * dayWidth;
            var w = run.Length * dayWidth;
            var rect = new Rectangle
            {
                Width = Math.Max(2, w),
                Height = 16,
                RadiusX = 2,
                RadiusY = 2,
                Fill = fill,
            };
            // In WPF ToolTip è una proprietà diretta su FrameworkElement; in Avalonia è allegata.
            ToolTip.SetTip(rect, $"{run.Start:yyyy-MM-dd} → {run.End:yyyy-MM-dd} ({run.Length} days)");
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, 4);
            LayoutCanvas.Children.Add(rect);
        }
    }
}
```

(`ActualWidth` di WPF diventa `Bounds.Width`; l'evento `SizeChanged` esiste con la stessa semantica ma qui si sottoscrive nel costruttore invece che da XAML.)

- [ ] **Step 4: Scrivere il test-screenshot**

`tests/Recordum.UI.Tests/ControlsScreenshotTests.cs` (ricorda `using Xunit;` — v. Global Constraints): costruisci una finestra con i due controlli alimentati da ViewModel veri, e cattura in chiaro e scuro con `ScreenshotProbeTests.Capture`, sullo stesso modello di `HeatmapStripScreenshotTests.cs`.

**Due regole non negoziabili per questo test**, entrambe già pagate una volta:

1. **Non asserire sul peso del file.** Un PNG vuoto pesa e il test passa: è così che la sonda Live Preview ha prodotto una schermata vuota con un test verde. Asserisci su una **proprietà strutturale** — per esempio che `LayoutCanvas.Children.Count` sia pari al numero di run del ViewModel, e che `ItemsControl` della heatmap abbia realizzato 371 celle.
2. Lo screenshot è un **di più** per guardare, non la prova.

```csharp
[AvaloniaFact]
public void Streak_timeline_draws_one_rectangle_per_run()
{
    // Redraw() legge SOLO RangeFrom, RangeTo e Runs — tutte e tre pubbliche e scrivibili
    // (`[ObservableProperty]` le prime due, ObservableCollection la terza). Le due dipendenze del
    // costruttore servono a LoadAsync, che questo test non chiama: passarle null! è deliberato e
    // tiene il test dentro Recordum.UI.Tests, che non vede le fixture di Presentation.Tests
    // (progetti xunit di major diverse: 2.9.3 là, v3 qui).
    var vm = new StreakTimelineViewModel(null!, null!)
    {
        RangeFrom = new DateOnly(2026, 1, 1),
        RangeTo = new DateOnly(2026, 3, 31),
    };
    vm.Runs.Add(new StreakRun(new DateOnly(2026, 1, 5),  new DateOnly(2026, 1, 12), 8));
    vm.Runs.Add(new StreakRun(new DateOnly(2026, 2, 1),  new DateOnly(2026, 2, 3),  3));
    vm.Runs.Add(new StreakRun(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20), 11));

    var sut = new StreakTimelineControl { DataContext = vm, Width = 400, Height = 24 };
    var window = new Window { Content = sut, Width = 420, Height = 60 };
    window.Show();
    Dispatcher.UIThread.RunJobs();

    var canvas = sut.GetVisualDescendants().OfType<Canvas>().Single();
    Assert.Equal(3, canvas.Children.OfType<Rectangle>().Count());
}
```

- [ ] **Step 5: Provarlo contro il codice rotto**

```bash
cd /home/lucafrog/dev/recordum
sed -i 's/LayoutCanvas.Children.Add(rect);/\/\/ROTTO LayoutCanvas.Children.Add(rect);/' src/Recordum.UI/Controls/StreakTimelineControl.axaml.cs
dotnet test tests/Recordum.UI.Tests --filter 'FullyQualifiedName~ControlsScreenshotTests' --nologo -v q 2>&1 | tail -3
sed -i 's/\/\/ROTTO LayoutCanvas.Children.Add(rect);/LayoutCanvas.Children.Add(rect);/' src/Recordum.UI/Controls/StreakTimelineControl.axaml.cs
dotnet test tests/Recordum.UI.Tests --filter 'FullyQualifiedName~ControlsScreenshotTests' --nologo -v q 2>&1 | tail -2
```

Atteso: rosso, poi verde.

- [ ] **Step 6: Guardare i PNG**

```bash
ls -la tests/Recordum.UI.Tests/bin/Debug/net10.0/linux-x64/Screenshots/
```

Aprili davvero. Un assert strutturale dice *quante* forme ci sono; solo l'occhio dice se somigliano al gemello WPF.

- [ ] **Step 7: Eseguire tutto e chiudere l'onda**

```bash
cd /home/lucafrog/dev/recordum
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true -v q --nologo 2>&1 | tail -3
for p in Core Data Plugins Plugins.ExportEngine Presentation UI; do
  printf "%-24s " "$p"
  dotnet test "tests/Recordum.$p.Tests" --nologo -v q 2>&1 | grep -oE 'Failed:\s+[0-9]+, Passed:\s+[0-9]+' | tail -1
done
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
feat(ui): YearHeatmap e StreakTimeline su Avalonia, e finisce F0

I due controlli non-grafico che mancavano; HeatmapStrip era già portato dalla sonda,
i due wrapper LiveCharts restano a F7, dove il motore si spezza. Nessuno dei due usa
disegno immediato — in tutto src/ non ce n'è — quindi sono porting diretti: il primo
è XAML dichiarativo, il secondo riempie un Canvas in code-behind.

Le differenze di meccanismo, tutte piccole e tutte annotate dove capitano:
ToolTip è allegata invece che diretta, TryFindResource vuole la variante di tema,
ActualWidth è Bounds.Width, e x:DataType è obbligatorio perché i binding compilati
sono il default del progetto.

Il test asserisce sulla STRUTTURA — un rettangolo per run, 371 celle — e non sul peso
del PNG: un PNG vuoto pesa, ed è così che la sonda Live Preview ha prodotto una
schermata vuota con un test verde. Lo screenshot resta, per guardare.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_016tY1cajAem6wSq8SnHNi4i
EOF
)"
```

---

## Chiusura dell'onda

Dopo il Task 10, prima di considerare `F0` chiusa:

1. **Review di branch**, non solo per-task. Le review di branch dell'Onda 2.4 e 2.5 hanno trovato quattro Critical che le review per-task, cieche fuori dal proprio diff, non potevano vedere — e in quest'onda il rischio è esattamente di quel tipo, perché un file finito nella cartella sbagliata al Task 7 non lo vede nessun singolo task.
2. **Il conto dei test su Linux va scritto nel commit di chiusura**, come nuova baseline per `F1`.
3. **Il debito verso la VM resta e va detto**: `Recordum.WPF.Tests` conserva 23 file che su Linux non girano. Finché WPF esiste, la loro verifica è sulla VM.
4. **CLAUDE.md va aggiornata** — §4 (i progetti non sono più cinque), §8 (la nuova posizione di `Strings.Designer.cs` e il punto cieco di `guard-generated.js` che trasloca con lei), §7 (lo stato dell'onda). È un commit a parte, non un ripensamento in coda a un task.

`F1` (Home) si pianifica dalla stessa spec, a ridosso dell'esecuzione.
