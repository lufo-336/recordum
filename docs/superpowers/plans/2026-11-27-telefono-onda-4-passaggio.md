# Passaggio — onda 4 del telefono, da riprendere al Task 5

Scritta da: Claude (Fedora), 2026-11-27. Serve a riprendere da un'altra macchina (VM Windows), dove la memoria di
Claude e il registro git-ignored `.superpowers/sdd/2026-11-24-telefono-onda-4/progress.md` non arrivano.
Da cancellare a fine onda (Task 14).

## Dove siamo

- Branch `telefono-onda4` (partito da `unisci`), pushato; ultimo commit di codice `cfd77a47`, CI verde su linux, android
  e windows. Il merge in `main` lo fa l'autore.
- Spec: `docs/superpowers/specs/2026-11-24-telefono-onde-4-5-design.md`; mockup `docs/design/2026-11-24-telefono-onde-4-5/`;
  sonde sul Galaxy: `docs/design/2026-11-24-telefono-sonde.md`; piano: `docs/superpowers/plans/2026-11-24-telefono-onda-4.md`.
- Fatti: **Task 1** WPF spenta (b2fd1c9f) · **Task 2** `Recordum.UI` libreria + testa `Recordum.Desktop` (f4184efd,
  6845286d) · **Task 3** `AppStartup` condivisa fuori dal thread UI, con `StartupTimeline` (4dc19067) · **Task 4**
  Steps 1, 2, 4: `src/Recordum.Android`, `PhoneStartupHost` provvisorio, `UnlockView` estratta, script di firma e
  d'installazione, controllo del manifest, job `android` in CI (cfd77a47).
- **Manca il Task 4 Step 3** (solo da Fedora, col Galaxy collegato): l'autore lancia `scripts/android/create-signing-key.sh`
  (password a mano, finisce nel portachiavi di Linux), poi `scripts/android/install-phone.sh` (chiede di aver fatto
  "Backup ora", non disinstalla mai); `adb logcat -s Recordum` per la riga `startup … home=`; 5 avvii a freddo
  (`am force-stop` + `am start -W -n io.github.lufo336.recordum/io.github.lufo336.recordum.MainActivity`), mediana nel
  §16 della spec.
- **Prossimo: Task 5** (la cornice: `TabbedPage` in basso, `PhoneNavigator`, `HandleBack`, `RebuildShell`). Su Windows
  si fanno Step 1, 2 e 4 (codice in `Recordum.UI` e test headless); lo Step 3 (prova sul telefono, "Non conservare
  attività") va fatto da Fedora.

## Cosa è provvisorio e il Task 5 sostituisce

- `src/Recordum.UI/Phone/PhoneStartupHost.cs`: ctor `PhoneStartupHost(Action<Control> attach)` (App gli passa
  `root => single.MainView = root`); oggi mostra attesa → `UnlockView` → vista d'errore costruita in codice → una
  `HomeView` nuda. `ISingleViewApplicationLifetime` non è implementabile da codice utente: per questo l'`Action`, e i
  test lo usano così (`tests/Recordum.UI.Tests/Phone/PhoneStartupHostTests.cs`).
- `src/Recordum.Android/AndroidHead.cs` `AddShellServices`: oggi `AddRecordumDesktopShell()` + `AndroidApplicationLifetime`;
  diventa `AddRecordumPhoneShell()`.

## Decisioni dell'autore (non riaprirle)

Impronta + passphrase di ripiego; Sicurezza con i modi d'apertura (T10: impronta, blocco schermo, senza chiedere,
passphrase); blocca di nuovo dopo 5 min; recenti coperte; ripasso della passphrase spento di default; finestre →
pagine/fogli; "Annulla" 5 s; notifica del timer senza "Ferma"; lo scatto finisce nella Galleria; cornice con
`TabbedPage`/`NavigationPage` di Avalonia 12; ApplicationId `io.github.lufo336.recordum`; RSA-OAEP per il Keystore.

## Trappole già pagate in quest'onda

- **Test mirati**: `dotnet test … -- --filter-class` passa da vstest e **ignora il filtro** (gira tutta la suite UI, ~30 min,
  e su Fedora ha finito la memoria). Usare l'eseguibile: su Windows
  `tests\Recordum.UI.Tests\bin\Debug\net10.0\win-x64\Recordum.UI.Tests.exe -class <Namespace.Classe>`
  (su Linux `…/linux-x64/Recordum.UI.Tests`). `--blame-hang` non vede i test xunit v3: `-longRunning N`.
- La suite UI intera dura ~30 min con tratti lunghi senza output: non è un blocco. `HomePerformanceTests` può andare
  rosso solo sotto il carico della suite (verde da solo).
- `Dispatcher.UIThread.InvokeAsync(Func<Task<T>>)` esiste in Avalonia 12.1.2: i ponti di `AppStartup` verso l'host lo usano.
- `App` non tiene `AppStartup` in un campo (CA1001): variabile locale, `Exit` aspetta `DisposeAsync` (12 s).
- Android: le librerie hanno il RID dell'host (`Directory.Build.props`), quindi `Recordum.Android.csproj` mette CS8012 in
  `NoWarn` e referenzia **ogni** progetto direttamente con `GlobalPropertiesToRemove="RuntimeIdentifier;SelfContained"`
  (i transitivi ricevevano `android-arm64`, NETSDK1047). Il progetto **non** è in `Recordum.slnx`: la build della
  soluzione non chiede il workload Android. `Process.StartElapsedRealtime` è una proprietà.
- `IsDefaultLocation` su Android: `RecordumOptions.Default()` (la cartella sta in `FilesDir/.local/share/Recordum`).
- `check-manifest.sh` legge solo `*/android/manifest/AndroidManifest.xml` (il manifest unito, non quello pre-unione).
- Dal worktree `recordum-gio11` su Fedora l'hook guard-main legge il branch della cartella principale: `git -C <worktree>`.
- Telefono: mai `adb uninstall`; niente tocchi adb né screenshot, solo intent. Dal Task 9 build firmate con la chiave del
  dogfooding e precedute da "Backup ora".

## Antigravity (agy)

L'autore vuole usare agy il più possibile: Hydrogen (`gemini-4-hydrogen-high`) scrive da un brief, Argon
(`gemini-4-argon-high`) fa la review; `agy_run.py --role write|review --prompt-file … --timeout 2400s` in background.
Nel brief: vietare la shell (un comando negato annulla la sessione), nominare gli strumenti, "leggi SOLO dentro il
repository" (niente memoria di Claude), mai dargli `.resx` né `*.Designer.cs` (le stringhe le aggiunge Claude via
shell). Sulla VM Windows va verificato prima che agy e la skill `antigravity` ci siano.
