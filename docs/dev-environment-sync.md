# Sincronizzazione ambiente Claude Code — VM ↔ Mac

> **Nota operativa interna** (non è codice né stringa utente). Serve a tenere allineati i due ambienti da cui si lavora a Recordum con Claude Code. Questo file **viaggia via git**, quindi è leggibile da entrambe le macchine. Tienilo aggiornato come fonte di verità sullo stato degli ambienti.

## Contesto

- **Primario — VM Windows**: `C:\Users\luca.foglia\Documents\recordum` (PowerShell, `taskkill`, RID `win-x64`).
- **Secondario — Mac**: `/Users/lucafoglia/Desktop/recordum` (usato quando la VM non è raggiungibile).
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

### Output VM (incolla qui)

> _(da compilare alla prossima sessione sulla VM)_

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

## Log sessione 2026-06-23 (da macOS) — handoff per la VM

Lavoro committato su `fase-4-polish` (3 commit). Sulla VM: `git pull`, poi esegui la **checklist di verifica** qui sotto — su macOS NON si eseguono né la WPF né i test SQLCipher, quindi tutta la verifica runtime/visiva è da fare sulla VM.

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
- [ ] **R-emptyState**: Home senza abitudini (Attive/Archiviate/Cestino) → **icona line + titolo** centrati, icona diversa per tab (Sprout/Moon/Sparkles); workspace senza tab → BookOpen + testo. Light+dark. Rif: `docs/empty-state-preview.html`.
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

**Altro:**
- [ ] **CR8** (opzionale): `dotnet ef migrations add` per riallineare il seed `schema_version`/ModelSnapshot a **2.11** (oggi il costante è `2.9`; impatto runtime zero, è solo igiene).
