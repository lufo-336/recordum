# Sincronizzazione ambiente Claude Code — VM ↔ Mac ↔ Linux

> **Nota operativa interna** (non è codice né stringa utente). Serve a tenere allineati i due ambienti da cui si lavora a Recordum con Claude Code. Questo file **viaggia via git**, quindi è leggibile da entrambe le macchine. Tienilo aggiornato come fonte di verità sullo stato degli ambienti.

## Contesto

- **Primario — VM Windows**: `C:\Users\luca.foglia\Documents\recordum` (PowerShell, `taskkill`, RID `win-x64`).
- **Secondario — Mac**: `/Users/lucafoglia/Desktop/recordum` (usato quando la VM non è raggiungibile).
- **Secondario — Linux (Fedora)**: `~/dev/recordum`, clone da GitHub (aggiunto 2026-09-09). Vedi la sezione dedicata in fondo: si builda tutto e si testano davvero **Core, Data, Plugins ed ExportEngine**; **WPF e ChartEngine no**, e non è un difetto di configurazione.
- Esiste anche una copia su **Google Drive** (`.../Other computers/Il mio PC/Documents/recordum`).

## Cosa viaggia da solo, via git ✓

Già allineato automaticamente su entrambe le macchine al `pull`:

- `.claude/settings.json` (permessi base + wiring dei 4 hook guardrail)
- `.claude/hooks/*.js` (guard-main, guard-userdata, guard-generated, stop-dirty)
- `.claude/skills/run/` (skill di progetto)
- `CLAUDE.md`, `docs/`, e questo file

## Cosa NON viaggia — machine-local, va replicato a mano ✗

Vive in `~/.claude/` (o `%USERPROFILE%\.claude\`), fuori dal repo:

- **Plugin abilitati** (`~/.claude/plugins/`, `~/.claude.json`)
- **Memorie di progetto** (`~/.claude/projects/<hash>/memory/`)
- **`.claude/settings.local.json`** (gitignored; permessi per-utente — quelli Windows `PowerShell(...)`/`taskkill`/`C:\` sono inutili su Mac e viceversa, **non serve sincronizzarli**)
- Eventuali **sub-agent** (`~/.claude/agents/`), **MCP server**, **skill a livello utente** (`~/.claude/skills/`)

---

## ⬇️ DA COMPILARE DALLA VM — report dello stato

La prossima volta che sei sulla VM, esegui questi comandi e incolla l'output nella sezione "Output VM" qui sotto (o passali in chat a Claude). Servono a sapere **cosa hai sulla VM che qui manca**, così riconcilio.

```powershell
claude plugin list                 # 1) plugin installati + versioni + enabled/disabled
claude plugin marketplace list     # 2) marketplace registrati
claude mcp list                    # 3) MCP server configurati (atteso: nessuno)
dir %USERPROFILE%\.claude\agents          # 4) sub-agent custom (atteso: nessuno)
dir %USERPROFILE%\.claude\skills          # 5) skill a livello utente
dir %USERPROFILE%\.claude\projects        # 6) trova la cartella "recordum" e guarda dentro \memory
```

**Domande a cui rispondere:**

1. Il **set di plugin** della VM coincide con quello del Mac (vedi "Stato noto — Mac": superpowers, feature-dev, code-simplifier, claude-md-management, code-review, frontend-design, context7)? Ce ne sono **in più** (es. hookify, altri marketplace) o **in meno**?
2. Ci sono **sub-agent custom** (file in `\.claude\agents\`), **MCP server** aggiuntivi o **skill utente** standalone che qui non ho trovato?
3. Quali **memorie** ci sono in `…\projects\<recordum>\memory\`? Se sono utili, esportale (vedi sotto).

### Output VM (compilato 2026-06-25)

**Plugin installati sulla VM** — tutti da `claude-plugins-official`, scope user, enabled:

| Plugin | Versione VM | vs Mac |
|---|---|---|
| `superpowers` | **6.0.3** | = |
| `claude-md-management` | 1.0.0 | = |
| `code-review` | unknown | = |
| `context7` | unknown | = (MCP connesso) |
| `frontend-design` | unknown | = |
| `figma` | **2.2.68** | **solo VM** (porta skill figma-* + 1 MCP server) |
| `feature-dev` | — | **MANCA sulla VM** (Mac ce l'ha: agent `code-architect`/`code-explorer`/`code-reviewer`) |
| `code-simplifier` | — | **MANCA sulla VM** (Mac ce l'ha: agent `code-simplifier`) |

- **Marketplace**: solo `claude-plugins-official` (= Mac).
- **MCP**: `plugin:context7:context7` ✔ connesso. `figma` + `claude.ai Google Drive/Calendar/Gmail` compaiono come *Needs authentication* — sono MCP forniti dal plugin figma / da claude.ai, **non** configurati a mano.
- **Sub-agent custom** (`%USERPROFILE%\.claude\agents`): cartella **assente** → nessuno (= Mac; gli agent vengono dai plugin).
- **Skill utente standalone** (`%USERPROFILE%\.claude\skills`): cartella **assente** → nessuna (= Mac; solo quelle dei plugin + `run` via git).
- **Memorie** (`…\projects\C--Users-luca-foglia-Documents-recordum\memory\`): set storico **completo** (20 file: `MEMORY.md` + 19 memorie milestone/feature/strategia). Sono quelle che "restano sulla VM" da nota Mac.

**Riconciliazione — divergenze da decidere:**
1. **`feature-dev`** e **`code-simplifier`** mancano sulla VM. Sono comodità di sviluppo (non requisito hard come superpowers). Installabili con `claude plugin install feature-dev@claude-plugins-official` e idem `code-simplifier`, poi `/reload-plugins`. Da fare solo se si vuole parità piena.
2. **`figma`** (2.2.68) è solo sulla VM. Decidere se replicarlo sul Mac o se è intenzionalmente VM-only.

---

## Stato noto — Mac (aggiornato 2026-06-23)

**Plugin installati** — tutti da marketplace `claude-plugins-official`, scope user, enabled. Questo è il **set di riferimento** da replicare sulla VM:

| Plugin | Versione | Porta |
|---|---|---|
| `superpowers` | **6.0.3** | 14 skill (brainstorming, writing-plans, executing-plans, subagent-driven-development, TDD, finishing-a-development-branch, verification-before-completion, systematic-debugging, requesting/receiving-code-review, using-git-worktrees, using-superpowers, writing-skills, dispatching-parallel-agents) + 1 hook SessionStart |
| `feature-dev` | unknown | skill `feature-dev` + 3 agent (`code-architect`, `code-explorer`, `code-reviewer`) |
| `code-simplifier` | 1.0.0 | 1 agent (`code-simplifier`) |
| `claude-md-management` | 1.0.0 | 2 skill (`claude-md-improver`, `revise-claude-md`) |
| `code-review` | unknown | skill `code-review` |
| `frontend-design` | unknown | skill `frontend-design` |
| `context7` | unknown | **1 MCP server** (`context7`, lookup docs librerie) |

| Voce | Stato Mac |
|---|---|
| Marketplace | `claude-plugins-official` |
| Sub-agent custom (file propri) | nessuno — gli agent sopra sono forniti dai plugin |
| MCP server | `context7` (via plugin `context7`); nessun altro configurato a mano |
| Skill utente standalone | nessuna — solo quelle dei plugin + `run` di progetto (via git) |
| Memorie | ricostruite da zero — solo `dev-environment`; le storiche restano sulla VM |

> Le skill/agent dei plugin si caricano **al riavvio sessione** (o con `/reload-plugins`). `context7` richiede una sessione nuova per agganciare l'MCP.

## ✅ Checklist allineamento (quando manca qualcosa)

- [ ] **Allinea il set di plugin** (vedi tabella sopra). Per replicare l'intero set su una macchina che parte da zero, poi `/reload-plugins` o riavvio:
  ```bash
  for p in superpowers feature-dev code-simplifier claude-md-management code-review frontend-design context7; do
    claude plugin install "$p@claude-plugins-official"
  done
  ```
- [ ] **superpowers è il requisito hard** (pin **v6.0.3**): tutto il flusso `docs/superpowers/*` (plans, specs, brainstorming, TDD, subagent-driven-development) ne dipende. Gli altri 6 sono comodità di sviluppo, allineali se vuoi parità piena.
- [ ] **`context7`** porta un MCP server: dopo l'install serve una **sessione nuova** perché si agganci; potrebbe chiedere configurazione/chiave al primo uso.
- [ ] **Memorie utili** → non viaggiano via git. Per condividerle: copiare i `.md` da `~/.claude/projects/<recordum>/memory/` e tenerli in un punto condiviso (es. allegarli a questo file o a `docs/`), poi reimportarli sull'altra macchina.
- [ ] **`settings.local.json`** → NON sincronizzare i permessi cross-OS (PowerShell/`C:\` ↔ bash/`/Users`). Ogni macchina accumula i suoi.
- [ ] Dopo ogni `pull`, gli **hook guardrail** e la skill `run` sono già aggiornati (via git): nessuna azione.

## Procedura "appena arrivo sull'altra macchina"

1. `git pull` (allinea repo, hook, skill `run`, questo file).
2. `claude plugin list` → se manca `superpowers`, installalo e riavvia.
3. Dai un'occhiata all'**Output VM** qui sopra per vedere se è emerso altro da replicare.

---

## Log sessione 2026-06-23 → 2026-06-24 (da macOS) — handoff per la VM

Lunga sessione da Mac su `fase-4-polish` (dal commit `24c0e60` fino a `60ead30`+docs): chiusura quasi totale del backlog code-review (CR1–CR24, vedi `phase-4-issues.md` §"Code review 2026-06-19"), un'ampia **passata di rifinitura UI** (menù/dropdown/tooltip/slider/splitter/scrollbar/tabstrip/empty-state/simbolo R + scroll colonne ibrido), e la **feature Color Picker v2** (spec→plan→subagent-driven). Sulla VM: `git pull`, poi esegui **tutte le checklist qui sotto** — su macOS NON girano né la WPF né i test SQLCipher/DPAPI, quindi ogni verifica runtime/visiva e l'intera suite test sono da fare sulla VM. (Manca ancora un remote git: il push è da configurare.)

### Cosa è arrivato (commit)
- **`24c0e60` fix(data): CR1–CR4** — restore atomico + rollback (CR1), guard `schema_version` reale con confronto major.minor (CR2), bound sui parametri Argon2id non fidati + cap anti zip-bomb (CR3), shred dei temp in chiaro (CR4). Nuovi helper `SchemaVersionGuard`, `SecureWipe`.
- **`3497021` feat(wpf): appearance pass** — editor diario tematizzato (segue tema light/dark **a caldo**) + diario in **serif**; input minimal **underline** (TextBox già fatto, ora anche `PasswordBox` + preview `NotesTabView`); **font Palatino** app-wide (`Tokens.Font.Default` + `Window/Page`); **setting font utente** (`AppFontService`, persiste `ui.font`, default Serif) in **Impostazioni → Aspetto**; editor che segue il font via push C#.
- **docs** (questo commit) — questo log + `phase-4-issues.md` + `CLAUDE.md` + anteprime browser.

### Vincoli macOS scoperti (utili a ogni futura sessione Mac)
- **WPF non gira su macOS** e non builda di default. Solo *compile-check*: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj -p:EnableWindowsTargeting=true`. Eseguire/vedere l'app = solo VM.
- **Test `Recordum.Data` su Mac**: RID pinnato `win-x64` → il runner x64 non parte su arm64. Per girarli su Mac forza l'arch: `dotnet test tests/Recordum.Data.Tests -p:RuntimeIdentifier=osx-arm64` (i test crypto/managed passano; quelli che aprono SQLCipher possono comunque richiedere la VM). I test **`Recordum.WPF.Tests` NON girano su Mac** (WPF assente) — solo compile-check con `EnableWindowsTargeting`.
- **CRLF**: il working tree Mac è CRLF, il repo è LF. Fix locale (solo Mac, non committato): `git config core.autocrlf input` → spariscono i diff fantasma. Niente da fare sulla VM.
- **UI non visibile da Mac** → anteprime browser (aprile con `open <file>`): `docs/editor-theme-preview.html` (editor tematizzato + toggle dark/light + diario serif), `docs/font-preview.html` (confronto font su UI finta), `docs/diario-ui-preview.html` (mockup textbox/diario). Il check definitivo resta nell'app sulla VM.

### ✅ Checklist verifica sulla VM
**Data / sicurezza (CR1–CR4) — poi segna `verified-fixed` in `phase-4-issues.md`:**
- [ ] `dotnet test tests/Recordum.Data.Tests` completo verde.
- [ ] **CR1**: restore di un `.recbak` interrotto/corrotto a metà → il DB live **sopravvive** (rollback), non sparisce.
- [ ] **CR2**: restore di un backup con `schema_version` più recente del build → messaggio "Update Recordum…", niente crash EF.
- [ ] **CR3**: `.recbak` con header malevolo (memMib enorme) → `invalid_format`, nessuna alloc mostruosa.
- [ ] **CR4**: dopo un restore, `%TEMP%` non contiene un DB in chiaro recuperabile.

**Appearance / font (visivo):**
- [ ] `dotnet test tests/Recordum.WPF.Tests` (incl. `AppFontServiceTests`) verde.
- [ ] Editor diario: cambia tema light↔dark **a editor aperto** → si ri-tematizza a caldo; testo del diario in **serif**.
- [ ] Input coerenti **underline**: TextBox, `PasswordBox` (backup/restore/impostazioni), preview note.
- [ ] Tutta l'app in **Palatino**; numeri (timer/diagnostica) restano monospace.
- [ ] **Impostazioni → Aspetto → Font**: System ↔ Serif (Palatino) → app **e** diario cambiano a caldo; riavvio → scelta persistita (`ui.font`).

### ✅ Checklist verifica sulla VM — continuazione 2026-06-23 (commit `fe6cb9d`, `37a2fd5`)
**Test (poi segna `verified-fixed` in `phase-4-issues.md`):**
- [ ] `dotnet test tests/Recordum.Data.Tests` completo verde **inclusi** i 17 Backup/Security (su Mac falliscono per DPAPI/SQLCipher Windows-only).
- [ ] `dotnet test tests/Recordum.WPF.Tests` verde — **prima esecuzione** dopo il fix di compilazione (`FakeAppFontService`); il progetto non compilava dalla `3497021`.

**Data (verificato su Mac, riconferma su VM):** CR5/CR7/CR9/CR10/CR11 — già verdi su `osx-arm64`.

**WPF — verifica visiva + run test (compile-only su Mac):**
- [ ] **CR5**: habit **State** con scala 0-based, registra il valore **0** → la cella calendario **appare** (tinta presenza minima + numero "0"), non vuota; un giorno con sola nota resta senza tinta.
- [ ] **CR13**: registra una serie con **sola durata** (es. 1:30) → in Cronologia/Records la riga mostra "Tempo: 1:30", non vuota.
- [ ] **CR20**: Backup → "Sfoglia" → nome file suggerito `recordum-AAAAMMGG-hhmm.recbak` con timestamp corrente.
- [ ] **CR22**: year-heatmap di un bisestile col bordo-settimana (es. 2028 cultura domenica-prima / 2012/2040 lunedì-prima) → il **31 dic** è presente nella griglia.
- [ ] **R-contextMenu** (menù tasto-destro): click destro su una riga habit → pannello arrotondato con ombra, **niente colonna-icone grigia**, hover accento tenue, submenu "Fissa" si apre col chevron. **Controllo critico**: gli **angoli del popup sono trasparenti** (no rettangolo bianco/nero dietro il raggio) — se non lo sono, è il `HasDropShadow`/`AllowsTransparency` del popup. Light **e** dark. Riferimento look: `docs/context-menu-preview.html`.
- [ ] **R-slider** (scala State): habit State con slider → traccia sottile, porzione riempita in accento fino al thumb circolare; anello su hover/drag; snap-to-tick funziona. Light+dark. Rif: `docs/slider-preview.html`.
- [ ] **R-brandMark** (title bar): accanto a "Recordum" c'è una **R serif** (Palatino) nel colore del testo, non più la linea-impulso; resta serif anche se imposti UI font System. Rif: `docs/brand-mark-preview.html`.
- [ ] **R-comboDropdown**: apri un ComboBox (es. periodo in Cronologia) → pannello con **ombra** e raggio come i menù, **allineato sotto la casella** (no scostamenti), hover accento, valore corrente con tinta tenue. Light+dark. Rif: `docs/combobox-preview.html`.
- [ ] **R-columnScroll** (ibrido fill-then-scroll): apri **1–2 colonne** → riempiono il tab senza spazio vuoto a destra; apri **molte** (Cronologia+Dettaglio+Note+Configura…) → quando non ci stanno parte lo **scroll orizzontale**, ogni colonna resta ≥320 (non si schiaccia). Lo scroll è liscio (trackpad / Shift+rotella). I GridSplitter tra colonne ridimensionano ancora. Rif: `docs/column-scroll-preview.html`.
- [ ] **R-tooltip**: passa sopra un pulsante con tooltip (es. ⌂ sidebar, Backup) → riquadro **arrotondato con ombra** (non più rettangolo squadrato), angoli trasparenti puliti. Light+dark. Rif: `docs/tooltip-preview.html`.
- [ ] **R-columnSplitter**: con 2+ colonne aperte, il divisore a riposo è solo la **hairline** (non più la barra grigia 4px); passandoci sopra compare una **barra accento** sottile, piena mentre trascini; il resize funziona ancora. Rif: `docs/column-splitter-preview.html`.
- [ ] **R-emptyState**: Home senza abitudini (Attive/Archiviate/Cestino) → **icona line + titolo** centrati, icona diversa per tab (Sprout/Moon/Sparkles); workspace senza tab → BookOpen + testo. Sul tab **Attive** un pulsante accento **"Crea la prima abitudine"** apre il flusso di creazione (nascosto su Archiviate/Cestino); copy non più "milestone 3.4". Light+dark. Rif: `docs/empty-state-preview.html`.
- [ ] **R-scrollbar**: in una lista lunga (es. albero Home, colonna Cronologia) il thumb a riposo è sottile/tenue, **non sparisce** mai (min-size); passandoci sopra/trascinando si **scurisce e allarga**. Light+dark. Rif: `docs/scrollbar-preview.html`.
- [ ] **R-tabStrip**: apri 2+ tab nel workspace → angoli superiori **arrotondati**, underline accento sulla tab attiva, chiusura come **icona X** (non più "✕"); pin/preview funzionano come prima. Light+dark. Rif: `docs/tabstrip-preview.html`.

### ✅ Color Picker v2 (R-colorPicker) — verifica sulla VM
Apri il color picker (editor habit / builder / colore note). Mockup di riferimento: `docs/color-picker-preview.html`.
- [ ] **Test**: `dotnet test tests/Recordum.WPF.Tests` → verdi i nuovi `ShadeRampTests`, `ColorEqualsConverterTests`, `FavoriteColorStoreTests`, `NotesSectionViewModelTests.AddFavorite_then_load_exposes_the_color`.
- [ ] **Polish**: popup arrotondato con ombra (famiglia menù); SV/hue arrotondati; campo hex con "#" fisso + anteprima.
- [ ] **F1 selezione**: lo swatch del colore corrente ha l'**anello accento**; hover su un altro = anello grigio (`TextMuted`).
- [ ] **F2 tonalità**: la riga "Tonalità" mostra 7 sfumature chiare→scure del colore scelto; cliccarne una lo imposta.
- [ ] **F4 preferiti**: "+ Aggiungi" appunta il colore corrente; ✕ lo rimuove; persiste al riavvio (AppSetting `ui.favorite_colors`, max 12). Funziona in editor, builder e colore note.
- [ ] **F3 contagocce**: il bottone apre un overlay full-screen; clic su un punto qualsiasi → campiona quel colore; Esc/click destro annulla. (Win32, verificabile SOLO qui.)

### ✅ Robustezza/perf/convenzioni 2026-06-24 (CR12, CR14–CR19, CR21, CR23, CR24) — verifica sulla VM
Per lo più non-visivi: si verificano eseguendo la **suite test WPF completa** (mai girata interamente da quando ho riparato la build dei test). `dotnet test tests/Recordum.WPF.Tests` → verdi in particolare:
- `HabitEditorViewModelTests.LoadAsync_deselect_during_an_in_flight_load_does_not_throw_or_clobber` (CR14)
- `HomeViewModelTests.ApplyDropAsync_does_not_throw_when_the_dragged_node_vanished` (CR16)
- `DebouncedColorRecorderTests` (CR23) — *timing-based, margini generosi; se flaky rilancia*
- `QuickAddViewModelTests.Save_failure_shows_a_localized_message_not_the_raw_exception` (CR21)
- `StreakSummaryViewModelTests.A_lapse_closed_run_ending_yesterday_does_not_feed_CurrentLength` (CR12)

Spot-check a vista:
- [ ] **CR17** drag-drop: l'indicatore Before/Onto/After segue i terzi reali della riga (anche trascinando sopra testo/sparkline).
- [ ] **CR21** QuickAdd: forzare un errore di salvataggio → messaggio **localizzato** ("Salvataggio non riuscito. Riprova."), mai testo EF/SQLite grezzo.
- [ ] **CR-emptyCTA** Home senza abitudini (tab Attive) → pulsante accento **"Crea la prima abitudine"** apre il flusso; nascosto su Archiviate/Cestino.

**Altro:**
- [ ] **Suite completa**: `dotnet test tests/Recordum.Data.Tests` (i 17 DPAPI/SQLCipher Windows-only) **+** `dotnet test tests/Recordum.WPF.Tests` (tutta — net10.0-windows, mai eseguita su Mac).
- [ ] **CR8** (opzionale): `dotnet ef migrations add` per riallineare il seed `schema_version`/ModelSnapshot a **2.11** (oggi il costante è `2.9`; impatto runtime zero, è solo igiene).

---

## Log sessione 2026-06-25 (da VM Windows) — prima sessione VM dopo Mac

Eseguita **sulla VM** (qui WPF e SQLCipher girano davvero). 7 commit su `fase-4-polish` (`f43b6ee` → `11fb675`). Dettaglio in `phase-4-issues.md` §"Stato al 2026-06-25".

**Suite test — eseguita per la prima volta intera sulla VM, verde**: `Recordum.Data.Tests` **356/356** (inclusi i 17 DPAPI/SQLCipher prima Windows-only-falliti su Mac) + `Recordum.WPF.Tests` **651/651**. Quindi le checklist "test" delle sessioni Mac 2026-06-23/24 qui sopra sono ✅ **fatte**. L'unico bug emerso al primo run reale era un test sbagliato (`ShadeRampTests`, regex hex troppo stretto → reso case-insensitive, commit `165d114`).

**Verificato a schermo dall'utente sulla VM (→ `verified-fixed`):**
- **Zen focus** (`1d8f2f7`), **font Palatino su tutte le finestre** (`0ada297`), **slider a tappe + scala 1-based** (`c94f222`), **scrollbar sottili overlay auto-hide** (`11fb675`).
- Due gotcha WPF utili per il futuro: (1) gli **stili impliciti non si applicano alle sottoclassi** (Window→MainWindow) — il `FontFamily` va messo sul root di ogni Window; (2) la **ScrollBar ha un `MinWidth` effettivo ~16px** che ri-clampa `Width` → per una scrollbar sottile serve `MinWidth=0` locale + ScrollViewer ritemplate-ato in overlay.

**Ambiente plugin**: installati sulla VM anche `feature-dev` + `code-simplifier` (+ `skill-creator`); ora la VM ha tutto il set Mac **più** `figma`/`skill-creator`. (Sezione "Output VM" sopra aggiornata 2026-06-25.) Usato un subagent (`general-purpose`) per il retemplate dello ScrollViewer.

**Ancora da fare sulla VM** (resta dalla lista Mac, non ancora ripassato a video): sweep visivo R-* accumulato (context menu, combo, tooltip, splitter, empty-state, tabstrip, brand mark, column-scroll, Color Picker v2 F1–F4), CR5/CR13/CR20/CR22 a schermo, restore corrotto runtime (CR1–CR4), e **CR8** (regen ModelSnapshot a 2.11). **Nota dati**: le registrazioni State già nel DB locale sono 0-based → sfasate di 1 col nuovo encoding; per una verifica pulita conviene `--clean` (skill `run`) o re-registrare.

**Push**: ancora nessun remote git configurato — i commit restano locali sulla VM. Da decidere come allinearli al Mac (push o copia).

---

## Log sessione 2026-06-27 (da copia Google Drive `G:\…`) — handoff per la VM

Sessione breve aperta sulla **terza postazione**: la copia su **Google Drive File Stream** (`G:\Altri computer\Il mio PC\Documents\recordum`), non VM né Mac. Git coerente (HEAD `7d0b6a6` discende linearmente da `11fb675` della VM 2026-06-25; working tree pulito; nessun remote). Plugin a posto: set di riferimento completo, `superpowers` **6.0.3**.

### ⚠️ Limite scoperto: NON si builda/avvia l'app WPF sul disco Google Drive
- `dotnet build` del progetto WPF fallisce su `CreateAppHost`: `System.IO.IOException: …file la cui sezione mappata dall'utente è aperta`. Causa: **GoogleDriveFS** tiene una sezione memory-mapped sull'`.exe` di output → collide con la patch dell'apphost. Nessuna istanza di Recordum era in esecuzione: è il filesystem virtuale.
- **Le librerie e i test invece compilano e girano** su Drive: i progetti di test sono librerie (niente apphost). La suite WPF si lancia con **`-p:UseAppHost=false`** per saltare lo step che fallisce.
- **Conseguenza**: tutto il backlog **visivo** (run dell'app: U3, residuo U1 Esporta, U4, U5, sweep R-*, restore corrotto a video) **non è eseguibile dalla copia Drive**. Va fatto su VM (o da un clone locale fuori da Drive). Decisione utente in questa sessione: **si prosegue sulla VM**.

### ✅ Fatto in questa sessione (no run dell'app)
- **Gate test della serie U** (era l'unico "da lanciare" dell'handoff 2026-06-25): **`Recordum.Data.Tests` 356/356** + **`Recordum.WPF.Tests` 651/651** verdi. Unico fail nella run completa = `BackupSchedulerHostTests.Initialize_runs_first_due_check_after_initial_delay`, **flake da timing** (verde 2/2 isolato; margini stretti + Drive lento). Il campo nuovo `ScalePoints` (U3) non rompe nulla. Registrato anche in `phase-4-issues.md` (handoff 2026-06-25).
- **ADR 0001** — `docs/decisions/0001-ui-framework-wpf-over-avalonia.md`: registrato il razionale "perché WPF e non Avalonia" (non era scritto da nessuna parte). In breve: strategia *UI nativa per piattaforma + Core condiviso*, niente desktop cross-OS → il vantaggio di Avalonia non serve; WPF vince su WebView2/maturità/continuità.

### ▶️ Da riprendere sulla VM (priorità invariata, ora che i test sono verdi)
1. **U3** a schermo (allineamento label↔thumb slider scala: Mood/Sleep quality; estremi che non sborbino; scale >12 step).
2. **Residuo U1**: finestra **Esporta** — checkbox blu nativi + corpo troppo scuro: verificare se lo stile flat si applica ora che ha la chrome scura; se no, sotto-task stile CheckBox in quella finestra.
3. **U4** (numero centrato nel cerchio heatmap) e **U5** (toolbar "Modelli predefiniti" da spostare a sinistra) — rifiniture minori, no design.
4. Poi sweep visivo R-* accumulato + CR5/CR13/CR20/CR22 + restore corrotto runtime (CR1–CR4) + **CR8** (regen ModelSnapshot a 2.11), come da handoff 2026-06-25.

> **Promemoria sync**: nessun remote git. Questi commit/doc viaggiano alla VM **solo via sincronizzazione file di Drive** (incluso `.git`) — fragile. Per evitare conflitti di working tree, far sincronizzare Drive **a tree pulito** prima di lavorare sulla VM. La configurazione di un remote vero resta consigliata e aperta.


## Linux (Fedora) — aggiunto 2026-09-09

Terza macchina, clone GitHub in `~/dev/recordum`. Serve sapere **cosa ci si può fare davvero**, perché la risposta non è né "tutto" né "niente".

### Cosa gira, cosa no

| | Su Linux |
|---|---|
| Build dell'**intera** soluzione (`-p:EnableWindowsTargeting=true`) | ✅ |
| `Recordum.Core.Tests` · `Recordum.Data.Tests` · `Recordum.Plugins.Tests` · `Recordum.Plugins.ExportEngine.Tests` | ✅ girano |
| **SQLCipher** (DB cifrato, `PRAGMA cipher_version`) | ✅ funziona — l'encryption non è una cosa Windows |
| `Recordum.WPF.Tests` | ❌ serve `Microsoft.WindowsDesktop.App`, che su Linux **non esiste** |
| `Recordum.Plugins.ChartEngine.Tests` | ❌ `net10.0-windows` (ospita le view LiveCharts2 WPF) |
| `Recordum.Avalonia.Tests` (headless, Avalonia.Headless.XUnit) | ✅ gira — prima suite di **interfaccia** del progetto che gira davvero su Linux |
| I 18 test **DPAPI** dentro `Recordum.Data.Tests` | ❌ `ProtectedData` è Windows-only — l'astrazione `IDbKeyProvider` esiste apposta, e `PassphraseKeyProvider` (Argon2id) è portabile |

Conteggi di riferimento su Fedora (2026-09-09, fine R9): **Core 143 · Data 476/494 (i 18 rossi sono tutti e soli i DPAPI) · Plugins 66 · ExportEngine 37**.

### Prerequisito

Nessun `dotnet` di sistema: SDK userland, senza root.

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh && chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

### Comandi

Da **2026-09-09 non serve più passare il RID a mano**: `Directory.Build.props` fissa `win-x64` su Windows e altrove prende il RID dell'host (`linux-x64`, `osx-arm64`). Il nativo di SkiaSharp per Linux/macOS è una `PackageReference` condizionata all'OS in `Recordum.Plugins.ExportEngine`: senza, l'assembly managed si carica e poi esplode al primo tipo (`DllNotFoundException: libSkiaSharp`) — e il runner **aborte a metà**, quindi il totale test sembra più basso invece che rosso.

Da **2026-09-09 non si passa più `-p:UseAppHost=false` a mano**: è il default in `Directory.Build.props` (workaround per il lock `CreateAppHost` di DriveFS sulla VM Windows), e passarlo da riga di comando è una *global property* che vince su qualunque override nel `.csproj` — il che ora **rompe** la build della soluzione, perché `tests/Recordum.Avalonia.Tests` (xunit v3) sovrascrive quel default a `true` apposta e xunit v3 pretende l'apphost nativo.

```bash
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true
dotnet test tests/Recordum.Core.Tests            -p:EnableWindowsTargeting=true
dotnet test tests/Recordum.Data.Tests            -p:EnableWindowsTargeting=true
dotnet test tests/Recordum.Plugins.Tests         -p:EnableWindowsTargeting=true
dotnet test tests/Recordum.Plugins.ExportEngine.Tests -p:EnableWindowsTargeting=true
```

`EnableWindowsTargeting=true` serve perché la soluzione contiene progetti `net10.0-windows`: senza, il restore li rifiuta e non compila **niente**.

### Identità git

Il clone nuovo non aveva `user.name`/`user.email` (i commit falliscono con *"Author identity unknown"*). Impostati **locali al repo**, non globali:

```bash
git config user.name "Luca Foglia" && git config user.email "luca.foglia@edu-its.it"
```

### Cosa vuol dire per il prodotto

Il confine non è arbitrario: cinque progetti su sette sono `net10.0` e **portabili per costruzione** — è la promessa di §4 di CLAUDE.md ("Core/Data/Plugins riutilizzati senza modifiche in Fase 5 e 7"), e questa è la prima volta che qualcuno la verifica su una macchina non-Windows. Windows-only è la **shell**: WPF (`Microsoft.WindowsDesktop.App` non esiste su Linux e non esisterà) e il *renderer* dei chart, mentre il lato query dei chart sta già in `Recordum.Data`.

Un desktop Linux visualizzabile richiederebbe quindi **una UI diversa, non un port del core**. Nota per quando se ne parlerà: **MAUI (Fase 5) non copre il desktop Linux** — fa Android/iOS/Mac/Windows. Le strade reali sono **Avalonia** (il parente stretto di WPF) o la **PWA** già prevista fra le fasi future.


## Log sessione 2026-09-11 (VM Windows) — handoff per Linux

Sessione breve sulla VM per chiudere ciò che Linux non può eseguire di R9. Branch **`r9-day-boundary`**, pushato.

### Fatto

- Pull dei 9 commit arrivati da Linux (R9 chiuso + review di branch, W25-2 / schema 2.18, RID per-host, spec+piano Avalonia).
- Build completo della soluzione sulla VM col nuovo `Directory.Build.props`: **0 errori** (il RID per-host non rompe Windows).
- **Tutte le suite sulla VM**: Core 145 · Data 496 (i 18 DPAPI inclusi, qui girano) · WPF 1044 · Plugins 66 · ExportEngine 37 · ChartEngine 18.
- **Sonde "rosso prima"** di tutti i siti WPF di R9, due lacune di copertura chiuse con test nuovi, un test con fixture sbagliata corretto — dettaglio in `docs/phase-4-issues.md` §"Verifica sulla VM di R9".

### Da riprendere

- **Su Linux**: nulla di R9 è bloccato lì. Il lavoro naturale è la **fetta Avalonia** (`origin/avalonia-linux-slice`, piano `2026-09-09-avalonia-linux-slice.md`). Nota: il suo Task 4 tocca test WPF che **su Linux compilano ma non girano** — vanno rilanciati sulla VM, come qui.
- **Sulla VM, con l'autore**: la **verifica a schermo di R9** (il `reset_time` ora ha effetto; un numero di streak può cambiare al primo avvio ed è corretto), il **backfill sul DB vero** solo dopo backup (Task 11 Step 7, disciplina E21), poi il **merge** di `r9-day-boundary`.

### Gotcha di questa VM

- **Commit con messaggio multilinea**: una here-string PowerShell passata a `git commit -m` è stata **negata da un guard** con un messaggio senza senso (*"Remove-Item on system path '/' is blocked"*) — il comando non conteneva alcun `Remove-Item`. Scrivere il messaggio in un file e usare `git commit -F <file>`.
- **Filtri `dotnet test`**: filtrare per **nome di classe**, non di file (v. `QuickAddViewModelBaseTests` nel tracker), e leggere sempre il totale eseguito: un filtro che non seleziona niente somiglia a una sonda verde.

---

## Handoff per la VM — onda F0 (Avalonia come piattaforma), 2026-09-10

Una cosa sola da eseguire, e **solo quando serve**: sblocca il Task 7 dell'onda `F0` (la traslocazione
dei 126 ViewModel in `Recordum.Presentation`). Fino ad allora il porting prosegue su Linux senza
toccare la VM.

### Perché serve

Il Task 7 **rinomina 126 file**. Se R9 avesse bisogno di correzioni, farle atterrare *attraverso* una
rinomina di quella taglia è l'unico scenario davvero brutto dell'intera onda. Questa esecuzione dice se
quel rischio esiste — niente di più. **Non** serve mergiare R9 in `main`, **non** serve la verifica a
schermo: quelle si fanno quando conviene.

Il debito che copre: `r9-day-boundary` porta **47 file sorgente WPF e 46 file di test (9 nuovi) mai
eseguiti**, perché la suite WPF non gira su Linux.

### Il comando

PowerShell, dalla radice del repo sulla VM:

```powershell
# 1) R9 da solo — è questa la risposta che sblocca il Task 7, ed è la prima per un motivo
git checkout r9-day-boundary
dotnet test tests\Recordum.WPF.Tests --nologo -- xUnit.ParallelizeTestCollections=false

# 2) poi il branch del porting, che aggiunge i Task 1-5 dell'onda F0
git checkout avalonia-linux-slice
dotnet test tests\Recordum.WPF.Tests --nologo -- xUnit.ParallelizeTestCollections=false
```

**L'ordine non è cosmetico: serve a sapere di chi è la colpa se qualcosa è rosso.** I Task 1-5 hanno
toccato **50 file** fra `src/Recordum.WPF` e i suoi test, e non sono più solo righe di `using` come
quando questa sezione è stata scritta la prima volta: ci sono le firme di quattordici costruttori
cambiate dalla cascata del battito (Task 4), il modello della tinta del calendario e un terzo valore
nel `HabitColorTintConverter` (Task 5). **Nessuna di quelle modifiche è mai stata eseguita**, perché
la suite WPF non gira su Linux — sono tutte verificate dal solo compilatore.

Quindi: se la (1) è verde e la (2) è rossa, il problema è del porting e lo sistemo io. Se è rossa
già la (1), è R9. Senza la (1) prima, le due cose sono indistinguibili.

### Tre cose da sapere prima di lanciarlo

1. **La serializzazione non è opzionale.** `-- xUnit.ParallelizeTestCollections=false` è il workaround
   di **W24-E1**: in parallelo la suite WPF su questa VM si appende **a zero fallimenti**. Se si appende
   lo stesso, **rilanciare, non indagare**.
2. **Non passare `-p:UseAppHost=false`.** Non è più un flag da riga di comando: è il default in
   `Directory.Build.props`, e da CLI diventa una *global property* che rompe la build di
   `tests/Recordum.UI.Tests` (xunit.v3 pretende l'apphost nativo).
3. **Gotcha DriveFS.** `git status` può segnalare file **modificati che non lo sono** — worktree, indice
   e HEAD con lo stesso hash — e questo basta a far rifiutare un `git checkout`. Si sblocca con
   `git checkout -- <file>`, che sul contenuto è un no-op.

### Cosa riportare indietro

La riga finale di ciascuna esecuzione, così com'è:

```
Passed!  - Failed: 0, Passed: NNN, Skipped: 0, Total: NNN
```

Se sono **entrambe verdi**: il Task 7 è sbloccato, e non serve altro dalla VM per un pezzo — con in più la
conferma che i Task 1-5 non hanno rotto niente, che è la sola prova possibile per 50 file che qui si
possono solo compilare.
Se è **rossa**: i nomi dei test falliti bastano. Non serve indagare sulla VM — è esattamente
l'informazione per cui questa esecuzione esiste, e va decisa prima di rinominare 126 file.

### Cosa NON fare in questa sessione

Il merge di `r9-day-boundary` in `main`, gli step "rosso-prima" dei task WPF di R9, e la verifica a
schermo. Sono tutti rimandabili e nessuno blocca il porting. **Nota per quando li farai**: la pila è
`main → wave-2-visual-pass (10 commit) → r9 (20 commit)`, quindi mergiare R9 fa atterrare **anche** il
pass visivo dell'Onda 2 — è una scelta da fare sapendola. E alla verifica a schermo, un numero di streak
**può cambiare al primo avvio**: passando da UTC a locale lo storico si ri-interpreta. È corretto, non è
una regressione.
