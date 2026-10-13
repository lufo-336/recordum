# Recordum — Fase 4 issue tracker

> **Scopo**: lista vivente dei bug e delle rifiniture aperte in Fase 4. Sostituisce le sezioni "Bloccanti / Rifinitura" che erano in `CLAUDE.md` §7. CLAUDE.md mantiene solo il pointer.
>
> **Stati**: `reported` (utente lo segnala, non ancora investigato) → `verified` (riprodotto da claude / utente conferma stato attuale) → `triaged` (root cause ipotizzata + scope chiaro) → `in-progress` (fix in lavorazione su un branch) → `fixed` (commit fatto, build verde) → `verified-fixed` (utente conferma sul build).
>
> Quando un issue raggiunge `verified-fixed`, spostalo in fondo nella sezione "Risolti" con il commit SHA del fix.

## Indice — cosa resta aperto e dove si chiude (aggiornato al 2026-10-06)

> Una pagina per sapere cosa è ancora aperto e **dove si chiude**. Il dettaglio di ogni voce sta più sotto, nella sezione
> della sua onda. I problemi trovati dalla revisione del 2026-09-25 (`RV-*`) sono nel piano del Passo 0, non qui; i
> finding di F1 (`F1-*`) sono nella sezione "Onda F1". **Quando una voce cambia stato, si aggiorna anche questo indice.**

**Prima del dogfooding — logica da correggere in `Recordum.Presentation` (lotto P-6 della revisione), con test provati rossi**
- **F0-Y1 = A5** — la heatmap-anno si riempie per righe (le righe in basso verdi tutto l'anno) · `reported`
- **W2V-15** — il builder compone un diario con una durata che nessuna superficie sa registrare · `verified`
- **W2V-5** — il cerchietto del calendario dice tre cose diverse a seconda del tipo · `verified`
- **W2V-10** — con Lancia disabilitato l'hint elenca tutte le condizioni, non quella che manca · `verified`
- **W2V-12** — il simbolo di un'unità non compare da nessuna parte · `verified`
- **W2V-13** — due unità con lo stesso nome nel picker, a schermo lo slug · `verified`
- **W2V-14** — i mattoni illegali non sono disabilitati: il click è muto · `verified`
- **W2V-17 (a, b)** — "Decimali" insegna il modello sbagliato; l'input numerico non dice le sue regole · `verified`
- **W2V-20** — "gesto primario" è gergo di progetto · `verified`
- **F0-M4, F0-M5, F0-Q1, F0-T3** — residui di F0: guardia di C1 a metà, timer del chart builder non disposto,
  `QuitCommand` senza consumatori, nessun test che il ticker riparta dopo `Clear()` · `reported`
- **R9-M1…M8** — i minori della review di R9 · `reported`

**Trovati in F1 (sezione "Onda F1")**
- **F1-PERF1** — la Home con 300 righe: era ≈ 3,8 s a caldo contro il budget di 500 ms (E9/E20); ora ≈ 115 ms in
  headless con la lista piatta virtualizzata (branch `f2-perf1-home`) · `fixed`, manca la verifica a schermo · **F1-SEC1** — `recordum.db`/`-wal`/`-shm` nascono 0644: nella lista del `/security-review` prima di
  pubblicare · **F1-RAIL1** — rail e Ctrl+4/Ctrl+5 spenti: F2, F6 e F7 riaccendono i propri · **F1-ICON1** — il glifo
  "riduci a icona" era vuoto: misurato e `fixed` (verifica a schermo su Windows)
- Informativi: F1-1, F1-2, F1-3, F1-5, F1-8, F1-9, F1-10, F1-DND1, F1-LOG1 · `reported`

**Trovati in F2a (sezione "Onda F2a")**
- **F2a-WPF1** — il ✎ delle registrazioni in WPF non fa nulla · `reported`
- **F2a-1** — un gruppo in F2a ha l'intestazione ma né chip né colonne (la Panoramica è F3) · **F2a-2** — vista Anno e menu di
  salto mese/anno del calendario rimandati a F3 · **F2a-3** — il cronometro di cella di una serie compare nella barra dei timer
  e il suo Stop registra una durata (CM-4) · **F2a-4** — il purge toglie `active_timer` senza evento: la barra tiene una riga
  morta il cui Stop lancia · **F2a-5** — Presentation traccia con `Debug.WriteLine`: in Release i guasti in background sono
  invisibili · **F2a-6** — F0-Y2 `fixed` nel calendario Avalonia; W2V-4/U4 **non** risolti (vedi sotto) · **F2a-7** — rinomina dalla Home non arrivava all'intestazione · `fixed` · **F2a-8** — fascia astinenza: "1 tentativi (90g)" su un periodo appena iniziato · `reported` · **F2a-9** — il flip di Ripetibile non pubblica `HabitUpdated` · **F2a-10** — `ActiveTimerService.StopAsync` non atomico · `fixed` · **F2a-11** — eccezioni dei comandi async → `CrashLogger` `Handled=false` · `fixed` · **F2a-12** — `TimerStatusBarViewModel` transient con `IHabitNodeService` scoped (F2a-9 e F2a-12 `reported`) · stati: F2a-1…F2a-5 `reported`, F2a-6 `fixed` solo per F0-Y2 (W2V-4/U4 `reported`)

**Da ricontrollare sulle schermate Avalonia, onda per onda**
- **F1 Home** — W2V-2 (zero-state muto), W2V-3 (nomi tagliati senza ellissi), A7 (zona destra della riga tagliata
  nell'albero stretto), F0-12 (banda bianca in tema scuro), W2V-7 (conferma: su Avalonia è già una finestra nostra)
- **F2 Registrazione** — la sezione §3 della checklist del pass visivo (tap counter, somma, cronometro, export)
- **F3 Dettaglio** — W2V-4 (il cerchietto copre il numero del giorno), U4 (numero scentrato nel cerchio della heatmap),
  R8 (la card Goal non porta a Configura)
- **F4 Diario** — W2V-8 (il composer non dice dove scrivere), B14 (editor di completamento o colonna Note)
- **F5 Configura e builder** — W2V-9, W2V-11 (unità e scale non si modificano né si cancellano), W2V-16, W2V-18,
  W2V-19, A8 (le sezioni sbordano dalla colonna), A12 (l'autosalvataggio non si percepisce), W23-VF4 (il cronometro non
  si scopre dal builder), W23-VF5 (le abitudini "cronometro" di prima restano escluse)
- **F6 Impostazioni, backup, export** — F0-8 (il dialog "Crea backup" è opaco), F0-10 (l'errore di restore esce come
  codice grezzo), F0-T2 (i test di Settings sono ancora in `Recordum.WPF.Tests`)
- **F7 Grafici** — A4 = W23-VF7 (grafici del Dettaglio vuoti, card bianche in tema scuro), W1a-VF1 (LiveCharts in tema
  scuro)

**Le decide il dogfooding — questioni di modello**
- **CM-1** — "ripetibile" inerte sui tap-counter · **CM-2** — Sonno: totale o sessioni · **CM-3** — durata e unità
  separate (in parte risolta: l'unità è obbligatoria) · **CM-4** — più metriche durata e più cronometri (`active_timer` è
  UNIQUE per abitudine) · **W23-VF8** — il badge del calendario conta le occorrenze, il totale somma

**Verso il pubblico — dopo F8**
- **F0-14** — le notifiche si configurano ma nessun motore le fa scattare (E14)
- **F0-6** — l'app in inglese di default (il selettore di lingua c'è già) e il seeder demo
- **0.3-residuo** — auto-backup osservato nel tempo; passphrase opt-in provata con un backup fresco
- **R5, A10** — accessibilità: nomi e pattern di automazione nelle onde, l'audit completo dopo la v1.0

**Ambiente e note informative**
- **W24-E1** — la suite WPF in parallelo può appendersi: la CI la serializza, e sparisce con WPF a F8
- **F0-F1** — `SemiBold` risolve sul Bold (Source Serif 4 è spedito senza il peso 600) · **F0-M6** — nomi di file e tipi
  poco scopribili · **F0-T1** — la fixture `FakeReferenceData` ha una scala 1-5 con Min/Max a 0
- **A11** — click sintetici sull'albero WPF senza effetto: superato insieme a WPF

**Chiuse, anche dove il titolo più sotto dice ancora "aperto"**
- **B1** (salvataggio note) `fixed` · **B11** (builder e registrazione) `verified-fixed` · **B-calYearRender**
  `verified-fixed` · **CR8** chiuso dalla migration `AbstinencePeriodV212` · **U3, U5** `verified-fixed` · **U6, U8, U10**
  fatti nelle Onde 2.1, 2.3, 2.2 · **F0-13** (selezione o completato, Onda 2.2) · **A6** (parola del tipo tolta, 2.1-bis)
- **Superate**: **B2** (tema dell'editor Toast UI: l'editor sparisce con F4) · **B3** ("tante altre cose non
  funzionano", maggio: triata voce per voce da allora)

## Onda 0 — Verità di base (checklist di verifica, dal piano v1.0 2026-07-03)

> Mappa: `docs/superpowers/specs/2026-07-03-v1-redefinition-plan-design.md`. Nessun fix in questa sessione: solo misurare e registrare. Per ogni voce: **stato** (OK / rotto / brutto) + **sintomo esatto** (cosa, dove, screenshot se serve). I findings nuovi diventano voci del tracker.

### 0.1 Chart / esplorazione dati (il tracker li dà "rotti" al 2026-06-19 — mai più guardati)
- ◐ **Dettaglio** (sessione 2026-07-03, DB popolato con `--seed-demo`, screenshot autore): **heatmap anno OK** (Camminata/Diario/Lettura mostrano i dati) · **streak TUTTI a 0** (→ F0-2) · **andamento "Dati insufficienti"** (→ F0-3) · **"Ora del giorno" vuota** (→ F0-4) · resistance (Niente fumo) ancora da guardare.
- ✗ **Schermata Charts**: creazione chart custom **impossibile e crash** (→ F0-1).
- ☐ Chart esistenti/salvati si aprono senza errori. *(non testabile finché F0-1 impedisce di crearne)*

**Findings 0.1 (tutti `verified`, riprodotti a schermo il 2026-07-03 — nessun fix in Onda 0, triage → Onda 1a):**
- **F0-1 — Chart builder incompleto + crash al salvataggio** · gravità **alta** (perdita processo). La riga-serie in `ChartsView.xaml` (~55-66) ha **solo TextBox etichetta + ✕**: nessun picker abitudine/metrica → `ChartSeriesRowViewModel.HabitNodeId` resta `Guid.Empty` → `ChartService.CreateAsync` → SQLite Error 19 `FOREIGN KEY constraint failed` → `DbUpdateException` **non gestita** → **l'app crasha** (stack: `ChartBuilderViewModel.SaveAsync` via `AsyncRelayCommand`). Due difetti distinti: (a) il builder è UI-skeleton — con la sola etichetta non si può costruire un chart valido (viola "nessuna UI placeholder"); (b) nessuna validazione né error handling sul Save — anche a builder completo, un errore DB non deve uccidere il processo.
- **F0-2 — Streak attuale/record/Cicli sempre 0 nel Dettaglio** · malgrado ~100 giorni di completions (Camminata action 80%, Diario journal, Lettura counter). Ipotesi da triare: config seedate senza `streak_mode` utile, oppure lo `StreakSummaryViewModel`/aggregator non riceve i dati (le completions del seeder sono `IsRetroactive=true` — verificare se incide).
- **F0-3 — "Andamento" dice "Dati insufficienti"** · anche su counter con metrica valorizzata ogni giorno (Lettura/Pagine, Acqua/Bicchieri). Da triare insieme a F0-2 (stessa pipeline dati del Dettaglio?).
- **F0-4 — "Distribuzione oraria" vuota** · le completions seedate coprono le 6-23, la sezione non mostra nulla.
- *Nota estetica collegata (per Onda 1a, non separata)*: la schermata Charts è visivamente skeleton — ListBox default, bottone "✕" testuale non Lucide, nessun empty-state.

- **Esito 0.1 resistance (2026-07-03, screenshot autore)**: **OK sostanziale** — heatmap period-aware ✓ (giorni puliti in colore habit, gap ricadute), cronometro vivo ✓ ("20g 0:03:23"), Ricadute (90gg)=3 ✓, andamento/orario nascosti ✓ (fix U9.1 confermati a schermo). **Note minori**: (a) la barra rossa spessa segmentata sotto le stat (streak timeline dai periodi) è funzionale ma visivamente grezza — nessuna label/legenda; (b) "Nessun goal attivo — Impostane uno" mostrato anche su resistance: il goal ha senso per l'astinenza? da decidere in Onda 2; (c) incoerenza conteggio: Dettaglio "20g" (tempo trascorso) vs export `current_streak: 21` (giorni di calendario inclusivi) — convenzione da unificare.

### 0.2 Export (plugin ExportEngine)
- ◐ Ognuno dei 5 formati (PDF / HTML / Markdown / JSON / CSV) su dati veri; file aperto e letto. *(2026-07-03: provati i 5 formati su "Niente fumo" → tutti prodotti e ben formati, MA vedi F0-5; da riprovare su un'abitudine ricca, es. Lettura, per giudicare il contenuto)*
- ✗ Export di un'abitudine resistance (→ F0-5).

**Findings 0.2 (`verified` 2026-07-03, file su Desktop dell'autore ispezionati):**
- **F0-5 — Export resistance non period-aware** · CSV = solo header (zero righe); JSON: `completions: []`, `heatmap: []`, `total_completions: 0` (fuorviante — l'abitudine ha 4 periodi/3 ricadute); MD/HTML idem. Solo `current_streak`/`longest_streak` leggono i periodi (CR11). Le **registrazioni** (periodi con inizio/fine) mancano del tutto dall'export: `ExportQueryService` va reso period-aware come heatmap/streak del Dettaglio (Onda 1 o insieme a F0-1..4).
- *Nota superata (export Lettura, 2026-07-03)*: su un'abitudine ricca MD/HTML/CSV/JSON contengono il corpo dati completo (38 completions in tabella) — lo "scarno" di Niente fumo era solo F0-5. Due findings nuovi dall'export ricco:
- **F0-7 — Nome metrica mai esportato** · colonna `metric` vuota in CSV/MD e `"metric": null` in JSON su tutte le 38 righe di Lettura, che ha la metrica "Pagine" valorizzata (i *valori* ci sono, manca il join col *nome*). `ExportQueryService`/formatter da correggere.
- **Conferma triage F0-2** · anche l'export di Lettura riporta `current_streak: 0, longest_streak: 0` → il bug streak NON è nella UI del Dettaglio ma nella pipeline condivisa (aggregator/query), visto che Dettaglio ed export leggono gli stessi zeri. (Su Niente fumo invece gli streak sono corretti → il ramo periodi post-U9 funziona, è il ramo completion-based a essere rotto.)

### Addendum 2026-07-03 (sera) — domanda dell'autore sul core abitudini
- **F0-14 — Le notifiche si configurano ma non scattano mai** (`verified` da ispezione codice): esiste solo la sezione editor (`NotificationsSectionView` + VM, config persistita); **nessun motore** (nessuno scheduler, nessun toast, niente tray). Decisione autore: **motore minimo in v1.0** (toast Windows ad app aperta/in tray) → Onda 4. Feature configurabile-ma-morta = inaccettabile per il rilascio pubblico.
- **Parità template↔builder** (decisione autore): **piena** — R-builderCustomUnit e R-builderCustomScale **promossi** da "declassate" a scope Onda 2.5 (unità/scale custom create inline dal builder). Verifica: matrice di parità nella nuova **Onda 2.9** ("verità sul core abitudini", vedi piano §10), che copre anche scheduling/recovery/pause/goal/timer/tag/reset-time a schermo + rassegna del catalogo template.

### 0.L Lingua (segnalazione autore 2026-07-03)
- **F0-6 — L'app deve potersi presentare completamente in inglese** · Oggi la UI segue la culture dell'OS (resx: VM italiana → tutto in IT); i contenuti del seeder demo sono in italiano. Per la v1.0 pubblica (principio English-first): decidere **default EN + selettore lingua in Impostazioni** (o forzare EN finché non c'è il selettore) e valutare seeder demo in EN. Decisione di design → collocata in Onda 4 (onboarding/primo avvio), ma da tenere presente in ogni stringa nuova da qui in poi.
  - **Scope allargato dall'autore (2026-08-17)**: Impostazioni deve offrire **la cultura**, non solo la lingua — formato data, separatore decimale, lingua. Sono **due manopole distinte**, come le separa Windows stesso: **lingua dell'interfaccia** (`CurrentUICulture` → quale `.resx`) e **formato regionale** (`CurrentCulture` → date, numeri, e quindi anche **quale separatore decimale l'input accetta**, vedi W2V-17c). Vanno separate perché un utente italiano che vuole l'app in inglese continua a scrivere `2,5` e a leggere `17/08/2026`. Meccanica: due AppSetting + un service allo stile di `ThemeService`/`AppFontService`, applicati in `App.OnStartup` **prima** di costruire la UI (il `Language` dei `FrameworkElement` si imposta una volta lì — cambiare a caldo richiede di riapplicarlo all'albero visuale già montato, quindi la v1 può chiedere il riavvio). **Il fix di W2V-1/W2V-17c è la fondazione di questa feature, non un lavoro sprecato**: imposta la cultura corrente come sorgente unica del formato; il selettore poi cambia solo *da dove* quella cultura arriva (OS → AppSetting).
  - **Selettore `fixed` 2026-08-17** (anticipato su richiesta dell'autore): `AppCulture { System, English, Italian }` in Core, `ICultureService`/`CultureService` (specchio di `AppFontService`, **senza evento di cambio** — nessuna delle due scelte è applicabile a caldo), AppSetting `ui.language` e `ui.format`, due gruppi di radio in Impostazioni → Aspetto con l'esempio accanto alle voci di formato (`Italiano — 17/08/2026 · 2,5`) e la nota di riavvio. `System` non è una cultura sola: si risolve nella `CurrentUICulture` per la lingua e nella `CurrentCulture` per il formato, così le due manopole conservano la distinzione che Windows stessa fa. Un test di vista (`SettingsCultureViewTests`) monta la finestra vera, perché i due gruppi vivono dentro un `DataTemplate` e un ViewModel non li vede; **provato contro il codice rotto** (rinominato un `GroupName`: rosso).
  - **Correzioni dopo la prima occhiata dell'autore (2026-08-17)**: le manopole erano finite in **Aspetto**, dove non appartengono — Aspetto è come l'app *appare* (tema, carattere), la cultura è come si *comporta* (quali stringhe legge, come scrive le date, quale separatore accetta). Spostate in **Generale**, accanto all'orario di reset, e le proprietà con loro (`GeneralSettingsViewModel`, non `SettingsViewModel`). Seconda correzione, sulla copy: le voci dicevano `Italiano — 17/08/2026 · 2,5`, cioè enunciavano un fatto senza dire di che cosa fosse un fatto. Ora le voci portano il solo nome e sotto c'è **un'anteprima viva** che segue la selezione e nomina le due cose (`Con questa scelta una data si legge 17/08/2026 e un numero decimale si legge 2,5`), costruita con la cultura risolta e la data di oggi da `IClock`.
  - **Restano aperti di F0-6**: il **default a inglese** per la v1.0 pubblica e il **seeder demo in italiano**. Il selettore non li decide.

### 0.3 Backup/restore runtime (CR1–CR4 finora solo test-verified)
- **F0-8 — Dialog "Crea backup" opaco: l'autore non è riuscito a creare un backup** (`verified` 2026-07-03, screenshot + codice). Il bottone Crea si abilita solo con passphrase ≥12 char uguale nei due campi (`BackupDialogViewModel.CanRun`:54-58), ma il dialog non comunica nulla: (a) il **secondo PasswordBox non ha etichetta** (è la conferma — nel layout pare un campo misterioso); (b) **nessun hint sul requisito** dei 12 caratteri; (c) l'indicatore di forza mostra l'**enum grezzo non localizzato** ("None"/"Weak"/"Ok"/"Strong"); (d) il bottone disabilitato non spiega perché. UX da rifare (Onda 3 o insieme al blocco backup di Onda 1/4).
- *Nota autore collegata*: "il backup delle Impostazioni è differente da quello di sinistra" — due superfici backup con modelli diversi (icona ribbon → dialog manuale `.recbak` con passphrase; Impostazioni → auto-backup DPAPI + history). Concetti realmente diversi ma presentazione incoerente: da unificare/spiegare (stessa onda di F0-8).
- ✓ Backup manuale → restore: backup creato ✓ (con passphrase 12+, dopo F0-8); anteprima OK ("Schema: 2.7" → F0-9); **restore completato con dati identici** (screenshot autore 2026-07-03: heatmap, calendario, registrazioni al loro posto). Round-trip CR1/CR4 verificato a video.
- ✓ Restore di un `.recbak` **corrotto a metà**: testati sia troncato al 50% sia con 64 byte capovolti nel mezzo (2026-07-03, screenshot) — **entrambi rifiutati** in anteprima, bottone Ripristina disabilitato, dati intatti. Comportamento CR1/CR3 corretto. Ma vedi F0-10 sul messaggio.

**Findings 0.3 (`verified` 2026-07-03, screenshot + codice):**
- **F0-9 — Il manifest del backup stampa uno schema hardcoded "2.7"** · `BackupService.cs:25` `CurrentSchemaVersion = "2.7"` è una costante mai aggiornata: un backup creato oggi da un DB a schema **2.12** viene etichettato 2.7 (l'anteprima lo mostra). Il guard CR2 lato restore legge la versione corrente dal DB reale, ma il **writer** no → l'etichetta è sempre falsa e il confronto "backup più nuovo dell'app" non potrà mai scattare correttamente. Fix: leggere `schema_version` dall'AppSetting al momento del backup (come fa `RestoreService.GetCurrentSchemaVersionAsync`). Gravità **media-alta** (integrità metadati, futuro-proofing).
- **F0-10 — Errore restore mostrato come codice grezzo** · il dialog mostra "invalid_passphrase" in grigio semi-illeggibile: è `ErrorMessage = ex.Code` (raw, non localizzato) invece del messaggio; e per un file corrotto è anche fuorviante (AES-GCM non distingue passphrase errata da file danneggiato — il messaggio giusto è "passphrase errata **o file danneggiato**", che infatti esiste già in `AesGcmBackupCipher.cs:119` ma non arriva alla UI). Stessa famiglia UX di F0-8.
- ~ Restore di un backup con `schema_version` più recente (CR2): **non testabile a video finché F0-9** (lo stamp hardcoded 2.7 rende impossibile produrre un backup "più nuovo" reale); il guard resta coperto dagli unit test `SchemaVersionGuardTests`. Ritestare a video dopo il fix F0-9.
- ☐ Auto-backup scheduler: scatta secondo `backup.auto.*`, riga in backup history.
- ☐ Passphrase opt-in (Argon2id): transizione DPAPI→passphrase e riavvio.

### 0.4 Sweep visivo accumulato (rese mai ri-verificate a schermo)
- ☐ Context menu (tasto destro riga habit): pannello flottante arrotondato, hover accento, submenu chevron.
- ☐ Dropdown ComboBox: ombra + raggio, testo nitido.
- ☐ ToolTip: raggio 6 + ombra (non rettangolo Aero).
- ☐ Divisori colonne (sash): hairline a riposo, accento su hover/drag.
- ☐ Empty-state per-tab (icona + titolo) + CTA "Crea la prima abitudine" solo su Attive.
- ☐ TabStrip workspace: angoli arrotondati, X Lucide, underline accento.
- ☐ Brand mark "R" serif in title bar.
- ☐ Scrollbar: hover/drag scurisce+allarga, min-size thumb.
- ☐ Scorrimento colonne fill-then-scroll (tante colonne aperte → scroll orizzontale, mai sotto 320px).
- ☐ Color Picker v2: anello selezione (F1), ramp tonale (F2), contagocce schermo (F3), preferiti cap 12 (F4).
- ☐ CR13 durate visibili in Cronologia · CR20 date corrette in BackupDialog · CR22 31 dic presente nella year-heatmap · CR5 State valore-0/1-based nel calendario.

**Esito 0.4 (2026-07-03, screenshot autore)** — sweep sostanzialmente **promosso**: context menu ✓ (pannello arrotondato, niente Aero, submenu chevron), tooltip ✓, sash colonne ✓ ("sembra ok"), Color Picker v2 ✓ (HSV + ramp Tonalità + Preferiti/+Aggiungi + hex + contagocce tutti presenti), tab strip ✓ (pin + X), brand mark ✓, scrollbar ✓. CR22 plausibile (heatmap arriva al bordo destro). Findings nuovi:
- **F0-11 — Tab Configura: enum grezzi in inglese e layout scheletrico** (`verified`, screenshot). I due ComboBox in testa mostrano i valori raw "Habit"/"Action" (NodeType/HabitType non localizzati né presentati); attorno c'è il vuoto — la riga type/color fluttua su fondo nero con doppia label "Configura". Da incrociare con il pilastro "builder/editor per-tipo" dell'Onda 2 (stessa superficie).
- **F0-12 — Banda bianca spontanea sulla testata Home in tema scuro** (`verified`, screenshot + conferma autore "sto facendo tutto col tema scuro"): l'intera fascia alta (titolo "Recordum", tab Attive/Archiviate/Cestino, +Nuova, sfondo dietro la riga filtri) è apparsa **bianca** mentre righe/sidebar/resto restavano scuri. Trigger non identificato (nello screenshot era attivo il tooltip "Grafici" del ribbon; Home a piena larghezza, nessun tab workspace aperto). Da riprodurre/triage: qualche superficie della testata Home perde il `DynamicResource` del background? Gravità **media** (theming, visibile subito).
- **F0-13 — RISOLTO non-bug + finding minore derivato** (2026-07-03): cliccando il 2 luglio la lista Registrazioni mostra correttamente la voce "2026-07-02 09:31" (con matita/X) → la lista records funziona; il 3 luglio era solo **selezionato**, non completato. **Finding minore reale**: nel calendario della Cronologia **il giorno selezionato è visivamente indistinguibile da un giorno completato** quando il colore dell'abitudine è verde (stessa tinta) — ha ingannato sia l'autore sia il triage. Differenziare selezione (bordo/anello) da completamento (fill) → Onda 2 (esperienza per-tipo, superficie calendario) o Onda 3.

### 0.5 Perf
- ✓ Home <500ms: `HomeTreeBudgetTests.GetHomeTreeAsync_p95_under_300ms_on_seeded_dataset` **verde sulla VM** (2026-07-03) — p95 <300ms, dentro il budget. *Nota*: misura data-layer; il render WPF non è strumentato (nessuno Stopwatch nel codice UI) — a percezione l'app è reattiva; strumentare solo se in Onda 4 emergono lentezze su macchine più deboli.

### 0.3-residuo (deferiti con motivo)
- ~ **Auto-backup scheduler**: scatta ogni `frequency_days=7` — non osservabile in sessione; verificabile guardando lo storico backup in Impostazioni tra qualche giorno d'uso. Coperto da unit test (`BackupSchedulerHost`).
- ~ **Passphrase opt-in DPAPI→passphrase**: transizione one-shot che ri-cifra il DB — rischiosa da provare a metà sessione di verifica; da testare a parte (con backup fresco in mano) prima dell'Onda 4.

**Esito sessione — ONDA 0 COMPLETATA (2026-07-03, sessione VM con l'autore)**. Bilancio: heatmap/calendario/astinenza/backup-sicurezza/perf **solidi**; chart custom, pipeline streak/andamento/orario del Dettaglio ed export **rotti come temuto**, ora con sintomi e root-cause hypothesis precisi. 13 findings (F0-1…F0-13, di cui F0-13 declassato a minore). Smistamento proposto: **Onda 1a** = F0-1 (builder chart + crash), F0-2 (streak pipeline), F0-3 (andamento), F0-4 (orario), F0-5 (export resistance), F0-7 (nome metrica), F0-9 (stamp schema backup — fix piccolo, data-integrity); **Onda 2** = F0-11 (Configura/enum), selezione-vs-completato (ex F0-13); **Onda 3** = F0-8/F0-10 (UX dialog backup/restore), F0-12 (banda bianca — prima va riprodotta); **Onda 4** = F0-6 (lingua EN + selettore). Deferiti 0.3-residuo: auto-backup scheduler (osservazione nel tempo), passphrase opt-in (test dedicato pre-Onda 4).

## Stato al 2026-06-28 — sessione VM: WebView2 ripristinato + U3/U1 chiusi a schermo

Sessione **sulla VM** (`WSA-2527-04`), app lanciata e verificata a schermo dall'autore. **Suite verde**: `Recordum.Data.Tests` **356/356** + `Recordum.WPF.Tests` **651/651** (dopo i fix, nessuna regressione). 3 commit di codice (`cb92fed` → `15ae730`) + ADR/test-gate (`a795450`).

**Scoperto e chiuso — editor diario (WebView2) era ROTTO sulla VM** (`cb92fed`, **verified-fixed**): il banner "Runtime WebView2 richiesto" appariva *malgrado il runtime installato* (`pv=149.0.4022.98`). Causa: `CoreWebView2Environment.CreateAsync()` senza cartella esplicita mette la user-data folder accanto all'eseguibile del processo → sotto `dotnet run`/`UseAppHost=false` è `C:\Program Files\dotnet\` (read-only) → `E_ACCESSDENIED` → fallback testo. **Stesso hazard per un utente che installa in Program Files.** Fix: cartella esplicita `%LocalAppData%\Recordum\WebView2`. L'editor markdown ricco ora carica.

**U3 — slider scala** (`cdfb748`, **verified-fixed**, "ottima la scala così!"): due difetti distinti emersi a schermo dopo il checkpoint `7d0b6a6`.
1. *Non si poteva salvare il primo step*: il binding `Value` aveva `TargetNullValue=0` — è **bidirezionale**, quindi il thumb a riposo su 0 riscriveva `null` su `SelectedScaleIndex` → `BuildValue` ritornava null → "richiesto almeno un valore". Tolto (la VM semina già index 0).
2. *Le etichette testuali si accavallavano* ("Very lowLow"): centrare parole su ogni fermo è impossibile. Inoltre il fix offset del checkpoint **non funzionava affatto** — il `MultiBinding` stava su un `TranslateTransform` (Freezable) dove `RelativeSource` non risolve → `hostWidth=0` → tutte le label impilate. Riscritto: l'offset converter ora torna un `Thickness` su `Margin` (FrameworkElement, RelativeSource risolve) + clamp; e il layout è **tick per ogni step + etichette solo agli estremi** (min sx / max dx), col valore corrente già mostrato sopra in accento.

**U1 residuo — finestra Esporta** (`15ae730`, **verified-fixed**, "Esporta è perfetto"): i checkbox erano già flat (l'accento `#60A5FA` ingannava); il vero problema era una **fascia nera a destra** = `SizeToContent="WidthAndHeight"` + `WindowChrome` che gonfia la larghezza non-client. Risolto con `Width` fissa + `SizeToContent="Height"` (come gli altri dialog chromed). Bonus: tematizzato `.toastui-editor-md-tab-container` nell'editor (striscia bianca residua accanto ai tab Write/Preview).

**U5 — Expander "Modelli predefiniti"** (`4573434`, **verified-fixed**, "perfetto"): lo stile `Expander` implicito era ancora Aero (freccia blu circolare che stonava sui riquadri). Ri-templato piatto: header full-width + chevron Lucide che ruota, contenuto sotto. Condiviso col section-host dell'editor abitudini (verificato che non regredisce). Bonus stessa sessione (`dc3bb20`, **verified-fixed**): scrollbar del picker spostata in un **canale a destra** (ScrollViewer full-bleed + margine 16 sul contenuto) e **chrome scura sulla finestra "Nuova abitudine"** (il builder è creato in codice in `WpfHabitBuilderLauncher` → applicato `Recordum.ChromedWindow` + Brush.Background + Font.Default; prima aveva la barra titolo nativa bianca).

**Ancora aperti dal resoconto 2026-06-25**: **U4** (centratura badge nel cerchio heatmap — a schermo sembrava ~ok, da rifinire), **U6–U10** (design: quadratini Home type-aware, Food diary veloce, Sleep tracker, Stop smoking=astinenza, Morning routine). Più lo sweep R-* accumulato e **CR8** (regen ModelSnapshot a 2.11).

## Verifica visiva v1.0 — in attesa di resoconto utente (2026-06-19)

App lanciata via skill `run` (build win-x64 verde). Aree da controllare a schermo, da consolidare nel "resoconto stato app" (agenda punto 3) e da triare contro i CR:
1. **Grafici** — sappiamo rotti: sintomo esatto (errore / area vuota / crash) e in quale schermata.
2. **Export** (PDF/HTML/Markdown/JSON/CSV) — parte? produce file leggibile?
3. **Plugin** — qualcosa di visibile/funzionante?
4. **Backup/Restore** — area dei findings gravi CR1–CR4; testare su dati di prova.
5. **Basics**: 5 tipi habit, registrazione, calendario/heatmap, streak, gruppi, note.
6. **UX/theming** — cosa non sembra "da v1.0".

## Resoconto utente 2026-06-25 (Obsidian) — triage verifica visiva v1.0

Fonte: `obsidian_og/Resoconto Recordum.md` (15 screenshot annotati). Primo giro reale dell'autore a schermo. Tutti `reported`, da triare/eseguire. Divisi in **visivi rapidi** (no decisione) e **design/comportamento** (richiedono scelta o brainstorm).

### Stato fine sessione 2026-06-25 (handoff)

**Branch** `fase-4-polish`. Commit di questa sessione, in ordine:
1. `1aebe37` feat: **☰ toggle striscia icone** (non più albero Home) — VERIFIED-FIXED. *(Prima parte di sessione: ripristinato il sizing colonne dopo una regressione dell'esperimento di animazione sidebar, vedi `R-ribbonSlide` rinviato sotto.)*
2. `3c45f9a` feat: **glifo pin** sulla riga abitudine — VERIFIED-FIXED.
3. `ce3573e` feat: **U1 chrome scura riutilizzabile** sulle 5 finestre figlie — VERIFIED-FIXED.
4. `0668462` fix: **U2 dropdown ordinamento** nitido + chevron in colonna dedicata — VERIFIED-FIXED.
5. *(checkpoint)* **U3 slider scala** allineamento label↔thumb — **build pulita, NON verificato a schermo** (utente ha chiuso). Vedi voce U3.

**Da VERIFICARE alla prossima apertura** (priorità):
- **U3** allineamento slider a ogni step (Mood, Sleep quality) — è l'unico non visto a schermo di questa sessione.
- **Residuo U1**: la finestra **Esporta** mostrava **checkbox blu nativi** + corpo molto nero — controllare se lo stile flat dell'app si applica ora che la finestra ha la chrome scura; se no, è un sotto-task (forse uno stile CheckBox non risolto in quella finestra/DataTemplate).

**Da FARE (resoconto, non iniziati)**: U4 (numero centrato nel cerchio heatmap), U5 (toolbar "Modelli predefiniti" da spostare a sinistra), U6 (quadratini Home type-aware — design), U7 (Food diary veloce+foto — design), U8 (Sleep: tasto "1", serie opzionale, unità tempo — design; nota: riusa lo slider di U3), U9 (Stop smoking = cronometro astinenza, = B-abstinence — design/feature), U10 (Morning routine landing — chiarimento). Vedi sotto per dettaglio.

**Suite test**: ✓ **rieseguita 2026-06-27** (copia Drive, Windows) — `Recordum.Data.Tests` **356/356**, `Recordum.WPF.Tests` **651/651** (un solo `BackupSchedulerHostTests` flake da timing nella run completa, verde 2/2 isolato). Il campo nuovo `ScalePoints` (U3) non ha rotto nulla. Nota build: la suite WPF è stata lanciata con `-p:UseAppHost=false` perché su Google Drive File Stream la creazione dell'apphost `.exe` fallisce (`CreateAppHost` IOException su file con sezione mappata da DriveFS) — vedi handoff ambiente 2026-06-27 in `dev-environment-sync.md`.

**Altro rinviato** (non dal resoconto): **R-ribbonSlide** (slide morbido del toggle striscia icone) — vedi sezione "Rinviato esplicitamente (2026-06-25)" più sotto; l'helper `GridLengthAnimation` è in `…/scratchpad/wip-sidebar-anim/`.

**Visivi rapidi (no decisione di design):**
- **U1 — chrome nativo sulle finestre figlie** · **VERIFIED-FIXED** (2026-06-25, "sembra tutto ok"). Impostazioni, dialog "Nuova abitudine da modello", "Crea backup", "Ripristina", "Esporta" usavano la **barra titolo nativa bianca + pulsanti finestra nativi** → incoerente con la custom chrome scura di `MainWindow`. **Fix**: chrome scura **riutilizzabile** — nuovo behavior `Controls/ChromeWindow.cs` (cabla i caption button ai `SystemCommands`) + stile `Recordum.ChromedWindow` con `ControlTemplate` condiviso (`Resources/WindowChromeStyles.xaml`, merge in App.xaml), applicato alle 5 finestre con `Style="{StaticResource Recordum.ChromedWindow}"`. Min/Max seguono `ResizeMode` (NoResize→solo ✕; CanResize→min/max/close); rimosso `WindowStyle="ToolWindow"` dal picker. ⚠️ **Residuo da controllare**: il **checkbox nativo blu** dell'Esporta e il "corpo troppo nero" — se l'Esporta mostra ancora checkbox blu, manca lo stile flat in quella finestra (sotto-task aperto).
- **U2 — dropdown ordinamento sfocato + freccia spuria** · **VERIFIED-FIXED** (2026-06-25). Due cause distinte in `BaseStyles.xaml`: (1) **sfocato** — il `DropShadowEffect` era sul bordo che conteneva anche gli item → WPF rendeva il sottoalbero in un bitmap intermedio = testo soft; spostato su un layer-ombra dedicato (senza testo) + tolto `PopupAnimation="Slide"`. (2) **freccia spuria** — il chevron stava in una colonna *dentro il toggle*, e con valori corti il `ContentPresenter` del testo (disegnato sopra) ne copriva la metà sinistra lasciando spuntare un "/" accanto alla parola; portato il chevron in una **colonna dedicata del template ComboBox** (testo col 0, chevron col 1 da 22px) + cambiato il triangolo pieno in **chevron a linea** (coerenza Lucide). Stessa `DropShadowEffect`-su-testo resta in ContextMenu/ToolTip: applicare il layer-ombra anche lì se risultano soft (non segnalato finora).
- **U3 — slider scala: pallino non allineato alle label** · **IMPLEMENTATO, da VERIFICARE a schermo** (build pulita, commit checkpoint; l'utente ha chiuso prima della verifica). Causa: le label stavano in `UniformGrid` di N celle (centri a (i+0.5)/N) ma i fermi del thumb cadono su **N−1 intervalli** → combaciavano solo al centro ("Neutral" ok). **Fix**: nuovo `ScalePoint(Label, Fraction)` + lista `ScalePoints` in `SeriesCellViewModel` (Fraction = i/(N−1)); in `HabitScreen.xaml` le label passano da UniformGrid a un **Grid a cella singola** dove ogni label (tick+testo) è centrata sul fermo reale del thumb `8 + (W−16)·Fraction` via nuovo `ScaleLabelOffsetConverter` (thumb=16px → inset 8; auto-centratura indipendente dalla larghezza testo). **Da controllare**: allineamento a ogni step (estremi Very low/Very good e intermedi Low/Good/Fair), che gli estremi non sborbino troppo, e che con scale grandi (>12 step, niente reference points) nulla si rompa. Stesso slider è usato anche in U8 (Sleep quality).
- **U4 — numero non centrato nel cerchio del giorno (heatmap)** · rifinitura minore. Il badge valore ("4") nel cerchio del giorno acceso è scentrato.
- **U5 — toolbar "Modelli predefiniti" si sovrappone** · rifinitura minore. In "Crea nuova abitudine" la toolbar/chevron dei modelli si sovrappone ai riquadri → spostare leggermente a sinistra.

**Design / comportamento (richiedono decisione/brainstorm):**
- **U6 — quadratini mini nella riga Home type-aware + personalizzabili** · design. Oggi i 5–6 quadrati a destra della riga sono uguali per tutti; hanno senso solo in relazione al **tipo** di abitudine e andrebbero **correlati all'abitudine** + personalizzazione su quanti/cosa mostrare.
- **U7 — template "Food diary" orientato alla velocità** · design/feature · **`fixed` 2026-08-08** (Onda 2.4, spec/piano `2026-08-08-wave-2-4-food-diary*`, 7 task). La nota veloce **è** una voce di diario, non un tipo a parte: il seed "Food diary" perde le tre metriche fantasma (un habit Journal non ha superficie di inserimento metriche — le prometteva senza poterle offrire) e dichiara `journal_gesture: QuickNote`; "Daily journal" dichiara `Diary` esplicitamente — migration data-only `FoodDiaryQuickNoteV217`, **schema 2.17**, con test di guardia che percorre tutti i template ufficiali e fallisce su qualsiasi journal con metriche (gemello del guard durata di R7). Nuovo composer nota veloce (`JournalQuickEntryViewModel`/`JournalEntryRowViewModel`: foto tenute in attesa finché la nota non esiste, lista voci del giorno, edit inline, elimina con conferma) ospitato nella colonna ✎ Diario al posto dell'editor Markdown quando il gesto è `QuickNote` — WebView2 non viene istanziato per quell'habit. Il gesto si cambia da Configura → sezione Registrazione. **Le abitudini già istanziate dal vecchio template conservano le tre metriche fantasma**: sono dati utente, nessun backfill. Fix collaterale di un bug preesistente nel builder (il payload salvato come template non era nella forma che `HabitTemplateService` rilegge per `repeatable`/`counts_occurrences`/`mode`/ordine metriche). **Niente verifica visiva in questa onda** — l'autore guarda l'intera Onda 2 una volta sola dopo la 2.5. **Parcheggiato per quel pass** (finora solo nel ledger locale, non nel tracker): il composer lega `TextBox.Tag` a `Strings.Diario_QuickEntry_Placeholder` aspettandosi che renderizzi come placeholder/watermark, ma **nessun meccanismo del genere esiste** in questo repo — lo stile implicito `TextBox` di `BaseStyles.xaml` non ha trigger Tag-driven né esiste un attached property Placeholder/Watermark, quindi la stringa (corretta, localizzata) **non compare mai**; stesso pattern morto già presente in `EditorPane.xaml:62`. L'autore ha deciso il 2026-08-08 di lasciarlo così e guardarlo al pass visivo post-2.5.
- **U8 — Sleep tracker: il tasto "1" confonde + serie opzionale + unità tempo editabile** · design. "Perché per salvare devo cliccare l'uno? Cosa indica?" Ripensare il selettore serie ("Aggiungi serie" + "1"); rendere la **serie opzionale** via un check "ripetibile" in costruzione; permettere di **scegliere/convertire l'unità di tempo** (s→min/h…). Anche qui scala disallineata (vedi U3).
- **U9 — Stop smoking = cronometro d'astinenza** · design/feature **(= B-abstinence già tracciato)** · **`verified-fixed` 2026-07-01** (verifica a schermo OK dall'autore). Tasti **"Inizio"/"Ricaduta"**: Inizio avvia il tempo d'astinenza; Ricaduta chiude il periodo; poi si può ricliccare Inizio per ripartire. **Implementato** in due giri:
  - **U9** (spec+piano `2026-06-28-abstinence-explicit-state*`, 11 task TDD): tabella `abstinence_period` (schema v2.12, chiude **CR8**), `IAbstinenceService`, motore streak da periodi, quick-add Inizio/Ricaduta + cronometro vivo, azione Home contestuale, calendario colorato dai periodi (+ note badge preservati), demo "Niente fumo" riallineato.
  - **U9.1** (spec+piano `2026-07-01-abstinence-clock-and-records*`, 9 task TDD subagent-driven + 4 fix inline dopo verifica visiva): cronometro vivo con secondi (quick-add **e** Dettaglio, `DurationFormat.FormatStopwatch` + `LiveElapsedClock`); lista **"Registrazioni"** a righe-scheda nella Cronologia con **intervalli di tempo** (durata start→ricaduta), data ricaduta ed **elimina con conferma** (`IConfirmationDialog` + `DeletePeriodAsync`); reload di records+quick-add dopo Inizio/Ricaduta/Elimina; heatmap del Dettaglio period-aware (giorni puliti nel colore abitudine, andamento/orario nascosti per l'astinenza); fix leak dei `DispatcherTimer`. Suite finali **Data 370 / WPF 683** verdi.
- **U10 — Morning routine "qui non c'è niente"** · chiarimento/minore. Aprendo l'abitudine action la landing pare vuota; chiarire tipo (è un semplice check fatto/non fatto) e cosa mostrare di default.

## Stato al 2026-06-25 — prima sessione sulla VM dopo Mac: suite verde + font/scrollbar/slider/zen

Prima sessione **sulla VM Windows** dopo il blocco di lavoro da Mac. 7 commit su `fase-4-polish` (`f43b6ee` → `11fb675`).

**Suite test — prima esecuzione completa sulla VM (verde)**: `Recordum.Data.Tests` **356/356** (inclusi i 17 DPAPI/SQLCipher Windows-only che su Mac fallivano per forza) + `Recordum.WPF.Tests` **651/651**. Questo **conferma sulla VM** i CR non-visivi chiusi da Mac compile-only: CR1–CR4 (test sicurezza/restore), CR5/CR7/CR9/CR10/CR11/CR12/CR14/CR16/CR21/CR23/CR24 e i test data layer. Restano da `verified-fixed` solo le verifiche **runtime/visive** (restore corrotto a video, e gli spot-check UI sotto).

**Bug trovato al primo run reale (`fixed`, commit `165d114`)**: `ShadeRampTests` pretendeva hex minuscolo con regex troppo stretto, ma `HsvColor.ToHex` emette maiuscolo canonico (richiesto da `HsvColorTests`); confronti colore sono `OrdinalIgnoreCase` → solo cosmetico. Regex reso case-insensitive.

**Feature/rifiniture nuove (tutte `fixed`, build+suite verdi):**
- **Zen focus** (`1d8f2f7`) — **VERIFIED-FIXED** dall'utente. Il bottone ⤢ ora è un focus a livello di workspace: nasconde sidebar Home + splitter + tab strip, il pannello riempie la finestra (era solo column-focus dentro l'abitudine → l'utente lo riteneva inutile). Nuovo `WorkspaceViewModel.IsFocusZen` che segue lo screen attivo; 5 test. Esc / bottone in-pannello escono.
- **Font UI applicato a tutte le finestre** (`0ada297`) — **VERIFIED-FIXED**. Gli stili impliciti WPF matchano il tipo esatto, **non le sottoclassi**: lo stile implicito `Window` (con `FontFamily=Font.Default`) non raggiungeva `MainWindow`/`SettingsWindow`/dialog (derivati) → l'app rendeva in Segoe, non Palatino, malgrado R8. Aggiunto `FontFamily="{DynamicResource Font.Default}"` sul root dei 6 Window. Chiude il "font non Palatino" segnalato a schermo.
- **Slider a tappe + scala State 1-based** (`c94f222`) — **VERIFIED-FIXED**. La scala State passa da bottoni a uno **slider a tappe** con valore corrente sopra + tick/label per ogni passo (label o numeri). La **prima tappa è selezionata di default** (lo slider non poteva selezionare la posizione 0 → Salva dava "richiesto almeno un valore"). **Codifica ora 1-based**: passo i → valore i+1 (prima tappa = 1, mai 0) su slider, calendario e Cronologia; lettori (`CompletionListViewModel`, `DaySummaryFormatter`) e seeder aggiornati. ⚠️ le registrazioni State **già nel DB** sono 0-based → sfasate di 1 finché non si reimposta lo stato locale.
- **Scrollbar sottili overlay auto-hide** (`11fb675`) — **VERIFIED-FIXED** (Obsidian-style). Lo stile `ScrollBar` si applicava ma `Width` non aveva effetto: il template default dello ScrollViewer riserva una colonna e la ScrollBar ha un **`MinWidth` effettivo ~16px** che ri-clampa `Width`. Fix strutturale: **ScrollViewer ritemplate-ato** (bar in overlay, niente colonna) + `Width=6`/`MinWidth=0` **locale** sulle bar + **auto-hide** (Opacity 0 a riposo, fade-in su hover). Subagent-assisted per il retemplate dello ScrollViewer; causa radice (`MinWidth`) trovata con diagnostica a colori (rosso=bar, verde=thumb).
- **☰ nasconde la striscia di icone (non più l'albero Home)** — **VERIFIED-FIXED** dall'utente. Il ☰ in title bar collassava la **sidebar Home** (effetto collaterale: le colonne abitudine si allargavano → l'utente leggeva "apre le colonne"). Ora il ☰ mostra/nasconde la **striscia di icone da 44px** (`WorkspaceRibbon`, Home/Charts/Backup): nuovo `MainWindowViewModel.RibbonCollapsed` + `ToggleRibbonCommand`, colonna del ribbon `Auto` + `Visibility` legata (→0 quando nascosta), nuova stringa `Workspace.ToggleRibbon` (EN/IT). L'albero Home resta collassabile dall'icona ⌂ del ribbon (invariato). Toggle **istantaneo** (vedi rinvio sotto). _(stato: non committato a fine sessione)_
- **Glifo pin sulla riga abitudine** — **VERIFIED-FIXED**. Icona pin (`Icon.Pin`) accanto al simbolo quando l'abitudine è pinnata (Local/Global), via nuovo `HomeRowViewModel.IsPinned` (`HabitRowTemplate.xaml`). _(stato: non committato a fine sessione)_

**Rinviato esplicitamente (2026-06-25)**:
- **R-ribbonSlide** — slide morbido (~150–200ms) per il toggle della striscia di icone del ☰, al posto dello scatto istantaneo. Caso isolato e sicuro: animare solo la colonna del ribbon 44px↔0 (entrambe assolute, nessuna `Star`, nessuna interazione con le colonne Home/Workspace). L'helper `GridLengthAnimation` già scritto per il primo (fallito) tentativo è preservato in `…/scratchpad/wip-sidebar-anim/Animations/GridLengthAnimation.cs` (riusabile). ⚠️ **Non** riapplicare quel tentativo all'albero Home: lì animava una colonna `Star` accanto allo splitter e **rompeva il fill-then-scroll delle colonne** (regressione "spazio vuoto chiudendo una sezione") → motivo del revert.

**Ancora aperto sulla VM**: sweep visivo accumulato dalle sessioni Mac non ancora ripassato (R-contextMenu, R-comboDropdown, R-tooltip, R-columnSplitter, R-emptyState, R-tabStrip, R-brandMark, R-slider→ora sostituito, R-columnScroll, Color Picker v2 F1–F4, CR5/CR13/CR20/CR22 a video) + restore corrotto runtime (CR1/CR2/CR3/CR4) + **CR8** (regen ModelSnapshot a 2.11).

## Stato al 2026-06-23 — CR1–CR4 chiusi + appearance pass (font Palatino), sessione da macOS

Sessione svolta da Mac (vincoli e checklist di verifica VM: `docs/dev-environment-sync.md` §"Log sessione 2026-06-23"). 3 commit su `fase-4-polish`.

**Sicurezza / perdita-dati — CR1–CR4 `fixed` (commit `24c0e60`)**: restore atomico + rollback (CR1), guard `schema_version` reale letto dal DB con confronto major.minor (CR2), bound sui parametri Argon2id non fidati + cap anti zip-bomb (CR3), shred dei temp in chiaro (CR4). Nuovi helper `SchemaVersionGuard`, `SecureWipe`. Test `SchemaVersionGuardTests` + casi CR3 di `AesGcmBackupCipherTests` verdi (eseguiti via `osx-arm64`). **Restano da `verified-fixed` sulla VM**: suite Data completa + restore-corrotto-a-metà (CR1) + reject schema più recente (CR2). Vedi le righe CR1–CR4 qui sotto.

**Appearance pass — `fixed` (commit `3497021`), verifica visiva VM**:
- **Editor diario** (Toast UI/WebView2): segue il tema app **a caldo** (tripwire `SetResourceReference` su `Brush.Surface`) e ora anche il **font** (secondo tripwire su `Font.Default` + push C#); superficie di scrittura in **serif**. Chiude il sotto-bucket "tema editor" di **B2**.
- **R7. Rinnovamento textbox — `fixed`**: lo stile implicito underline (Obsidian-like) era già su `TextBox`; allineati i due residui — **`PasswordBox`** (Impostazioni/Backup/Restore) e il **preview read-only di `NotesTabView`**. I 4 campi volutamente borderless (titolo/descrizione editor, inline settings) restano per scelta. Audit: 44 TextBox → 39 ereditano l'underline, 5 con override locale (4 voluti + NotesTabView ora sistemato). Disegno di riferimento: `docs/diario-ui-preview.html` §2 ("vale per tutte").
- **R8. Font UI Palatino + switcher — `fixed`**: font classico/serif richiesto dall'utente. `Tokens.xaml` → `Font.System`/`Font.Serif`/`Font.Default`; `Window`/`Page` lo ereditano (DynamicResource); `Consolas` (timer/diagnostica) resta monospace. **Switch utente** in **Impostazioni → Aspetto** via `AppFontService` (mirror di `ThemeService`: persiste `ui.font`, default Serif, evento `FontChanged` → `App.ApplyFont`). Il diario segue via push. Test `AppFontServiceTests` (compile-only su Mac; run su VM).

**AppSetting nuovo**: `ui.font` (runtime, non seedato, come `theme.mode`) — non cambia il conteggio dei seed.

**Anteprime browser** (per giudicare il look lato Mac, non sono UI spedita): `docs/editor-theme-preview.html`, `docs/font-preview.html`, `docs/diario-ui-preview.html`.

### Continuazione 2026-06-23 (da Mac) — chiusi CR5, CR10, CR11; CR8 triato→VM

Sessione da Mac (VM non accessibile). Triage dei CR Data-layer: **i numeri di riga di CR5–CR24 sono stale** (review pre-refactor; es. `HabitTemplateService` ora 263 righe non 1349) — corretti man mano. **Scoperta**: lo schema reale a HEAD è **2.11** (`NoteCategoryV211`, 2026-06-19), non 2.10 — CLAUDE.md §1 va aggiornata (note v2.11: colonne `category`/`completion_id` su `notes`, check `ck_note_category`).

- **CR5** `fixed` (Data verificato osx-arm64 + WPF compila; **resa WPF da verificare a vista VM**).
- **CR10** `fixed` (refactor `ValidateAndReparentAsync`, 19/19 verdi).
- **CR11** `fixed` (ancoraggio a `createdDate` + filtro `End>=fromDate`; 7/7 verdi). Si è rivelato **non solo latente**: tocca `ExportQueryService.BuildSummaryAsync`.
- **CR8** `triaged`→**VM**: il fix richiede rigenerare il ModelSnapshot (`dotnet ef`, non su Mac); impatto runtime zero.

Suite Data: **338 pass**, 17 fail = solo Backup/Security (DPAPI/SQLCipher Windows-only, `PlatformNotSupportedException`, pre-esistenti). Aree toccate tutte verdi. Primo commit `fe6cb9d`.

**Secondo giro — CR6/CR7/CR9** (stessa sessione):
- **CR7** `fixed`: `MaterializeMetricsSegmentsGoalAsync` su singolo context+transazione (da N+3 a 1), rimossi 2 servizi inutilizzati dal costruttore.
- **CR9** `fixed`: goal eredita il nome dell'habit invece del literal "Goal" (Data layer senza `.resx`, ereditare è la via pulita).
- **CR6** `triaged`→**WONTFIX proposto**: interpretazione dubbia (Counter è la riclassificazione voluta per resistance con metrica misurata), nessun dato first-party colpito, non fixabile su Mac né a posteriori.
- `HabitTemplateServiceTests` 9/9 verdi (osx-arm64).

**Terzo giro — CR13/CR20/CR22 (WPF) + fix compile test pre-esistente** (stessa sessione):
- **CR13** `fixed` (compile-only): ramo `Seconds` in `CompletionListViewModel.FormatValue` → durate visibili in Cronologia.
- **CR20** `fixed` (compile-only): `IClock` iniettato in `BackupDialogViewModel` (no più `DateTime.UtcNow`).
- **CR22** `fixed` (compile-only): year-heatmap a 54 settimane (era 53) → il 31 dic non sparisce più nei bisestili col bordo-settimana. L'ExportEngine **non** condivideva il bug (finestra rolling, non anno-solare).
- **Fix collaterale**: il progetto test WPF **non compilava** dalla sessione `3497021` (il costruttore di `SettingsViewModel` aveva guadagnato `IAppFontService` ma 2 file di test non erano stati aggiornati). Creato `FakeAppFontService`, aggiornati i 4 siti → `Recordum.WPF.Tests` ora **compila** (`-p:EnableWindowsTargeting=true`).
- I test WPF sono `net10.0-windows` → **compile-verified su Mac, RED→GREEN + verifica visiva da fare sulla VM**.

**Quarto giro — R-contextMenu: restyle menù tasto-destro** (richiesta utente, compile-only):
- **Problema**: `ContextMenu`/`MenuItem` avevano **solo setter di colore**, nessun `ControlTemplate` → resa col default Aero di Windows (colonna-icone grigia inutile, angoli squadrati, bordo spesso, hover blu di sistema = look datato).
- **Fix** (`Resources/BaseStyles.xaml`): template tematizzati. Pannello flottante arrotondato (raggio 8) su `Brush.SurfaceElevated` + **ombra morbida** (`DropShadowEffect`); **niente colonna-icone**; righe raggio 5 con hover/focus = **riempimento `Brush.Accent` al 14%** su layer separato (testo nitido); submenu con chevron Lucide (accento su hover) nello stesso pannello; separatori 1px (`MenuItem.SeparatorStyleKey`) invece della linea 3D. Tutti i menù app sono `ContextMenu` → ruoli `SubmenuItem`/`SubmenuHeader`, un solo template `Menu.Item` con `Popup` condiviso.
- **Anteprima approvata dall'utente**: `docs/context-menu-preview.html` (light+dark, submenu aperto, confronto col default). Build WPF verde.
- **Da verificare a vista sulla VM**: in particolare la **trasparenza degli angoli del popup** (`HasDropShadow=True` deve mantenere `AllowsTransparency` sul popup interno) e l'apertura submenu.

**Quinto giro — altri ritocchi UI datati** (richiesta utente, compile-only, anteprime approvate):
- **R-slider** (`BaseStyles.xaml`): lo `Slider` della scala State (unico nell'app, `HabitScreen.xaml:333`) non aveva stile → thumb appuntito Aero. Nuovo stile flat: traccia 4px arrotondata (`Brush.Border`), porzione raggiunta in `Brush.Accent`, thumb circolare 16px con anello morbido su hover/drag. RepeatButton/Thumb **keyed** (non impliciti) → non toccano ScrollBar/Expander. Anteprima `docs/slider-preview.html`.
- **R-brandMark** (`MainWindow.xaml`): il simbolo del programma nella title bar era l'icona Lucide `Icon.Activity` (linea-impulso). Sostituita con una **R serif Palatino** bold 18px (agganciata a `Font.Serif` → resta serif anche con UI font System), colore **`Brush.Text`** (scelta utente, non accento). `Icon.Activity` ora non più referenziata. Anteprima `docs/brand-mark-preview.html`.
- **R-comboDropdown** (`BaseStyles.xaml`): il dropdown del ComboBox era arrotondato (raggio 4) ma **senza ombra** → stonava accanto ai nuovi menù. Allineato alla stessa famiglia "popup flottante": raggio 8, padding 4, `DropShadowEffect` morbido (margine trasparente esterno + offset Popup `-6/-4` per restare allineato sotto la casella). `ComboBoxItem` ora con righe arrotondate e **hover accento 14%** su layer separato (era grigio pieno `Brush.Border`) + tinta tenue 8% sul valore selezionato. Anteprima `docs/combobox-preview.html`.

### R-columnScroll — scorrimento orizzontale colonne (regressione + ibrido fill-then-scroll)
- **Stato**: `fixed` (compile-only su Mac, run+verifica VM) · richiesta utente ("lo scorrimento delle colonne è andato perdendosi … fluida e contemporanea")
- **Root cause** (systematic-debugging): il commit **`f08fba9`** ("tab panes fill & split width") ha sostituito il modello originale (colonne a larghezza fissa dentro uno ScrollViewer orizzontale, `8bf7a56`) con **star-split** puro → i pannelli si spartiscono il tab e non sfondano mai, quindi **niente scroll** e schiacciamento con molte colonne aperte. Infrastruttura larghezze nel VM (`GetColumnWidth`, min 320/max 1000, `ui.column_widths`) + thumb resize rimasti orfani da allora.
- **Fix scelto dall'utente: ibrido "riempi-poi-scorri"** (anteprima interattiva `docs/column-scroll-preview.html`). `HabitScreen.xaml`: la griglia colonne è avvolta in uno `ScrollViewer` orizzontale (`x:Name="ColumnsScroll"`, vertical disabled); `Grid.Width="{Binding ViewportWidth, ElementName=ColumnsScroll}"` (riempie il tab → star-split senza buco) e `Grid.MinWidth="{Binding OpenColumnsMinWidth}"` (= colonne aperte × 320 → oltre quella soglia lo ScrollViewer scrolla invece di schiacciare). Nuova proprietà VM `OpenColumnsMinWidth` (notificata in `NotifyShowFlags`). I GridSplitter star restano per il resize fine. Test `OpenColumnsMinWidth_tracks_the_open_applicable_columns` (compile-only). "Fluido" = scroll fisico a pixel + trackpad/Shift-rotella + scrollbar flat tematizzata.
- **Nota**: `RestoreColumnWidthsAsync`/`GetColumnWidth`/`HabitScreen.ColumnResizeThumb`/`OnColumnResizeDelta` restano orfani (lo erano già da `f08fba9`); cleanup separato, non toccato qui.

### R-tooltip — ToolTip arrotondato + ombra (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix** (`BaseStyles.xaml`): il `ToolTip` aveva solo setter di colore → template default Aero (rettangolo squadrato, bordo grigio scuro). Aggiunto un `ControlTemplate` nella stessa famiglia flottante: angoli **raggio 6**, bordo tema 1px, `DropShadowEffect` morbido (opacity 0.16, blur 12, depth 2), `HasDropShadow=True` per la trasparenza degli angoli + margine trasparente esterno per il bleed, `MaxWidth 320`, font Small. Anteprima `docs/tooltip-preview.html`.

### R-columnSplitter — divisori colonne come sash (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix** (`HabitScreen.xaml`): i 5 `GridSplitter` tra le colonne erano barre **piene 4px `Brush.Border`** (grigie, pesanti, sempre visibili). Nuovo stile condiviso `HabitScreen.ColumnSplitter`: area di presa 7px **trasparente** a riposo (resta visibile solo la hairline 1px del bordo colonna), una sottile **barra accento 2px** che compare al 50% su hover e va piena durante il drag, cursore `SizeWE`. Stesso linguaggio del thumb dello slider. Anteprima `docs/column-splitter-preview.html`.

### R-emptyState — stati vuoti con icona + titolo (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix**: gli empty-state erano **una sola riga grigia** al 60% opacità. **`EmptyState.xaml`** (Home) ora è una composizione centrata **icona line + titolo**, con icona e testo per-tab via DataTrigger su `ActiveTab`: Attive → `Icon.Sprout` ("pianta un seme"), Archiviate → `Icon.Moon`, Cestino → `Icon.Sparkles`. **`WorkspaceView.xaml`** welcome allineato allo stesso pattern (`Icon.BookOpen` + testo). Nessuna nuova stringa (riuso `Home_Empty_*` / `Workspace_Empty`). Anteprima `docs/empty-state-preview.html`.
- **CTA aggiunta** (commit `ef9048f`): l'empty-state **Attive** ora ha un pulsante accento **"Crea la prima abitudine"** (bind `OpenNewHabitCommand` del Home VM; nascosto su Archiviate/Cestino). Corretta anche la copy **datata** `Home.Empty.Active` (EN+IT dicevano ancora "habit creation arrives in milestone 3.4", da tempo spedita) → "Ancora nessuna abitudine — pianta la prima." Nuova stringa `Home.Empty.NewHabit` (3 edit: resx EN/IT + Designer).

### R-scrollbar — stato hover + min-size (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix** (`BaseStyles.xaml`): la scrollbar era già flat (Fase C) ma **piatta**: thumb `Brush.Border` faint, nessun hover, nessun min-size (poteva rimpicciolirsi all'infinito su liste lunghe). Aggiunto: `Thumb MinHeight/MinWidth 28` (non collassa); thumb a riposo pillola sottile (`Brush.Border`, margin 3 → ~4px) che su **hover/drag** si **scurisce** (`Brush.TextMuted`) e **allarga** (margin 2 → ~6px). Anteprima `docs/scrollbar-preview.html`.

### R-tabStrip — intestazioni tab workspace (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix** (`WorkspaceView.xaml`): tab con **angoli superiori arrotondati** (`CornerRadius="6,6,0,0"` sul riempimento hover/selezione, padding 12,6) — prima erano riquadri squadrati; l'underline accento 2px sulla tab attiva resta. La **chiusura** ora è un'**icona X Lucide** (Path inline `M0,0 L9,9 M9,0 L0,9`, hover-tint dal chrome Button implicito) invece del glifo grezzo "✕". **Pin/preview invariati** (semantica non toccata). Anteprima `docs/tabstrip-preview.html`.
- **Non fatto** (scelta): muting delle tab inattive — il `Foreground` locale sul titolo vince sui trigger di Style (precedenza WPF), richiederebbe un refactor del title style col rischio di rompere il comportamento preview.

### R-colorPicker — Color Picker v2 (feature, brainstorm→spec→plan→subagent-driven)
- **Stato**: `fixed` (compile-only su Mac; logica testata ma test eseguibili solo VM; contagocce VM-only) · feature richiesta utente
- **Spec**: `docs/superpowers/archive/specs/2026-06-23-color-picker-v2-design.md` · **Piano**: `docs/superpowers/archive/plans/2026-06-23-color-picker-v2.md` · **Mockup approvato**: `docs/color-picker-preview.html`. Eseguito subagent-driven, 10 task, review per-task (controller) + **review finale whole-branch** (verdict: ready to merge, nessun Critical/Important; 2 minor cleanup applicati). Commit `505cdc9`→`e953e89`.
- **Cosa** (tutto in `ColorSwatchPicker`, ereditato da editor/builder/note): **polish** (popup nella famiglia flottante raggio 8+ombra, SV/hue arrotondati, hex con "#" via `HexDigitsConverter`); **F1 anello selezione** (swatch col colore corrente, `ColorEqualsConverter`+`BindingProxy`) + hover ring `TextMuted`; **F2 ramp tonale** (`ShadeRamp.For` puro, riga ricalcolata sul cambio `Color`); **F4 preferiti** (`IFavoriteColorStore` AppSetting `ui.favorite_colors` cap 12 add/remove + 3 DP + "+ aggiungi"/✕ + wiring nei 3 VM); **F3 contagocce** (`IScreenColorPicker`/`ScreenColorPicker` Win32 isolato, bottone nascosto se non bindato).
- **Test (compile-only su Mac, run VM)**: `ShadeRampTests`, `ColorEqualsConverterTests`, `FavoriteColorStoreTests`, `NotesSectionViewModelTests.AddFavorite_…` + `FakeScreenColorPicker`.
- **Nota build**: aggiungere una stringa richiede **3 edit** (`Strings.resx` + `Strings.it.resx` + proprietà manuale in `Strings.Designer.cs`, `Get("Key.Name")`) — niente ResX code-gen in questo progetto.

**Da fare sulla VM**: suite Data completa (incl. i 17 DPAPI/SQLCipher) + **suite WPF completa** (mai eseguita dalla rottura `3497021`); verifica visiva CR5 (cella State-0) + CR13/CR20/CR22 + **R-contextMenu** (menù tasto-destro, angoli popup); CR8 (regen snapshot).

---

## Code review 2026-06-19 — findings CR1–CR24

> **STATO AL 2026-06-24 — backlog chiuso: 22/24 `fixed`.** `wontfix`: **CR6** (reclass resistance — interpretazione dubbia, zero dati first-party). `triaged`→VM: **CR8** (riallineare seed `schema_version` a 2.11 via `dotnet ef`, impatto runtime zero). Nessun `reported` residuo. Tutti i fix WPF sono **compile-verified su Mac**; i test (WPF `net10.0-windows` + Data DPAPI/SQLCipher) **girano sulla VM** — vedi checklist in `docs/dev-environment-sync.md`. Le righe per-CR qui sotto hanno il commit del fix.
>
> Review mirata (multi-agente, recall-biased) su `fase-4-polish` vs `main`, esclusi i file EF generati, per sottosistema (1. Data layer → 2. motore streak → 3. ViewModels WPF/behaviors).

### CR1. Restore non atomico → distruzione del DB se il re-encrypt fallisce
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) — **runtime verify VM** · gravità **alta (perdita dati)**
- **Fix (working tree, non committato)**: `RestoreAsync` costruisce DB+attachment in un'area di **staging** accanto ai target e fa lo **swap atomico** (`CommitSwap`) tenendo copie `.old` da cui fa **rollback** se la sostituzione fallisce a metà; lo snapshot pre-restore resta come rete finale. Build verde. **Da verificare a runtime sulla VM** (swap file + re-encrypt SQLCipher: non eseguibile su macOS).
- **Dove**: `src/Recordum.Data/Backup/RestoreService.cs:93`
- **Cosa**: `RestoreAsync` fa `File.Delete(dbPath)` + cancella gli attachment (93-95) e *poi* chiama `ReEncryptDbAsync` (100). Se il dump plaintext è corrotto/troncato, disco pieno, o crash in mezzo → DB live perso e nuovo assente/parziale. Lo snapshot pre-restore esiste ma **non viene ripristinato in automatico** sul fallimento. Manca swap atomico (scrivi su temp → rename) e rollback dello snapshot.

### CR2. Guard di compatibilità restore inefficace (costante stale + confronto solo major)
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) · gravità **alta**
- **Fix**: la guard legge lo `schema_version` **reale dal DB vivo** via `dbFactory` (`GetCurrentSchemaVersionAsync`, niente più costante "2.7") e confronta major.minor **numericamente** (`SchemaVersionGuard`: "2.10" > "2.7"). Unit test `SchemaVersionGuardTests` verdi (7 casi, eseguiti su osx-arm64).
- **Dove**: `src/Recordum.Data/Backup/RestoreService.cs:21,39,140`
- **Cosa**: `CurrentSchemaVersion = "2.7"` hardcoded mentre lo schema reale è 2.10; `CompareMajor` confronta solo il major. La guard non scatta mai per i 2.x e non rifiuterebbe un backup con schema-minore più recente → EF mapping failure / perdita dati invece del messaggio "Update Recordum to restore it". Leggere lo schema_version reale e confrontare major.minor.

### CR3. Input backup non fidato senza bound → DoS all'apertura
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) · gravità **alta (sicurezza)**
- **Fix**: `AesGcmBackupCipher.DecryptAsync` **limita** i parametri Argon2id non fidati (mem ≤1 GiB, iters ≤24, parallel ≤8) **prima** di derivare, e legge il ciphertext con cap a 2 GiB; `BackupArchiveReader` estrae DB/attachment con cap cumulativo 4 GiB (anti zip-bomb). Unit test CR3 (header forgiato → `invalid_format`) verdi su osx-arm64.
- **Dove**: `src/Recordum.Data/Backup/AesGcmBackupCipher.cs:78,121` (+ `:85`)
- **Cosa**: `DecryptAsync` legge i parametri Argon2id (`memMib` fino a 65535, `iters`, `parallel`) dall'header non fidato → `MemorySize = memMib*1024` ⇒ ~64 GiB allocati aprendo un `.recbak` malevolo (già in `PreviewAsync`). In più ciphertext interamente in RAM + entry ZIP estratte senza tetto (bomba di decompressione). Mancano bound su parametri/dimensioni da fonte esterna. *(OK invece: nonce random, nessun riuso GCM, tag verificato.)*

### CR4. Dump DB in chiaro non distrutto (resta recuperabile)
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) — **runtime verify VM** · gravità **media (sicurezza)**
- **Fix (working tree, non committato)**: il dump in chiaro (`tempPlainDb`) e lo zip decifrato (`tempZip`) sono **shreddati** (`SecureWipe`, overwrite-zeri + unlink, condiviso con `BackupService.ShredDirectory`) invece di `File.Delete`. Build verde. **Da verificare a runtime sulla VM**.
- **Dove**: `src/Recordum.Data/Backup/RestoreService.cs:105`
- **Cosa**: `tempPlainDb` è copia in chiaro dell'intero DB in `%TEMP%`; rimosso con `File.Delete` (solo unlink). `BackupService` usa `ShredDirectory`, il restore no → DB decifrato forense-recuperabile dopo ogni ripristino. Usare lo stesso shred del backup.

### CR5. State con valore 0 sparisce dal calendario (falsy-zero)
- **Stato**: `fixed` (2026-06-23 cont.) — **verifica visiva VM** (parte WPF) · gravità **media (correttezza dati)**
- **Fix**: cross-layer. **Data** (`CompletionService.GetDailyMagnitudeAsync`): rimosso il filtro `if (mag > 0)` — ogni gruppo deriva da una registrazione reale, quindi il giorno è sempre riportato (no-op per Counter/Resistance floor-1 e Action/Journal Count≥1; sblocca State a valore 0). Test `Magnitude_state_includes_zero_valued_day` verde (osx-arm64). **WPF** (`HabitScreenViewModel`): `TintStepFor`/`NumberFor` ricevono ora un flag `hasCompletion` (presenza in `magByDate`) distinto dal valore → un giorno State registrato a 0 prende lo step di tinta minimo e mostra il numero "0"; un giorno con sola nota resta senza tinta. `PushDaySummaryAsync` usa la presenza (`dayMag is not null`) invece di `mag > 0`. **Compila su Mac**; resa da verificare a vista sulla VM (scelta design: cella State-0 = tinta presenza minima + numero "0").
- **Dove**: `src/Recordum.Data/Services/CompletionService.cs` (`GetDailyMagnitudeAsync`, ex riga 222) · `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (`TintStepFor`/`NumberFor`/`PushDaySummaryAsync`)
- **Cosa**: `GetDailyMagnitudeAsync` per `HabitType.State` usa il valore numerico della metrica primaria, poi `if (mag > 0) result.Add(...)`. Con una scala **0-based** (es. NRS dolore, valore più basso = 0) il giorno dà `mag=0` ed è escluso → cella calendario vuota pur con registrazione. Counter/Resistance hanno floor `Math.Max(1,…)`, Action/Journal `Count()≥1`; solo State è vulnerabile. Distinguere "nessuna registrazione" da "valore 0".

### CR6. CounterReclassV210 riclassifica resistance a metriche miste
- **Stato**: `wontfix` (confermato utente 2026-06-23) · gravità **bassa** (declassata da media)
- **Triage 2026-06-23 cont.**: (1) **interpretazione dubbia** — per la grammatica multivalue una resistance con *qualsiasi* metrica misurata (Integer/Decimal/Seconds) è una "serie" → Counter è la riclassificazione **voluta**; "scale-only resistances stay" significa metriche *esclusivamente* scala, e una resistance scala+misurata non è scale-only. Non è chiaro che sia un bug. (2) **Nessun dato first-party colpito**: l'unico template Resistance ("Stop smoking") ha `metrics:[]`; il set teorico (resistance con scala **e** misurata) è solo da vecchio builder utente. (3) **Non fixabile su Mac né a posteriori**: migrazione già applicata (editarla è bloccato/inutile); una forward-migration correttiva richiede `dotnet ef` (VM) e **non può distinguere** un Counter creato nativamente da uno riclassato. → Raccomando **WONTFIX**; eventuale forward-migration solo se emergono dati reali colpiti.
- **Dove**: `src/Recordum.Data/Migrations/20260613210206_CounterReclassV210.cs:19`
- **Cosa**: il subquery matcha **per riga di metrica**: una resistance con sia metrica scala sia misurata entra in `id IN (...)` per la seconda → l'intero habit diventa Counter. Down irreversibile.

### CR7. Materializzazione template = N+3 aperture di connessione (hazard SQLITE_BUSY)
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa (efficienza/robustezza)**
- **Fix**: `MaterializeMetricsSegmentsGoalAsync` riscritto su **singolo `DbContext` + transazione** (pattern `ComposedHabitService`): un unico lookup units/scales, le metriche/segmenti/goal aggiunti direttamente con `Id` pre-assegnati e una sola `SaveChanges`+`Commit`. Da N+3 aperture a 1. Rimossi `IMetricDefinitionService`/`IGoalService` dal costruttore (ora inutilizzati; DI invariato). L'interceptor timestamp/provenance scatta comunque sulla SaveChanges. `HabitTemplateServiceTests` 9/9 verdi (materializzazione metrica-scala/unità-goal/segmenti coperta).
- **Dove**: `src/Recordum.Data/Services/HabitTemplateService.cs` (`MaterializeMetricsSegmentsGoalAsync`; era indicato `:1287`, riga stale)
- **Cosa**: apriva un context per i lookup, poi `metrics.CreateAsync` per ogni metrica (ciascuna apre+salva), poi context per i segmenti, poi `goals.CreateAsync`. Ogni apertura ri-esegue `PRAGMA key` → hazard SQLITE_BUSY (già attutito da `busy_timeout`, ora eliminato alla radice).

### CR8. Seed `schema_version = "2.9"` stale rispetto a HEAD (2.11)
- **Chiuso** (2026-07-01): la migration `AbstinencePeriodV212` ha riallineato il seed a 2.12.
- **Stato**: `triaged` — **rimandato alla VM** (serve `dotnet ef`) · gravità **bassa**
- **Dove**: `src/Recordum.Data/SeedData/AppSettingSeed.cs:31` (era indicato `:925`, riga stale)
- **Triage 2026-06-23 cont.**: il valore `2.9` è in `HasData` → finisce nel **ModelSnapshot** (`RecordumDbContextModelSnapshot.cs:262`). `InitialSchema` semina `2.3` (riga 678); fu bumpato a `2.9` con `NoteHistoryV29` ma `CounterReclassV210`/`NoteCategoryV211` hanno bumpato solo via `UpdateData`, lasciando il costante a `2.9` mentre **HEAD reale è 2.11** (`NoteCategoryV211`, non 2.10 come dice CR/CLAUDE.md). **Impatto runtime = zero**: il guard CR2 legge lo `schema_version` reale dal DB vivo. Allinearlo richiede rigenerare lo snapshot (`dotnet ef migrations add`), non installato su Mac e file sotto guardrail → **da fare sulla VM**.

### CR9. Stringa "Goal" hardcoded (viola regola .resx)
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa (convenzioni)**
- **Fix**: eliminato il literal. `MaterializeMetricsSegmentsGoalAsync` riceve ora il nome risolto dell'habit (`node.Name`, già `payload.Node?.Name ?? tmpl.Name`) e lo usa come nome del goal — il goal appartiene all'habit, quindi ne eredita il nome (default sensato, mai una stringa inglese fissa). Nota: il Data layer **non ha `.resx`** (verificato: nessun resource in `Recordum.Data`/`Recordum.Core`), quindi localizzare lì non era un'opzione pulita; ereditare il nome è la soluzione corretta. Test `InstantiateAsync_goal_inherits_the_habit_name_not_a_hardcoded_default` verde.
- **Dove**: `src/Recordum.Data/Services/HabitTemplateService.cs` (`MaterializeMetricsSegmentsGoalAsync`; era indicato `:1349`, riga stale)
- **Cosa**: `Name = payload.Node?.Name ?? "Goal"`. CLAUDE.md §8: «Stringhe utente in .resx … Niente hardcoded.» Il fallback scattava solo con `Node.Name` assente.

### CR10. `MoveAndReorderAsync` duplica la validazione di `MoveAsync`
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa (manutenibilità)**
- **Fix**: estratto `private static ValidateAndReparentAsync(db, id, newParentId, ct)` che carica il nodo, rifiuta i soft-deleted, valida ciclo + parent-existence e riassegna `ParentId`, ritornando il nodo tracciato così `MoveAndReorderAsync` continua col riordino. Aggiunto test di regressione `Reorder_into_missing_parent_throws_HabitNodeNotFoundException`. `HabitNodeServiceTests` 19/19 verdi.
- **Dove**: `src/Recordum.Data/Services/HabitNodeService.cs` (`MoveAsync`/`MoveAndReorderAsync`/`ValidateAndReparentAsync`; era indicato `:1217`, riga stale)
- **Cosa**: cycle-check + parent-existence copiati verbatim da `MoveAsync`. Un futuro fix va fatto in due punti o divergono. Estrarre helper condiviso `ValidateAndReparentAsync`.

### Sottosistema 2 — motore streak (delta astinenza). Esito: solido, 2 note minori
> Delta `fase-4-polish` (il core schedule×mode×recovery è in `main`, già rivisto a M3.8). Verificato OK: ordinamento ricaduta più recente (`OrderByDescending(CompletedAtMs)`), storia piena in `allTime`, coerenza UTC, `DurationFormat.TryParse` robusto. Nessun bug bloccante.

### CR11. Streak astinenza: `length` del run corrente troncato alla finestra di query
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa** *(non solo latente: tocca l'export)*
- **Fix**: `ComputeAbstinenceAsync` ancora ora il conteggio dei giorni puliti a `createdDate` (la `fromDate` non tronca più la run corrente), poi filtra le run restituite a quelle con `End >= fromDate` — i caller con finestra non vedono run antiche chiuse, ma la run corrente/sovrapposta porta la lunghezza vera. Test `Current_run_length_counts_from_the_real_last_lapse_not_the_query_window` verde; `AbstinenceStreakTests` 7/7.
- **Nota triage**: **non del tutto latente** — `ExportQueryService.BuildSummaryAsync` calcola `current = runs.First(IsCurrent).Length` da `GetHistoryAsync(request.From,…)`, quindi un export Resistance su finestra recente troncava la streak corrente. `StreakSummaryViewModel` invece passa `createdDate` (nessun cambio di comportamento per lui).
- **Dove**: `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs` (`ComputeAbstinenceAsync`; era indicato `:186`, riga stale)
- **Cosa**: se chiamato con `fromDate` dopo l'ultima ricaduta, `length` conta solo dentro [start,toDate]. Calcolare il `length` dall'ultima ricaduta reale.

### CR12. `CurrentLength` numerico stale subito dopo una ricaduta in giornata (cosmetico)
- **Stato**: `fixed` (2026-06-24, commit `27c2ad6`) · gravità **bassa (cosmetico)**
- **Fix**: la selezione della run corrente richiede ora `BreakReason is null` (solo la run aperta è corrente) — una run chiusa da una ricaduta in giornata non alimenta più `CurrentLength`. Test `A_lapse_closed_run_ending_yesterday_does_not_feed_CurrentLength`.
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs` (selezione `current` + `CurrentLength`)
- **Cosa**: ricaduta *oggi* → headline corretto ("appena ora" via elapsed), ma `CurrentLength` resta sul run che finisce ieri (clausola `r.End == today.AddDays(-1)`). Innocuo finché `CurrentLength` non è mostrato per Resistance; verificare il binding del timeline.

### Sottosistema 3 — ViewModels WPF (HabitScreen/Home/drag-drop/builder). 12 findings
> Delta `fase-4-polish` (~3.700 righe, 69 VM + behaviors). Review multi-agente recall-biased; i CONFIRMED sotto sono stati verificati leggendo il codice.

### CR13. Metriche durata invisibili nelle liste registrazioni (CONFIRMED)
- **Stato**: `fixed` (2026-06-23 cont.) — **compile-only su Mac, run+verifica VM** · gravità **media (dato visibile mancante)**
- **Fix**: aggiunto il ramo `Seconds` in `FormatValue` (`if (v.ValueSeconds is { } secs) return DurationFormat.Format(secs);`) + `using Recordum.WPF.Formatting`. Una serie con sola durata ora mostra "1:30". Test `LoadDayAsync_renders_a_duration_field_value` (compila su Mac; **da eseguire sulla VM** — i test WPF sono `net10.0-windows`).
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs` (`FormatValue`)
- **Cosa**: `FormatValue` controlla ScaleLabels/ValueText/ValueDecimal/ValueInteger ma **non `ValueSeconds`** → una serie con sola durata (es. 1:30) appare con riga vuota in Cronologia/Records. Dato salvato ma invisibile.

### CR14. `HabitEditorViewModel.LoadAsync` senza generation guard → stato misto/salvataggio su habit sbagliato (CONFIRMED)
- **Stato**: `fixed` (2026-06-24, commit `90c92c4`) — compile-only su Mac, run VM · gravità **media (integrità)**
- **Fix**: token `_loadGeneration` (mirror di `HabitScreenViewModel`): ogni `LoadAsync` lo bumpa e fa **bail** se superato — prima di assegnare `Node`, prima di caricare le sezioni, prima di finalizzare; il `finally` azzera `_loading` solo se ancora corrente. Test `LoadAsync_deselect_during_an_in_flight_load_does_not_throw_or_clobber`.
- **Dove**: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs:181`
- **Cosa**: `LoadAsync` setta `_loading` ma non ha token di generazione (HabitScreen sì). Con Configura aperta e cambio rapido di habit i load si interlacciano: sezioni miste A/B, `Node` = ultimo writer, edit successivo persiste sul Node corrente potenzialmente errato. Aggiungere generation guard come in `HabitScreenViewModel`.

### CR15. Handler `async void` non protetti → crash su errore transitorio
- **Stato**: `fixed` (2026-06-24, commit `cc85c4d`) — compile-only su Mac · gravità **media (robustezza)**
- **Fix**: gli 8 handler `async void` (4 in `MainWindowViewModel`, 4 in `HabitScreenViewModel`) ora hanno il corpo in `try/catch` che traccia (`Debug.WriteLine`) e ingoia — un errore transitorio reagendo a un evento non diventa più un'eccezione non osservata → `DispatcherUnhandledException` → crash. Niente ILogger nei costruttori (l'app non logga nei VM).
- **Dove**: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` (OnHomePropertyChanged + 3 handler) · `…/HabitScreen/HabitScreenViewModel.cs` (handler bus/calendario)
- **Cosa**: `await` su chiamate DB/launcher senza try/catch in handler `async void`. Errore SQLite transitorio reagendo a un evento → eccezione non osservata → `DispatcherUnhandledException` → crash. Avvolgere in try/catch con logging.

### CR16. Drag-drop: cattura solo `CyclicHabitNodeMoveException` → altre eccezioni crashano
- **Stato**: `fixed` (2026-06-24, commit `d0e1e93`) — compile-only su Mac, run VM · gravità **media (crash su gesto)** — *era follow-up sotto R-dragReorder, promosso a CR*
- **Fix**: le due catch (`ApplyDropAsync` single-row + `TryBulkMoveAsync` per-riga) ora coprono anche `HabitNodeNotFoundException`/`HabitNodeAlreadySoftDeletedException` (nodo/parent rimosso da reload/cleanup concorrente) → drop diventa no-op invece di crashare dall'`async void OnDrop`. Test `ApplyDropAsync_does_not_throw_when_the_dragged_node_vanished`.
- **Dove**: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` (`OnDrop`) → `HomeViewModel.ApplyDropAsync` / `TryBulkMoveAsync`
- **Cosa**: `HabitNodeNotFoundException`/`AlreadySoftDeleted` (parent rimosso da reload/cleanup concorrente) sfuggono dall'`async void` → crash. try/catch completo in `ApplyDropAsync`/`TryBulkMoveAsync`.

### CR17. Zona di drop calcolata sul sotto-elemento, non sulla card riga
- **Stato**: `fixed` (2026-06-24, commit `885942e`) — verifica visiva VM · gravità **bassa (fragilità)**
- **Fix**: `OnDragOver` usa `FindRowCard` (la stessa card del ghost) e calcola la zona Before/Onto/After sull'altezza della card, non su `e.OriginalSource` (spesso un figlio).
- **Dove**: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` (`ResolveZone`/`OnDragOver`)
- **Cosa**: il bordo Before/Onto/After deriva da `e.OriginalSource.ActualHeight` (spesso un figlio: testo/sparkline), non dalla card; il rapporto Y/altezza non mappa il vero terzo della riga → indicatore/SortOrder potenzialmente errati. Il ghost usa già `FindRowCard`: applicarlo anche alla zona.

### CR18. `HomeViewModel`: reload fire-and-forget senza generation guard
- **Stato**: `fixed` (2026-06-24, commit `885942e`) · gravità **bassa**
- **Fix**: token `_reloadGeneration` in `ReloadPreservingSelectionAsync`: un reload superato fa bail prima di riapplicare la selezione (come HabitScreen/Detail).
- **Dove**: `src/Recordum.WPF/ViewModels/HomeViewModel.cs` (`ReloadPreservingSelectionAsync`/`OnRowChanged`)
- **Cosa**: eventi bus sovrapposti possono interlacciare due `Rebuild` sulla stessa `Rows` (duplicati transitori, handler doppi). HabitScreen/Detail hanno il guard; Home no.

### CR19. `HabitRecordsViewModel`: cambio periodo/modalità senza guard
- **Stato**: `fixed` (2026-06-24, commit `885942e`) · gravità **bassa**
- **Fix**: `CancellationTokenSource` per-reload (cancella il precedente; i metodi già passano `ct` alle query) → un toggle rapido Giorno↔Periodo non lascia risultati stantii. VM ora `IDisposable` (disposto da HabitScreenViewModel).
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs`
- **Cosa**: handler `OnModeChanged`/`OnSelectedPeriodChanged` fanno `_ = ReloadAsync()` sovrapponibili; toggle rapido Giorno↔Periodo può lasciare in lista i risultati della query più vecchia.

### CR20. `BackupDialogViewModel` usa `DateTime.UtcNow` diretto (CONFIRMED, convenzioni)
- **Stato**: `fixed` (2026-06-23 cont.) — **compile-only su Mac, run VM** · gravità **bassa (convenzioni)**
- **Fix**: iniettato `IClock clock` nel costruttore; `Browse` usa `clock.UtcNow.UtcDateTime` per il nome file. DI invariato (AddTransient risolve `IClock`). `FakeBackupSaveFileDialog` ora cattura `LastSuggestedName`; test `Browse_derives_the_default_filename_from_the_injected_clock_not_DateTime_UtcNow` (compila su Mac, run VM). Aggiornate le 3 costruzioni esistenti del VM nei test.
- **Dove**: `src/Recordum.WPF/ViewModels/Backup/BackupDialogViewModel.cs`
- **Cosa**: nome file backup da `DateTime.UtcNow`. CLAUDE.md §4: «Mai DateTime.UtcNow/Now diretti. Tutto via IClock/TimeProvider.»

### CR21. QuickAdd: eccezioni grezze in UI + duplicazione
- **Stato**: `fixed` (2026-06-24, commit `04522ea`) · gravità **bassa (convenzioni/UX/reuse)**
- **Fix**: helper `SetUnexpectedError` su `QuickAddViewModelBase` (trace + stringa localizzata `QuickAdd.Error.Unexpected`) usato dalle 3 catch — niente più messaggi EF/SQLite grezzi in UI. `ToggleChrono` + stato chrono duplicati estratti in `SeriesQuickAddViewModelBase` (esteso da Measured/CounterSeries; CounterTap resta sulla base). Test `Save_failure_shows_a_localized_message_not_the_raw_exception`.
- **Dove**: `…/QuickAdd/MeasuredQuickAddViewModel.cs`, `CounterSeriesQuickAddViewModel.cs`, `CounterTapQuickAddViewModel.cs`
- **Cosa**: `ErrorMessage = ex.Message` → stringa non localizzata (es. "UNIQUE constraint failed") mostrata all'utente (viola §8 .resx); usare messaggio localizzato + log. Inoltre `ToggleChrono` e lo skeleton Save sono duplicati tra le 3 VM → estrarre base condivisa.

### CR22. Heatmap anno perde il 31 dic di un bisestile che inizia di domenica (CONFIRMED)
- **Stato**: `fixed` (2026-06-23 cont.) — **compile-only su Mac, run VM** · gravità **bassa (raro, display)**
- **Fix**: `BuildYearHeatmap` ora costruisce **54 settimane** (378 celle) invece di 53/371 — con `leading==6` in un bisestile l'ultima cella è `jan1+371 ≥ 31 dic`. Aggiornati i doc-comment (Cells 378, YearWeeks 54). Test esistente aggiornato a 54/378 + nuovo `YearHeatmap_includes_Dec_31_of_a_leap_year_whose_Jan1_is_on_the_week_edge` (sceglie l'anno bisestile col bordo-settimana per la cultura corrente → deterministico). Compila su Mac, run VM.
- **Nota**: l'**ExportEngine NON condivide il bug** — il suo heatmap è una **finestra rolling di 53 settimane fino a oggi** (`ExportQueryService`: `anchor = mondayOfTodayWeek - 52*7`), non una griglia anno-solare Jan1→Dec31. Il cross-reference del CR era impreciso. Nessun cambio a `HeatmapImageRenderer`.
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs` (`BuildYearHeatmap`)
- **Cosa**: con `leading==6` (Jan 1 sull'ultima colonna della settimana in anno bisestile, es. 2028 cultura domenica-prima / 2012-2040 lunedì-prima) l'ultima cella era `jan1+364 < 31 dic = jan1+365` → il 31 dic non renderizzato.

### CR23. `HabitEditorViewModel.OnColorChanged` inquina la MRU colori a ogni tasto
- **Stato**: `fixed` (2026-06-24, commit `6e760d1`) — compile-only su Mac · gravità **bassa**
- **Fix**: nuovo `DebouncedColorRecorder` (in `Services/`) — registra nel MRU solo il colore **assestato** dopo un breve periodo di quiete, cancellando gli intermedi. `HabitEditorViewModel.OnColorChanged` e `NotesSectionViewModel.OnNoteMarkerColorChanged` ora fanno `Record(value)` invece di `AddAsync` a ogni cambio (il builder aggiungeva già al commit/Launch, invariato). Test `DebouncedColorRecorderTests`.
- **Dove**: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` (`OnColorChanged`)
- **Cosa**: `_recentColors.AddAsync` su ogni colore valido (anche intermedi mentre si digita l'hex), fire-and-forget. MRU riempita di colori transitori, condivisa con builder e picker note. Salvare solo sul commit.

### CR24. `LoadMarkersAsync`: 2×N query sequenziali per mese (perf)
- **Stato**: `fixed` (2026-06-24, commit `68f3c89`) — compile-only su Mac · gravità **bassa (perf)**
- **Fix**: le due query per-mese (magnitudine + conteggi note) di tutti i mesi del range partono **concorrenti** (ognuna apre la propria connessione SQLite; i reader non si bloccano) e si assemblano dopo un singolo `Task.WhenAll`, invece di 2×N await sequenziali rieseguiti a ogni completamento/nota.
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (`LoadMarkersAsync`)
- **Cosa**: per ogni mese del range due `await` DB sequenziali (`GetDailyMagnitudeAsync` + `ListNoteCountsInMonthAsync`), ri-eseguiti a ogni completamento/nota via `Calendar.JumpAsync`. Contro il budget home <500ms. Parallelizzare (`Task.WhenAll`) o query a range unica.

## Stato al 2026-06-16 — lista inserimenti filtrabile per periodo (Feature B)

### B-entriesPeriod. Lista registrazioni commutabile Giorno/Periodo nella colonna Cronologia
- **Stato**: `verified-fixed` (utente 2026-06-16: «vedo le note in cronologia col toggle in base al periodo») — eseguito 2026-06-16, 6 task TDD (executing-plans). Spec `docs/superpowers/archive/specs/2026-06-15-entries-list-period-filter-design.md`, piano `docs/superpowers/archive/plans/2026-06-15-entries-list-period-filter.md`.
- **Cosa**: nella colonna **Cronologia** dello HabitScreen il pannello "Registrazioni" ora è commutabile **Giorno / Periodo** (RadioButton). In modalità Periodo un ComboBox sceglie **Settimana / Mese / Anno / Tutto / Personalizzato**; con Personalizzato compaiono due `DatePicker` Da/A. Default invariato: **Giorno**, guidato dal giorno selezionato sul calendario (nessuna regressione del comportamento esistente).
- **Architettura**: resolver puro `EntriesPeriodRange.Resolve(period, today, customFrom?, customTo?)` in Core (Settimana = settimana ISO lun→dom; Mese/Anno = calendario; Tutto = illimitato; Personalizzato = estremi passati). `CompletionListViewModel.LoadRangeAsync(from?, to?)` riusa l'overload range già esistente su `ICompletionService` (`LoadDayAsync` ora è un wrapper `from==to`). Nuovo `HabitRecordsViewModel` incapsula modalità+filtro+lista, alleggerendo `HabitScreenViewModel` (`Records` rimpiazza `DayCompletions`). UI additiva sulla colonna Cronologia + 2 converter (`EntriesPeriodLabel`, `DateOnlyToDateTime`) + `ObjectDataProvider EntriesPeriodValues` + i18n EN/IT (10 stringhe). **Nessuna migrazione, nessun cambio a `ICompletionService`/schema.**
- **Commit**: `a17a776` (Task 1 Core resolver + 6 test) · `64945bb` (Task 2 LoadRangeAsync) · `29708df` (Task 3 HabitRecordsViewModel + 4 test) · `7d28349` (Task 4 cablaggio, comportamento invariato) · `1059da3` (Task 5 UI+i18n).
- **Test**: Core **61** verdi (+6), WPF **567** verdi (+2 LoadRange, +4 HabitRecords), build 0 errori.
- **Verifica visiva pendente** (tema scuro+chiaro): default Giorno = nessuna regressione; toggle Periodo → Mese corrente; preset Settimana/Anno/Tutto; Personalizzato Da/A (Da>A ⇒ lista vuota, nessun crash); selezione giorno-su-calendario in Periodo NON cambia la lista (QuickAdd/DayNote sì); ritorno a Giorno; refresh post-completamento nella modalità corrente; righe ancora editabili/eliminabili.
- **Resta aperto il ciclo successivo**: **Feature A** (tab Note cronologia pagine+note) — non ancora pianificata.

## Stato al 2026-06-15 — drag-and-drop visivo + riordino manuale

### R-dragReorder. Drag-and-drop visivo con fantasma + riordino manuale persistente
- **Stato**: `fixed` (verifica visiva utente da fare) — eseguito subagent-driven 2026-06-15, 6 task TDD (+ Task 7 verifica). Per ogni task: implementer → spec-review → code-quality-review.
- **Richiesta utente**: «inserisci il drag-n-drop visivo. Voglio vedere la casella dell'abitudine "sollevarsi" quando la trascino». In chiarimento: fantasma agganciato al cursore (stile file-manager) + indicatori di posizione (dentro un gruppo, sopra/sotto un'abitudine) per **riordinare e spostare**. Decisione UX: riordinando in un ordinamento non-Manuale, l'app **passa a Manuale in automatico**.
- **Spec**: `docs/superpowers/archive/specs/2026-06-15-drag-reorder-visual-design.md`; piano `docs/superpowers/archive/plans/2026-06-15-drag-reorder-visual.md`. **Nessuna migrazione**: `HabitNode.SortOrder` esisteva già nel modello/DB ma non era né letto né scritto per i nodi habit, e `HomeSort.Manual` era inerte (`_ => rows`).
- **Eseguito** (commit `7f6251f`→`e0784f3`, + rifiniture review `a23292e`/`a7f1195`/`5676505`):
  1. **Data** `IHabitNodeService.MoveAndReorderAsync(id, newParent, anchorSiblingId, placeAfter)` — riordino **anchor-based** (più robusto dell'indice intero): riassegna `SortOrder` contigui 0..n-1 ai fratelli inserendo il nodo accanto all'ancora (null/non trovata = append in coda); cycle guard riusato da `MoveAsync`; transazionale.
  2. **Sort** `SortOrder` esposto in `HomeTreeRow` (param opzionale in coda, zero blast-radius) + proiettato nella query; `HomeSort.Manual` ora ordina per `SortOrder` (tie-break Name).
  3. **VM** flag riga `IsBeingDragged`/`DropEdge` (enum `RowDropEdge`), `DropPlan` value type, `HomeViewModel.ApplyDropAsync` (bulk reparent vs single `MoveAndReorderAsync`; auto-switch a Manuale solo su reorder fine).
  4. **Behavior** `TreeDragDropBehavior.ResolveDrop` puro (zona+bersaglio→DropPlan, testato); indicatore **singolo** alla volta via `SetIndicator` (highlight gruppo *oppure* edge inserimento); `OnDrop`→`ApplyDropAsync`.
  5. **Ghost** `DragAdorner` (snapshot `VisualBrush` ~0.55 opacità che segue il cursore); spawn/teardown deterministico nel `finally` di `DoDragDrop` (pulisce ghost+flag+indicatore anche su annullamento/Esc).
  6. **XAML** trigger in `Recordum.HabitBlock`: sorgente trascinata sbiadita (opacità 0.4), linea di inserimento accent 2px sopra (Before)/sotto (After).
- **Catch della review olistica finale** (`2eb4d03`): l'auto-switch a Manuale faceva `Rebuild` sul **cache `_lastFetched` stantio** (la nuova `SortOrder` era nel DB, non nello snapshot in memoria) → il riordino si vedeva solo al load successivo. Fix: dopo un riordino a riga singola si **ricarica sempre** (sopprimendo il Rebuild ridondante durante la transizione di sort). Test di regressione asserisce un fetch fresco. *(Le review per-task non l'avevano colto: il seam era tra Task 3 e l'integrazione.)*
- Suite finali: **Data 332 / WPF 555 / Core 55 / Plugins 66 / Export 17 / Chart 18** verdi, build 0/0.
- **Limiti noti (accettati, da spec §6/§7)**: multi-selezione = reparent in blocco senza indice fine (il riordino preciso vale per il drag a riga singola); i pin (Global/Local) restano in cima, `SortOrder` ordina *dentro* il gruppo dei pin.
- **Follow-up (non bloccante, da review Task 4)**: `OnDrop` è `async void` e `ApplyDropAsync` cattura solo `CyclicHabitNodeMoveException`; un'eccezione diversa (DB error…) propagherebbe come unhandled. **Pre-esistente** (anche il vecchio `OnDrop` catturava solo quella) — valutare un try/catch con logging in `ApplyDropAsync` per non far cadere il processo su un gesto utente.
- **Verifica visiva utente (2026-06-15, tema scuro)**: la **meccanica funziona** ("sembra funzionare"). Resta aperto solo il **visual del ghost** (`R-dragGhostVisual`): inizialmente troppo trasparente perché la riga abitudine è translucida per design (~10% tint) e il `VisualBrush` live ne catturava la versione sbiadita da `IsBeingDragged`. Iterato a: snapshot `RenderTargetBitmap` congelato → poi su **card opaca** (`Brush.SurfaceElevated`+bordo, angoli arrotondati). Stato utente: «più o meno ci siamo, ancora da rivedere» → checkpoint `b7fe1d1` (`wip`), **rifinitura rinviata**. Possibile causa residua di "poco fluido": il ghost si riposiziona sugli eventi `DragOver` del drag OLE (più radi dei movimenti reali del mouse) → eventuale fix Win32 (posizione mouse real-time) se servisse. Restano da verificare a vista: indicatori sopra/sotto vs highlight gruppo, persistenza riordino al riavvio, auto-switch a Manuale, tema chiaro.
- **R-dragGhostVisual → `verified-fixed` (2026-06-15, commit `db8af0d` + `92f7675`)**: riscritto il ghost da `Adorner` a **`DragGhost` = Popup topmost** con snapshot `RenderTargetBitmap` della **card intera** (risolve dimensione variabile + sgranatura: prima si fotografava `OriginalSource`, un sotto-elemento), riposizionato a ogni **`QueryContinueDrag`** via `NativeMethods.TryGetCursorPos` (Win32 `GetCursorPos`, `LibraryImport`) → fluido; il Popup galleggia su tutto → mai più clippato ai bordi lista. `DragOver` torna a guidare solo gli indicatori. Eseguito subagent-driven 6 task TDD (interop → `PhysicalToDip` → `DragGhost` → `FindAncestorWithStyle` → cablaggio+rimozione `DragAdorner` → verifica). Per-task review + holistic finale ("ready to ship"). Suite: **WPF 561** (+6) / Data 332 / Core 55 / Plugins 66 / Export 17 / Chart 18, build 0/0 con `-warnaserror`. Spec `docs/superpowers/archive/specs/2026-06-15-drag-ghost-popup-design.md`, piano `…/plans/2026-06-15-drag-ghost-popup.md`. **Verifica utente: «Funziona alla grande»** (fallback `SetWindowPos` previsto ma NON necessario). Limiti noti (accettati): fallback `FindRowCard` per righe in gruppi annidati dipende dallo style `Recordum.HabitBlock` (style-match); se quel resource-key cambia, le righe annidate resterebbero senza ghost (mai crash).

## Stato al 2026-06-14 — crash "database is locked" al Lancia builder

### B-dbLock. Crash `SQLITE_BUSY` su scrittura concorrente (verifica visiva R-multiValue)
- **Stato**: `fixed` (verifica visiva utente da fare)
- **Quando**: 2026-06-14, durante la verifica visiva di R-multiValue. App lanciata via skill `run`, partita ok; **premendo "Lancia" nel costruttore** → crash terminating (`AppDomain.UnhandledException`), `Microsoft.Data.Sqlite.SqliteException: SQLite Error 5: 'database is locked'`.
- **Catena**: `HabitBuilderViewModel.LaunchAsync` → `RecentColorStore.AddAsync` → `AppSettingService.SetAsync` → `SaveChangesAsync`. La habit **era già creata** (`CreateComposedHabitAsync` riuscito); a fallire è la scrittura MRU del colore.
- **Root cause** (systematic-debugging, Phase 1): l'app usa `AddDbContextFactory` → ogni servizio apre la **propria** `SqliteConnection`. I default SQLite (`busy_timeout=0` + journal rollback) fanno fallire **all'istante** un writer che trova il file lockato da un'altra connessione (con ogni probabilità il `BackupSchedulerHost`, InitialDelay 2 min, o un reload concorrente) invece di attendere. Non è un bug del builder né di RecentColorStore: è **fragilità di configurazione connessione** che può colpire qualunque write concorrente.
- **Fix** (TDD, 3 test nuovi):
  1. **Root cause**: `PRAGMA busy_timeout = 5000` su **ogni** apertura connessione, nei due punti dove nasce — `SqlCipherKeyConnectionInterceptor` (chokepoint runtime, combinato con `PRAGMA key` che resta primo statement; path sync+async) e `SqlCipherConnectionFactory.Open` (primitivo per init/migration/backup). Costante `BusyTimeoutMs`. Un writer ora **attende** il lock transitorio invece di morire. Test `SqlCipherBusyTimeoutTests` (factory + interceptor → `PRAGMA busy_timeout > 0`).
  2. **Defense-in-depth**: la scrittura MRU colore in `LaunchAsync` è **best-effort** — `try/catch` swallow: un hiccup transitorio non deve crashare l'app né annullare la habit già creata. Test `Launch_does_not_crash_when_recording_recent_color_fails`.
- **Scelta**: `busy_timeout` (non WAL) — minimale, niente file sidecar `-wal`/`-shm`, niente impatto su backup/restore. WAL resta opzione futura per più concorrenza se servisse.
- Suite: **Data 322 / WPF 535** verdi.

### B-uiThread. Crash cross-thread su `ObservableCollection` legata a CollectionView (verifica visiva R-multiValue)
- **Stato**: `fixed` (verifica visiva utente da fare)
- **Quando**: 2026-06-14, subito dopo il fix B-dbLock. Registrando un completamento → crash terminating, `System.NotSupportedException: ...CollectionView non supporta modifiche al SourceCollection ... da un thread diverso dal thread del dispatcher`. Due stack: `YearHeatmapViewModel.LoadAsync` (via `DetailViewModel.OnCompletionEvent`) e `CalendarNavigatorViewModel.BuildMonthGrid` (via `HabitScreenViewModel.OnCompletionEvent`).
- **Root cause** (systematic-debugging): `EventBus.Publish` invoca gli handler **sincronamente sul thread del publisher**; le registrazioni completion/note girano con `ConfigureAwait(false)` → thread-pool. Gli handler VM sono `async void` e mutano `ObservableCollection` legate alla view **off-UI-thread** → crash. Il `.ConfigureAwait(true)` negli handler non aiutava (nessun SyncContext da catturare sul thread-pool). `EventBus` sta in `Recordum.Plugins` (platform-agnostic, riusato web/MAUI) → il marshaling va fatto nel WPF al punto di subscription.
- **Fix** (TDD): nuovo helper `IEventBus.SubscribeOnUi` (`src/Recordum.WPF/Services/UiEventSubscriptionExtensions.cs`) che marshala l'handler sul dispatcher **catturato al momento della subscription** (`Dispatcher.CurrentDispatcher` — i VM nascono sul thread UI). Adottato da `HabitScreenViewModel` (7 sub), `DetailViewModel` (6), `NotesViewModel` (3) e **`HomeViewModel`** (consolidato: rimosso il marshaling inline duplicato basato su `Application.Current.Dispatcher`). Test `UiEventSubscriptionExtensionsTests` (forwarding + dispose-unsubscribe).
- **Bonus**: catturare il dispatcher alla subscription (invece del global `Application.Current.Dispatcher`) **de-flaka** il test noto `CompletionRecorded_event_reloads_the_list_for_visual_feedback` — la flakiness era proprio il marshaling verso un dispatcher straniero/non-pompante leaked da `IconResourceAlignmentTests` (`new Application()`). Suite **WPF 535** verde 2× di fila.

### B-abstinence. La funzione d'astinenza (Resistance) non è implementata nel motore streak
- **Stato**: `fixed` (verifica visiva utente da fare) — eseguito subagent-driven 2026-06-14, commit `6133c08`→`c6d60d3` (6 task, per-task spec+quality review + review olistica finale: READY TO MERGE)
- **Quando**: 2026-06-14, durante la verifica visiva (utente: "il template stop smoking non funziona"). Indagine: non è il template — **il motore streak non ha logica per Resistance**. `SchedulingAwareStreakAggregator` è completion-positivo (un giorno conta solo se ha un completamento; giorno schedulato senza completamento = miss che spezza), mentre per l'astinenza la semantica è invertita (completamento = ricaduta, streak = tempo dall'ultima ricaduta). L'unica logica resistance-aware è in `CompletionService` (magnitudine = occorrenze; "records abstinence lapses") e nel display (`StreakSummaryViewModel` formatta come tempo trascorso) — ma lo streak sottostante è invertito → privo di senso. Repo con 3 modelli contraddittori (template zero-metriche / demo check-in ~70% / seed-habit ricadute rare).
- **Decisione utente**: fix vero nel motore (non aggirare via template/seed). Modello: streak = giorni puliti dall'ultima ricaduta (da creazione se mai); corrente a **precisione fine** (ore/min via `CompletedAtMs`, "2a 41g 6h"); percorso dedicato che ignora schedule/mode/recovery, onora le pause.
- **Spec**: `docs/superpowers/archive/specs/2026-06-14-abstinence-streak-engine-design.md`; piano `docs/superpowers/archive/plans/2026-06-14-abstinence-streak-engine.md`. Template "Stop smoking" **non si tocca** (lo fixa il motore); demo "Niente fumo" allineato al modello ricadute. Nessun cambio schema.
- **Eseguito**: (1) `StreakBreakReason.Lapse`; (2) ramo `ComputeAbstinenceAsync` nell'aggregator (run di giorni puliti dall'ultima ricaduta, pause congelate, ignora schedule/mode/recovery); (3) `DurationFormat.FormatElapsed` ("2a 41g"/"41g 6h"/"6h 12m"/"12m"/"appena ora"); (4) `StreakSummaryViewModel` Resistance = elapsed fine dall'istante dell'ultima ricaduta + stat "Ricadute (90gg)"; (5) editor nasconde Scheduling/Streak per Resistance (matrice aggiornata); (6) demo seeder "Niente fumo" registra ricadute rare. Suite **Data 328 / WPF 546** verdi.
- **Limitazioni note** (spec §5): il display fine corrente non sottrae i giorni di pausa (i run a giorni sì) → divergenza solo per Resistance con pause; `DurationFormat` resta hardcoded come `FormatDays`. Verifica visiva utente: "Niente fumo" mostra streak pulito ~21g + 3 marker ricaduta; "Stop smoking" (template invariato) ora funziona; editor Resistance senza Scheduling/Streak.

## Stato al 2026-06-11 (notte) — nav-model parte 1: workspace a colonne-toggle eseguito

**Nav-model parte 1 — workspace a colonne** (spec `2026-05-29-navigation-model-design.md` §3–§4/§7, piano `docs/superpowers/archive/plans/2026-06-11-column-workspace.md`, 6 task, commit `0c1304d`→`cae0ad0`): le viste habit (Cronologia/Dettaglio/Note/Configura) sono **colonne affiancate** aperte/chiuse da una **chip-bar** (stile `Recordum.SegmentCheck`, toggle indipendenti) al posto delle 3 tab + pannello editor separato. Stato in `HabitScreenViewModel` (4 flag `Is*Open` + ordine-di-apertura per Esc), persistenza CSV su AppSetting `ui.open_columns` (creata on-write, nessuna migrazione), set mantenuto al cambio habit, default Cronologia. `HabitEditorViewModel` ora di proprietà di `HabitScreenViewModel` (in `MainWindowViewModel` resta un passthrough); **eliminati** da `MainWindow` EditorPane standalone, splitter e larghezza persistita `ui.editor_pane_width`. ✕ per colonna + **Esc chiude l'ultima colonna aperta**; scroll orizzontale quando le colonne sfondano. Suite WPF **458 verdi**. **Stato: `verified-fixed`** (utente 2026-06-11: "sembra andare"). Parte 2 (piano successivo): gruppi-basi ＋Nuova▾ + drag-drop evidente (spec §3.2/§5).

### R-multiValue. Revisione abitudini multivalore (eseguita 2026-06-13)
- **Stato**: `fixed` (verifica visiva utente da fare)
- **Contesto**: backfill di "flessioni" (habit multi-metrica) con `--seed-habit`: il seeder ha popolato **solo la prima metrica**. L'utente fa notare che le metriche hanno semantiche diverse: il "contatore" di una serie indica *se la serie è stata eseguita* (quindi dovrebbe essere un **check**, non un numero), le "ripetizioni" sono una **quantità**, il "tempo" è una **durata** — e quest'ultimo è rimasto vuoto.
- **Eseguito** (spec `2026-06-12-multivalue-habits-revision-design.md`, piano omonimo, 15 task, commit `c891deb`→`bd99622`):
  1. **Grammatica v2** (`HabitCompositionRules`): contatore = occorrenze (mattone +1 a tela vuota), serie come proprietà del Counter, astinenza = mattone dedicato → Resistance; campo Count ritirato; unità quantità opzionale (Integer senza unità, Decimal con).
  2. **Builder**: palette con CheckBox Contatore/Astinenza al posto del toggle Serie; anteprima tap vs righe-serie.
  3. **Quick-add in tre forme** (routing per tipo + shape in `HabitScreenViewModel`): counter nudo = `CounterTapQuickAddViewModel` (+1/⌫, conteggio del giorno); counter con campi = `CounterSeriesQuickAddViewModel` (righe-serie con ✓/○ di validità, reindex senza buchi, "Registra N serie"); Resistance/State = `MeasuredQuickAddViewModel`.
  4. **Cella durata** mm:ss (`DurationFormat` + `SeriesCellViewModel.IsDuration`) che scrive `ValueSeconds`; **crono-cella** ⏱ su `active_timer` (`IActiveTimerService.StopWithoutRecordingAsync` — ferma senza registrare, riversa i secondi nella cella).
  5. **+1 da Home** (`CounterQuickActionHandler`): counter nudo → occorrenza nuda; con campi → apre la schermata.
  6. **Dati**: migration `CounterReclassV210` (resistance-serie → counter, schema_version 2.10); magnitudine = occorrenze per counter/resistance; `ValidatePerType` ammette completion nude per Counter/Resistance (solo State richiede un valore); streak Resistance mostrata in tempo trascorso ("2a 41g").
  7. **Seeder** `--seed-habit` natura-aware: riempie TUTTE le metriche (quantità con trend, durata, scala pesata), 1–4 serie/giorno con ~10% di campi vuoti; counter nudo → N occorrenze nude.
  - Suite: WPF 531, Data 320, Core 55, Plugins 66, Export 17, Chart 18 (flaky noto `CompletionRecorded_event_reloads_the_list` verde isolato).
- **Successivo collegato**: il filone grande **grafici + dati + correlazioni tra abitudini** (anticipa parte della Fase 9, brainstorm dedicato) resta da affrontare.

**Panoramica gerarchica** (appunto utente in verifica; commit `f139ca1`): il rollup della colonna Panoramica ora mostra anche i **sottogruppi come righe** (nome semibold + conteggio figli, niente sparkline/✓) con le loro habit **indentate** sotto (`PanoramicaChildRow.IsGroup/Indent/ChildCount`, converter `IndentToMargin` nuovo, globale); click su un sottogruppo naviga al sottogruppo. Il contatore "Oggi x/y" continua a contare solo le habit. Suite WPF **495**. **Stato: `fixed`, verifica visiva con gli altri item.**

**Seeder demo `--seed-demo`** (richiesta utente; commit `04eefeb`): `DemoDataSeeder` in Bootstrap, flag command-line sull'app — popola via i servizi reali (stessa pipeline SQLCipher/interceptor) una libreria credibile: 3 gruppi (Salute con sottogruppo Sonno, Mente) + 8 habit su tutti e 5 i tipi, colori/icone Lucide varie, ~100 giorni di completamenti con pattern realistici (Allenamento Lun/Mer/Ven con minuti, Acqua bicchieri, Mood scala 1-5 pesata, Diario journal con testi brevi, Niente fumo resistance), goal rolling 150 min/settimana, 2 tag, 3 note generali + 12 day-note sparse. Idempotente (marker AppSetting `dev.demo_seeded`), deterministico (Random(42)), additivo. Riuso futuro: `dotnet run --project src/Recordum.WPF -- --seed-demo`.

**Restyle riga gruppo — cornice BC3** (richiesta utente post-verifica con screenshot; spec `2026-06-12-group-row-restyle-design.md`, piano `2026-06-12-group-row-restyle.md`, commit `3739b2f`→ vedi sotto; scelta visiva via visual companion): **chevron ▸/▾** (swap geometria Lucide, niente più ToggleButton quadrato), nome gruppo in grassetto; **gruppo aperto = cornice arrotondata** nel colore del gruppo (bordo alpha 80 + velo alpha 10 via `HabitTint`) che racchiude i **figli indentati**; gruppi annidati = cornici annidate. Architettura: view ricorsiva su **`Roots`/`Children`** costruite da `Rebuild` in parallelo alla flat `Rows` (che resta la fonte di verità per selezione/shift/drag — eventi per bubbling, behavior intatto); `RowTemplateSelector` ora risolve per chiave via `FindResource` (autoreferenza dei gruppi annidati). Suite WPF **494** (3 test nuovi). **Stato: `fixed`, verifica visiva utente da fare.**

**Selezione multipla stile file-manager (pacchetto C)** (piano `2026-06-12-multi-selection.md`, commit `16e99d0`→`deffb33`): **Ctrl+clic** toggle, **Shift+clic** intervallo dall'ancora, clic semplice = singola; set in `HomeViewModel` (`_selectedIds`+ancora, `SelectedRow` resta l'API workspace e si annulla con 2+ → workspace chiuso); **azioni bulk** via hook `IRowSelectionHost`: i command di riga (menu contestuale + Canc/Ctrl+E/Ctrl+R) deviano su `TryBulkAsync` quando la riga è in multi-selezione (un solo reload, selezione azzerata dopo); **drag multiplo** (`TryBulkMoveAsync` nel behavior, cicli skippati per-riga); right-click fuori selezione collassa a quella riga; **Esc** azzera la multi-selezione; reload/quick-action preservano il set; attiva in tutte e tre le tab. Suite WPF **491** (8 test nuovi; flaky noto verde in seconda run). **Stato: `verified-fixed`** (utente 2026-06-12, insieme ad A e B). **La spec combinata A+B+C è completata.**

**Colonna Panoramica per i gruppi (pacchetto A, nav-model §5)** (piano `2026-06-11-group-panoramica.md`, commit `bcb0906`→`f890084`): `HabitColumn.Panoramica` + **chip dinamiche per tipo nodo** (gruppo → Panoramica·Note·Configura; habit → le 4 solite; visibilità = flag ∧ applicabilità, i flag sopravvivono allo switch habit↔gruppo); `PanoramicaViewModel` (rollup "Oggi x/y", righe habit discendenti ricorsive con `HeatmapStrip` e ✓, card Goal di gruppo, empty-state); **click su riga figlia naviga** alla habit (`ChildSelected` → `HomeViewModel.SelectById`, che riespande gli antenati collassati); default gruppo → Panoramica; `AllColumnsClosed` e default ora applicabilità-aware; colonna ridimensionabile come le altre (`ui.column_widths` guadagna il token `Panoramica`). Suite WPF **483** (9 test nuovi). **Stato: `fixed`, verifica visiva utente da fare.** Con questo il nav-model §5 (vista gruppo) è chiuso nella sua prima forma; resta piano 3 = selezione multipla (§C).

**Rifiniture albero/colonne (pacchetto B)** (spec `2026-06-11-group-view-tree-refinements-design.md` §B, piano `2026-06-11-tree-column-refinements.md`, commit `423ad22`→ vedi sotto): **B.1** scroll verticale dell'albero (prima le righe oltre la viewport erano irraggiungibili); **B.2** collasso gruppi funzionante (il toggle ▾/▸ era inerte — `Rebuild` ora salta i discendenti collassati, stato per-sessione in `_collapsedGroupIds`); **B.3** click-a-vuoto sul ribbon deseleziona; **B.4** chiudere l'ultima colonna aperta collassa il workspace (evento `AllColumnsClosed` → deselezione); **B.5** riapertura sull'**ultima colonna chiusa** (AppSetting `ui.last_closed_column`, fallback Cronologia). Suite WPF **474** (5 test nuovi). **Stato: `fixed`, verifica visiva utente da fare.** Prossimi: piano 2 (Panoramica gruppo §A), piano 3 (selezione multipla §C).

**Gruppi-basi (nav-model parte 2, §3.2)** (spec `2026-06-11-groups-basics-design.md`, piano `2026-06-11-groups-basics.md`, commit `a62e286`→`2b6e3e9`): bottone Home → menu **＋Nuova▾** (Nuova abitudine / Nuovo gruppo); **parent context-aware** per entrambe le voci (gruppo selezionato → dentro; habit → fratello; nulla → radice; `PickAsync(parentId)` finalmente usato); **"Nuovo gruppo" crea subito** (`HomeViewModel.CreateGroupAsync` → evento `GroupCreated` → MainWindowViewModel apre Configura per la rinomina); **drag-drop evidente**: highlight accent del gruppo bersaglio nella zona "dentro" (`HomeRowViewModel.IsDropTarget` + DragOver/DragLeave in `TreeDragDropBehavior` + trigger in `Recordum.HabitBlock`). **Bonus fix**: rebind dell'editor al cambio habit con colonna Configura aperta (gap della parte 1 — `ConfigureRequested` scatta solo sulla transizione chiuso→aperto). Suite WPF **469** (6 test nuovi; flaky noto verde isolato), build 0/0. **Stato: `verified-fixed`** (utente 2026-06-11). Resta del nav-model: §5 vista di dettaglio del gruppo (giro dedicato).

**Colonne ridimensionabili** (chiude nav-model §8 "larghezze"; spec `2026-06-11-column-resize-design.md`, piano `2026-06-11-column-resize.md`, commit `0a5e48e`→`7a79cdd`): richiesta utente durante la verifica visiva del workspace. `Thumb` di 6px sul bordo destro di ogni colonna (cursore ↔, hover/drag accent), drag → larghezza per-colonna in `HabitScreenViewModel` (clamp 320–1000), **doppio-click reset** al default (460/520/460/480), persistenza CSV su AppSetting `ui.column_widths` scritta a fine drag, restore allo startup insieme a `ui.open_columns`, token malformati/fuori range ignorati. Suite WPF **463** (5 test nuovi; 1 flaky noto verde isolato), build 0/0. **Stato: `verified-fixed`** (utente 2026-06-11).

---

## Stato al 2026-06-11 (sera) — rimodernizzazione UI: Fasi A+B+C eseguite

**Fase C — Passata estetica**: piano `docs/superpowers/archive/plans/2026-06-11-aesthetics-pass.md` eseguito (4 task, commit `7251c25`→`a0d2f31`). Stile condiviso **`Recordum.Segment`** (RadioButton→pill senza pallino, checked=elevated+bordo, focus=accent) adottato da Home tabs (Attive/Archiviate/Cestino in pill-group) e calendario (Settimana/Mese/Anno + Mesi/Heatmap); **ScrollBar flat** implicita a tema (thumb arrotondato, niente frecce, page-click ok); **ricerca Home** con icona Lucide + watermark funzionante (il vecchio `Tag` era inerte). Suite 457 verdi, build 0/0. **Stato: `fixed`, verifica visiva utente da fare (A+B+C insieme).**



**Fase B — Guscio (title bar custom + ribbon + status bar)**: piano `docs/superpowers/archive/plans/2026-06-11-shell-ribbon-titlebar.md` eseguito (5 task, commit `fb8ffdc`→`59cc46b`). `WindowChrome` (caption 36px, snap nativi, min/max/close flat, close hover rosso, fix overflow maximize via RootGrid margin); `WorkspaceRibbon` 44px (Home/Grafici con stato attivo accent | menu Backup Crea/Ripristina/Cronologia + Esporta libreria | Aiuto→Diagnostica, ⚙→Impostazioni); **eliminata la barra superiore testuale + Menu**; `Icon.Settings` completato a ingranaggio pieno + 3 icone nuove; TimerStatusBar compattata. Deviazione documentata: icona Backup apre un ContextMenu (restore/cronologia non avevano altra casa dopo la rimozione del menu). Ctrl+1..5 invariati. Suite WPF 456+1 flaky (verde isolato), build 0/0. **Stato: `fixed`, verifica visiva utente da fare.** Prossimo: **Fase C** (Home toolbar pill, scrollbar, hover audit).



**Piano master**: spec `docs/superpowers/archive/specs/2026-06-11-ui-modernization-plan-design.md` (riferimento Obsidian; decisioni: solo guscio ora, ribbon variante B, categorie future disabilitate = deroga approvata al no-placeholder, title bar custom, ordine A→B→C; nav-model resta parcheggiato come fase 4 del piano).

**Fase A — Finestra Impostazioni unificata**: piano `docs/superpowers/archive/plans/2026-06-11-settings-window.md` eseguito (8 task, commit `b6d5231`→`02a0a54`). `SettingsWindow` stile Obsidian (categorie sx: Aspetto/Generale/Backup/Diagnostica + Sync/Plugin/AI disabled "In arrivo"); nuova UI per `daily_reset_time` (`GeneralSettingsViewModel`); launcher `ISettingsWindowLauncher.ShowAsync(categoria?)`; menu ponte "Impostazioni"; **eliminati** SettingsPopup (orfano), BackupSettingsDialog, DiagnosticInfoDialog, IDiagnosticInfoLauncher, `IBackupDialogLauncher.ShowSettingsAsync`. 7 icone Lucide chrome nuove. Suite WPF **457 verdi** (incl. flaky), build 0/0. **Stato: `fixed`, verifica visiva utente in corso.** Prossimo: **Fase B** (title bar custom + ribbon + status bar).

**Picker template (#8 parte 1)** — `fixed` (commit `20916a6`): sezioni "Modelli predefiniti"/"I tuoi modelli" collassabili (Expander, conteggio in header, azioni pinnate in alto, niente persistenza stato). Spec `2026-06-11-template-picker-collapsible-sections-design.md`. **Verifica visiva utente: da fare.**

---

## Stato al 2026-06-11 (continuazione) — calendario chiuso + rifiniture minori esaurite

**Calendario (filone #3) — chiuso.** Commit `3c97ead`:
- **B-calYearRender** `verified-fixed`: celle Anno collassate per mancanza di `HorizontalContentAlignment/VerticalContentAlignment="Stretch"` sui Button (ereditavano Left/Top → ContentPresenter dimensionava a 0×0 il Grid solo-background). Aggiunto Stretch a `YearCellTemplate`+`HeatCellTemplate`.
- **Settimana/Mese**: dimensione cella **fissa** via `CalendarNavigatorViewModel.CellSize` (40 Mese / 56 Settimana) come unica sorgente; header e body derivano la stessa larghezza colonna (7×(CellSize+4)), left-aligned → allineati senza trigger e **senza ingigantirsi** quando la colonna si allarga.
- **Base cella**: aggiunta base grigia (`Brush.Border`) come sfondo del Button anche in Settimana/Mese (come Anno).
- **Splitter workspace**: niente più rimbalzo — `MaxHabitScreenWidth` 800→1100 + `MaxWidth` sulla ColumnDefinition (`MainWindow.xaml`) così lo splitter si ferma al cap invece di clampare al rilascio.

**Rifiniture minori — tutte chiuse:**
- **RB6c-4** `fixed`: anteprima leggibile delle etichette scala nella sezione Scale dell'editor (`ScalesSectionViewModel.ScalePreview` + riquadro in `ScalesSectionView.xaml`).
- **R-scaleUI** `fixed`: cella scala in registrazione ridisegnata — ≤10 step ora **bottoni segmentati** (ListBox stilizzato in `HabitScreen.xaml`, selezione accent/`Brush.OnAccent`) invece del dropdown; slider >10 mostra l'**etichetta** (`SeriesCellViewModel.SelectedScaleLabel`) invece dell'indice grezzo.
- **R-statePlus**, **R1** (theming flat), **R2** (banner WebView2), **R3** (toolbar Toast), **R4** (backup delay configurabile/reattivo): già implementate in sessioni precedenti, tracker allineato qui.

RB6c-4 + R-scaleUI: **verifica visiva utente da fare** (l'utente ha chiesto di fare tutte le limature e verificare dopo). Suite WPF calendario/scala verde.

**Restano (NON limature minori — richiedono design/brainstorm):** B6b template custom, #8 finestra nuova-abitudine a fisarmonica + crea gruppo, #9 gruppi, R-responsiveUI, R5 audit accessibilità app-wide, nav-model a colonne. Bloccanti vaghi: B2 (personalizzazione note, serve repro), B3 (placeholder).

---

## Come segnalare un nuovo bug

Quando ne incontri uno, copia il template qui sotto in una nuova voce `B<N>` (bloccante) o `R<N>` (rifinitura) sopra. Più dettagli = meno round-trip di domande.

```markdown
### B<N>. <Titolo breve, < 60 caratteri>
- **Stato**: `reported`
- **Quando**: 2026-MM-DD
- **Steps**:
  1. <ogni passaggio per riprodurre, dall'avvio dell'app>
  2. ...
- **Atteso**: <cosa dovrebbe succedere>
- **Osservato**: <cosa succede davvero — sintomo visibile>
- **Screenshot**: <path o "incolla in chat"> (opzionale ma molto utile per bug visuali)
- **Ambiente**:
  - Theme: <System | Light | Dark>
  - WebView2 Runtime installato: <yes/no, vedi Help → Diagnostic info>
  - Schema version: <da Diagnostic info>
  - Branch + SHA: <da Diagnostic info>
- **Note**: <qualsiasi info collaterale: prima funzionava? è successo dopo un'azione specifica? log/stack trace allegata?>
```

**Diagnostic info** (Help → Diagnostic info, dialog modale): copia-incolla l'output e hai l'intera sezione "Ambiente" già compilata.

**Log di crash**: se l'app cracha, `%LocalAppData%\Recordum\logs\recordum.log` ha lo stack trace appeso. Incollalo nella sezione "Note".

---

## Stato al 2026-05-29 (giornata estesa)

**Risolti/verificati questa sessione** (dettaglio in fondo + commit):
- **B1** "Save note" → era **theming chrome (R1)** + WebView2 falso negativo, non un bug di Save. Bottoni invisibili (chiaro-su-chiaro) risolti con chrome flat; editor markdown ripristinato.
- **WebView2 falso negativo** → probe spostato sul Dispatcher UI + logging. `verified-fixed` (editor ricco visibile).
- **R1** chrome flat Button/TabItem/ComboBox → `verified-fixed` (utente).
- **B4** sposta-nel-cestino/archivia non aggiornava la lista → `verified-fixed` (utente).
- **B5** "+"/registrazione + refresh Home su ogni completamento → `verified` (utente: "il riquadro si accende").
- **B6a** instanziazione template scartava metriche/scala/goal/segmenti → fixed (test verdi); scala 1-5 ora registra (`verified` utente).
- **B6c** UI sezioni editor (Scheduling/Streak/Metrics/Scales/Timer/Goal/Notifications/Tags/AI/ResetTime) costruita → `verified` funzionale (utente). Spec `docs/superpowers/archive/specs/2026-05-29-editor-sections-ui-design.md`, piano `docs/superpowers/archive/plans/2026-05-29-editor-sections-ui.md`.

**Risolti nel blocco "limatura" (2026-05-29 sera)** — `verified` da fare dall'utente al prossimo giro:
- **Theming Pass 3** (commit `4c6bac5`): CheckBox flat (implicito), toggle "Advanced" a pillola (stile keyed `Recordum.PillToggle`), DatePicker/Calendar tematizzati (template flat per CalendarDayButton/CalendarButton). Chiude **B7** (calendario chiaro-su-chiaro). NB minore: il navigatore mese del Calendar non persiste l'highlight selezione (usa IsPressed; `CalendarButton.IsSelected` non è trigger valido).
- **B9 i18n** (commit `3b42d2f`): 25 stringhe del'editor + picker localizzate (EN+IT). Restano in inglese solo i **nomi display dei template seed** (dati DB, non stringhe — localizzazione separata da decidere).
- **RB6c-1 day-picker** + **RB6c-2 chip Tag selezionate** (commit `50ef22d`): row di toggle giorni per schedule `SpecificDays` (convenzione `["Mon".."Sun"]` da `ScheduleEvaluator`); chip Tag ora mostrano stato selezionato (accent).
- Fix test culture-independent picker (commit `c0c515b`). Suite: WPF 335, Data 296 verdi.

**Ancora aperte**:
- **B6b** template personalizzato — **feature, richiede design** (brainstorming) prima di implementare. NON fatto di proposito.
- **RB6c-4** picker scala: il dropdown scala c'è, ma rifinitura labels/anteprima da fare.
- **R-scaleUI** layout scala State sgradevole (Cronologia).
- **R-statePlus** "+" su State non fa nulla se l'habit è già selezionata.
- **Theming**: `ToggleButton` generico resta Foreground-only (solo l'Advanced ha il pill keyed) — ok per ora.
- **Nav model** spec `2026-05-29-navigation-model-design.md` parcheggiato (colonne toggle) — dopo le basi.

---

## Stato al 2026-06-01

**Brainstorm "Recordum problemi 2" → spec + esecuzione**:
- **Spec** `docs/superpowers/archive/specs/2026-06-01-abitudine-column-calendar-design.md` (commit `2519edb`): 4 pilastri — colonna Abitudine + `CalendarNavigator` riusabile, note unificate (`Note.TargetDate`), timer Session ibrido, modello a colonne (Abitudine rimpiazza Cronologia nei tab attuali; refactor colonne-chip rimandato).
- **B10** (Configura non personalizza) → `fixed` `3f4dce6` (vedi Risolti).
- **Piano 1 — fondamenta dati schema v2.8** (`docs/superpowers/archive/plans/2026-06-01-schema-v28-data-foundation.md`, commit `3555f45`): **eseguito** subagent-driven, 5 task TDD. Schema **v2.8** additivo: `note.target_date` (+indice), `habit_config.note_marker_color` (excluded) + `session_repeat_count` (versioned+mirror), `segment_definition.duration_seconds`; migration `AbitudineCalendarV28`. Query `INoteService` note-per-giorno/mese. Suite **Data 301 / WPF 337** verdi.
- **Piano 2a — `CalendarNavigator` + tab Abitudine** (`docs/superpowers/archive/plans/2026-06-01-calendar-navigator-abitudine-tab.md`, commit `c2b0370`): **eseguito** subagent-driven, 4 task TDD. Widget calendario riusabile (`CalendarNavigatorViewModel` + controllo, marker provider delegate) che rimpiazza la striscia heatmap nella prima tab, ora **Abitudine**; marcatori completato=colore habit (`GetHeatmapAsync`) / nota=`NoteMarkerColor` (`ListNoteDaysInMonthAsync`); giorno selezionato → `QuickAdd.CompletionDate`. Suite **WPF 345 / Data 301** verdi. **Verifica visiva utente: da fare.**
- **Piano 2b — note-del-giorno unificate** (`docs/superpowers/archive/plans/2026-06-01-day-notes-abitudine.md`, commit `633d750`): **eseguito** (Task 1-2 subagent-driven, Task 3 inline per sovraccarico API). `DayNoteViewModel` (editor breve legato al giorno selezionato, scrive/aggiorna `Note.TargetDate`) nella tab Abitudine; `HabitScreenViewModel` si sottoscrive agli eventi nota → `Calendar.JumpAsync` (refresh pallini-nota); badge data nella timeline della colonna Note (`NoteRowViewModel.DateBadge`, "d MMM" o "Generale"). Suite **WPF 351 / Data 301** verdi. **Verifica visiva utente: da fare.**
- **Prossimo**: Piano 3 (timer Session ibrido — motore auto-avanzamento + ripeti ciclo, UI Timer in Configura, + picker colore-nota `NoteMarkerColor` in Configura). Poi i bug minori residui del report.

**Follow-up aperti dalle code-review del Piano 2a** (non bloccanti):
- **R-onAccent**: introdurre token `Brush.OnAccent` (foreground su accento) nei temi Light/Dark e usarlo al posto di `Foreground="White"` hardcoded (cella selezionata calendario + backfill `PillToggle`/`CalendarDayButton` in BaseStyles) — contrasto dark insufficiente. Pass di theming.
- **R-heatmapDead**: dopo lo swap, `HeatmapViewModel` è ancora costruito e ricaricato in `HabitScreenViewModel` ma non più mostrato nella prima tab (la year-heatmap del Dettaglio è un controllo separato). Rimuovere field/load/sottoscrizione + il `DataTemplate` morto (cleanup dedicato, tocca più superficie).
- **R-fakeWebView2-dup**: `FakeWebView2Availability` duplicata in `HabitScreenViewModelFactory` e `MainWindowViewModelFactory` — estrarre in un fixture condiviso.

---

## Stato al 2026-06-02

**Verifica visiva Piano 2a+2b (utente)** — feedback raccolto sulla tab Abitudine:
- ✓ Note del giorno: funzionano (calendario riflette i pallini, editor del giorno ok).
- **A — range anni navigatore troppo stretto**: dropdown solo 2021..2028 (`SelectableYears = Range(today-5, 8)`). I pulsanti ‹ › sono illimitati. → in Piano cell-markers Task 5.
- **B — DatePicker QuickAdd ridondante**: il `<DatePicker>` nei 5 template QuickAdd ha popup ancora chiaro-su-chiaro **ed** è ridondante (il calendario principale già pilota `CompletionDate`). → rimpiazzare con label read-only "Registrando per:", Piano cell-markers Task 9.
- **C — pallino completato si sovrappone al numero**: triage → brainstorming "marcatori cella calendario".

**Brainstorm "marcatori cella calendario" → spec + piano** (companion visivo):
- **Spec** `docs/superpowers/archive/specs/2026-06-02-calendar-cell-markers-design.md` (commit `35926a8`): cella-giorno ridisegnata — **sfondo tinto** type-aware (heat, intensità relativa) = fatto a colpo d'occhio + quanto; **numero conteggio** type-aware (risolve "3 serie = 1 completamento": resistance conta `SeriesIndex` distinti); **angolino nota** colore per-abitudine (`note_marker_color` già in v2.8) + conteggio se >1; **striscia dettaglio** del giorno selezionato. Niente migrazione (solo due query nuove). Nessuna modalità configurabile (una regola automatica per tipo). **Idea "figura geometrica = goal" parcheggiata** (il modello non ha goal per-giorno: `GoalPeriodType ∈ {Fixed,Rolling,Open}`) — §9 della spec. Note correttive: "session" non è un `HabitType` (counter in `timer_mode='session'`, trattato come counter per ora); tint State relativo (assoluto su scaleMax = refinement).
- **Piano** `docs/superpowers/archive/plans/2026-06-02-calendar-cell-markers.md`: 10 task TDD. Data (magnitudine type-aware, conteggio note) → host (calcola TintStep/Number/NoteCount) → CalendarNavigator muto (render) → converter tint → XAML cella → striscia dettaglio → picker colore-nota in Configura. Quick-fix A (Task 5) e B (Task 9) inclusi.
- **Scope shift**: il picker colore-nota (`NoteMarkerColor`) era in Piano 3; si sposta qui (riguarda il marcatore calendario). **Piano 3 resta solo il timer Session ibrido.**
- **ESEGUITO** subagent-driven (sessione 2026-06-02, branch `fase-4-polish`). 10 task + cleanup; per ogni task implementer → spec-review → code-quality-review. Commit `4a7e6a8`..`b22f09a` (14 feat/refactor + cleanup resx morta). Schema invariato (v2.8, due sole query nuove: `ICompletionService.GetDailyMagnitudeAsync` type-aware + `INoteService.ListNoteCountsInMonthAsync`). `NumberFor` riusato da cella calendario **e** striscia riepilogo (coerenti). `HabitColorTintConverter` (multi-binding hex+step→alpha). Nuova section editor `NotesSectionViewModel` (config-backed) per `NoteMarkerColor`. Quick-fix A (anni 15 indietro) + B (DatePicker QuickAdd → label `Abitudine.QuickAdd.RecordingFor`, vecchia chiave `HabitScreen.QuickAdd.Date` rimossa). Suite **Data 307 / WPF 362** verdi, build 0/0. Review finale olistica: **READY TO MERGE**. **Verifica visiva utente: da fare** (skill `run`).
  - **Catch della review**: la formula resistance "somma per-completamento dei SeriesIndex distinti" del piano è **corretta** per dominio (spec §riga 55) — un reviewer l'aveva flaggata per una parafrasi imprecisa del prompt, confermata sulla spec invece di propagare una falsa fix che avrebbe introdotto un bug.
- **Follow-up aperti (polish-debt, non bloccanti, dalla review finale)**:
  - **R-daySummaryUnits**: la striscia riepilogo mostra numeri nudi (es. State "✓ Done · 4"); la spec §5.5 voleva unità/scala ("Umore 4/5", "100 pagine"). Riusare nome metrica/scala primaria. Per State il "4" senza scala è opaco.
  - **R-noteCountContrast**: l'overlay conteggio-note in cella usa `Foreground="#1c1d22"` hardcoded sopra un colore-nota ora scelto dall'utente → rischio contrasto su colori scuri. Tematizzare (lega a `R-onAccent`).
  - **R-summaryGlyphs**: "✓"/"—" nella striscia sono glyph testuali (spec-acknowledged) ma in tensione col brief "no emoji nella chrome". Valutare icone Lucide.
  - `ListNoteDaysInMonthAsync` ora senza chiamanti di produzione (intenzionalmente tenuta in `INoteService` per usi futuri, spec §3.2); `CompletedColorHex` nome lievemente stale (è la sorgente del tint) — cosmetico.
  - **R-tintRamp** (verifica visiva 2026-06-02): l'effetto heat "è un po' meccanico" (utente). La ramp alpha fissa `{0x38,0x66,0x9e,0xd8}` su 4 step rende i salti netti/duri. Da rivedere (curva più morbida / più step / mescolanza con la surface invece di alpha puro). Non bloccante.
  - **Fix visivi post-verifica (commit `696d450`, verified-fixed utente)**: (1) il tint appariva come "pillola" attorno al numero — il `ControlTemplate` flat del Button centra il contenuto di default → `HorizontalContentAlignment/VerticalContentAlignment="Stretch"` sul Button cella; (2) il tint restava grigio default — `CalendarDayCellViewModel.TintColorHex` era `{get;set;}` non-observable → in `MultiBinding` il child-binding teneva il valore stale letto all'init; reso `[ObservableProperty]`. **Lezione**: ogni sorgente di un `MultiBinding` settata dopo l'init del binding DEVE essere observable, altrimenti resta al default.

**Brainstorm "creazione abitudini funzionale" → decomposizione 3 spec + Spec 1 eseguita** (companion visivo):
- **Decisione di fondo**: creazione = **costruttore block-first** (tela vuota → assembli mattoni → tipo dedotto → Lancia atomico, bozza fino al Lancia). I template restano l'on-ramp type-first (istanziano subito). Aggiunta utente: **template personalizzati**. Decomposto in **Spec 1** (builder non-sessione + draft/launch + template custom), **Spec 2** (modo sessione + runtime timer — assorbe il vecchio "Piano 3"), **Spec 3** (builder come "Configura" su abitudini esistenti con dati + unificazione draft↔live).
- **Spec** `docs/superpowers/archive/specs/2026-06-02-modular-habit-builder-design.md` + **Piano** `docs/superpowers/archive/plans/2026-06-02-modular-habit-builder.md` (14 task TDD). **ESEGUITO** subagent-driven (commit `d94ed8f`..`2af4529`). Grammatica pura `HabitCompositionRules` (mattoni→tipo + guardrail) in Core; `IComposedHabitService.CreateComposedHabitAsync` (Data, transazionale node+config+metriche); `HabitBuilderViewModel`/`HabitBuilderView` (composizione grammar-driven + anteprima); picker "Costruisci da zero" → builder (niente più nodo Action orfano); salva-come-template + sezione "I tuoi template" (rinomina/elimina). Suite **Core 59 / Data 309 / WPF 370** verdi, build 0/0. Review finale: **READY** dopo fix i18n. **Verifica visiva utente: da fare**.
  - **Catch della review (falso positivo evitato)**: in Spec 2 cell-markers la formula resistance del piano era corretta — confermata sulla spec invece di propagare la "fix". Stesso atteggiamento qui.
  - **Fix i18n post-review (commit `2af4529`)**: la view builder hardcodava ~20 stringhe italiane (violava English-first) e `DefaultName` italiano finiva nel `PayloadJson` persistito → localizzato tutto via resx (EN+IT+Designer), test reso culture-independent.
- **Follow-up aperti Spec 1 (deferiti, in questo tracker)**:
  - **R-builderFieldPickers** → `fixed` (2026-06-03, su `fase-4-polish`): aggiunti i ComboBox unità/scala nel field row del builder, visibili condizionalmente per `Nature` (Quantity→unità, Scale→scala) via `DataTrigger`, legati a `HabitBuilderViewModel.AvailableUnits`/`AvailableScales` (caricate da `InitializeAsync` via `IReferenceDataQueryService`, chiamata dal launcher prima di `ShowDialog`). Ora l'utente vede il picker e capisce cosa serve per abilitare il Lancia. **Verifica visiva utente: da fare.**
  - **R-builderMode**: `RecordingMode` (cronometro/countdown) è nella grammatica ma **non esposto** nella view e **non persistito** (`BuildConfig`→null). Cablare `RecordingMode`→`HabitConfig.TimerMode` + controllo Mode quando si fa la Spec 2.
  - **R-templateFidelity** → `fixed` parziale (2026-06-03): il writer (`BuildTemplatePayloadJson`) ora scrive `metrics[].unit`/`scale_code` (i **code**, non i GUID) mappando da `AvailableUnits`/`AvailableScales`, così il reader — che già risolve unità/scala per code — round-trippa unità e scala al re-instantiate. **Resta a Spec 2**: `series_count` (nessun target config nel builder non-sessione, la serie è a tempo di completamento) e `mode`→`TimerMode` (vedi R-builderMode).
  - **R-builderSeriesGuard** (già fixato): CheckBox serie `IsEnabled={Binding CanEnableSeries}` + i mattoni invalidanti bloccati da `CanAddField` → composizione invalida non raggiungibile (verificato in review).

---

## Stato al 2026-06-03

**Fix R-builderFieldPickers + R-templateFidelity (commit `8a7efad`)** — vedi sezione 2026-06-02 (follow-up Spec 1 aggiornati). Poi **verifica visiva utente del builder** (app lanciata via skill `run`) → feedback ricco, triagiato qui sotto. Un bloccante (B11) + 5 rifiniture/feature builder.

**Verifica visiva B11 (2026-06-03, 2ª tornata)** — multi-metrica registra (ok), ma emersi:
- **Fix UI fatti** (commit dopo `89ffcaa`): celle metrica **in verticale** `[Nome] [input] [unità]` (prima WrapPanel orizzontale confuso, unità miste); **rimosso box Note ridondante** del QuickAdd Measured (restava duplicato con "Nota del giorno"/`DayNote` sotto → tenuto solo quello).
- **B11-launch** → **`fixed`** (2026-06-03, commit `b7d82dd`): NON era un'unità mancante — **"g" (Gram) È tra le seed**. Era un **bug**: `HabitBuilderViewModel` non si iscriveva al `PropertyChanged` dei field, quindi scegliere unità/scala nel picker non rifaceva `LaunchCommand.NotifyCanExecuteChanged()` → il bottone restava grigio anche dopo aver scelto. Fix: `AddField` si iscrive (RemoveField si disiscrive), `RaiseLaunchState()` su cambio `UnitOfMeasureId`/`ScaleTemplateId`; aggiunto hint `LaunchBlocked` accanto al Lancia che spiega cosa manca. WPF 380 verdi. **Verifica visiva: da fare.**
  - **R-builderCustomUnit/CustomScale** restano aperte ma **declassate**: i seed coprono i casi comuni (km/m/kg/g/ml/l/kcal/steps/hours/minutes/seconds/pages/reps/sets + scala mood-5). La creazione di unità/scale *nuove* (es. scala 1-100) è una feature a sé, da fare se l'utente ne ha bisogno dopo la ri-verifica.
- **B12** → **`fixed`** (2026-06-03, commit `12ebf88`): rivedere/modificare una registrazione passata. Aggiunta sezione **"Registrazioni del giorno"** nella tab Abitudine: `CompletionListViewModel.LoadDayAsync` filtra le completion del giorno selezionato e formatta ogni `CompletionValue` in `FieldDisplays` ("Nome metrica: valore + unità" o etichetta-scala, **non** l'indice 0-based). Nuova lista `DayCompletions` (rimpiazza la lista ultimi-20 che c'era nella tab), ricaricata al cambio giorno e sugli eventi completion, con modifica/elimina per riga (comandi esistenti) + stato vuoto. Nuovo converter `ZeroToVisible`. WPF 382 verdi. **Verifica visiva: da fare.**
  - **Bug nota** ("compare ma non si edita" nella tab Note): l'editor markdown della tab Note non propaga le modifiche (Save resta disabilitato) — è il vecchio **B1** che riemerge. Da investigare a sé (serve riprodurre WebView2/Toast UI change-event). Il path affidabile di modifica resta `DayNoteView` in Abitudine.
- **B13 (needs-investigation)**: un'abitudine costruita "con varie serie" appare come **Measured a riga singola** (un solo Salva, niente "aggiungi serie") invece che Resistance — e i nomi metrica nello screenshot sono strani ("Serie", "Flessioni reps", "Flessioni"). Sospetto: round-trip template che **perde `series_count`** (R-templateFidelity, metà non risolta) → niente serie → tipo risolve a Counter; oppure naming metrica nel builder. Da investigare: il builder/template con serie deve produrre Resistance (con "+ aggiungi serie" già esistente in `ResistanceQuickAddViewModel`).
- **Counter scala-sola non "accende" il giorno** (caso 1): registrando solo la scala su un Counter (Quantità+Scala), il giorno/cella non si accende perché la magnitudine cella è **type-aware sulla metrica primaria** (la quantità). È **Opzione 2** (visualizzazione multi-metrica), rinviata.
- **R-daySummaryUnits** confermato (caso 3: scala "neutral" → cella mostra "2" nudo, indice 0-based senza etichetta).

**Feedback verifica (2026-06-03)**:
- **B11 (bloccante)**: l'abitudine costruita **non riflette la configurazione** nell'abitudine "funzionale" — vedi voce dedicata sotto. È il nodo critico: il builder produce composizioni multi-metrica che lo strato di registrazione (M3.5, mono-metrica per tipo) non renderizza.
- **R-builderColorPicker**: il color picker è solo esadecimale, il quadrante colore **non è cliccabile**. L'utente non sa gli hex. Richiesta: ruota/palette colori con **colori recenti** + hex affiancato. **Da portare anche in Configura** (stesso bisogno). → feature, richiede design.
- **R-builderIconPicker**: campo Icona accetta testo ma **non c'è un picker** per selezionare un'icona. Stesso bisogno in Configura. → feature (icone Lucide per design brief), forse rimandabile.
- **R-builderCustomUnit**: il picker Quantità elenca solo le unità seed, manca l'inserimento di un'**unità personalizzata** inline. → feature.
- **R-builderCustomScale**: idem per la Scala (scala personalizzata inline). → feature.
- **R-builderSaveFeedback**: "salva come template" non dà **alcun feedback** visivo (il template viene creato ma sembra non succedere nulla). Aggiungere un "salvato ✓" accanto al pulsante. → fix UX piccolo.
- **Nota su "non posso lanciare"**: con Count+Quantity+Time il Lancia restava disabilitato perché la Quantità non aveva unità scelta (gating ora **visibile** ma non spiegato). Con Scala+Quantità (unità+scala scelte) il Lancia funziona. Legato a R-builderCustomUnit + chiarezza del gating.

---

## Stato al 2026-06-04

**Blocco "limatura" autonomo** (utente: "applica tutte le rifiniture che ritieni opportune, poi farò un check visivo e d'esperienza"). Eseguito inline con TDD dove tocca logica. **Verifica visiva utente: da fare** su tutto il blocco.

- **B13 → `fixed`** (commit `fix(wpf): B13`): root cause **non** era il round-trip template (il reader legge `habit_type` direttamente → il path template produceva già Resistance). Il bug reale: la CheckBox Series binda `SeriesEnabled` **TwoWay**, bypassando il comando `ToggleSeries` (che faceva `Recompute`). Mancando `OnSeriesEnabledChanged`, `DerivedType` restava **Counter stale** → `BuildRequest` (usa `DerivedType`) lanciava un nodo Counter → instradato a `MeasuredQuickAdd` riga singola. Fix: `partial void OnSeriesEnabledChanged => Recompute()` + rimosso `Recompute` ridondante in `ToggleSeries`. 2 test di regressione end-to-end (path proprietà diretta). I nomi metrica strani ("Serie", "Flessioni reps") erano nomi-campo scelti dall'utente, non un bug. Stessa classe di B11-launch (stato derivato che non si propaga). `series_count` round-trip resta cosmetico/deferito a Spec 2 (ResistanceQuickAdd parte comunque da 1 serie + bottone aggiungi).
- **R-builderSaveFeedback → `fixed`**: badge "Salvato ✓" (`HabitBuilder.TemplateSaved`, EN+IT) accanto a "Salva come template", `TemplateSaved` observable settato dopo il save e azzerato su ogni edit che cambia il payload (nome/colore/icona/campi/serie/modo/unità/scala).
- **R-onAccent → `fixed`**: nuovi token `Brush.OnAccent` (Light=bianco, Dark=`#09090B`) + `Brush.Success` in entrambi i temi. Sostituiti i `Foreground` bianchi hardcoded su sfondo accent: cella calendario selezionata (`CalendarNavigator`), `CalendarDayButton`/`CalendarButton`/pill toggle (`BaseStyles` ×3).
- **R-noteCountContrast → `fixed`**: nuovo `ContrastForegroundConverter` (nero/bianco per luminanza BT.601 del colore-nota) rimpiazza l'hardcoded `#1c1d22` dell'overlay conteggio-note. NB: `Brush.OnAccent` NON era semanticamente giusto qui (lo sfondo è il colore-nota scelto dall'utente, non l'accent) → converter di contrasto dedicato.
- **R-tintRamp → `fixed`** (verifica soggettiva): ramp alpha ammorbidita `{0x26,0x4a,0x78,0xae}` (era `{0x38,0x66,0x9e,0xd8}`) — toe più dolce + ceiling più basso così il giorno pieno è una tinta, non una lastra. Una riga, facile da ritarare a vista.
- **R-daySummaryUnits → deferito** (deliberato): la striscia riepilogo deriva da una singola magnitudine type-aware che perde l'identità della metrica nei casi multi-metrica; il pannello "Registrazioni del giorno" (B12) **già** mostra i valori leggibili con unità/etichette subito sotto; farlo bene richiede una decisione di design sul riepilogo multi-metrica. Non fatto per evitare un mezzo-fix.
- Suite **WPF 385** verdi (+3), build 0/0. Data invariata (nessuna modifica Data).
- **Cleanup ancora aperti** (non visibili, non fatti questo giro): R-heatmapDead, R-fakeWebView2-dup, R-summaryGlyphs.

---

## Stato al 2026-06-05

**Sessione debugging B1 → fix + due bug di layout emersi dal primo uso reale dell'app, + brainstorm feature cronologia note.**

- **B1 → `fixed` + verified-fixed** (commit `64dfa31`): editor markdown — feedback-loop infinito (~93k change events) modificando una nota esistente. Root cause confermata con instrumentation del bridge JS↔.NET (poi rimossa): `setMarkdown()` di Toast UI emette più change sincroni, il one-shot `suppressNext` ne soppeva uno solo, il secondo rientrava dal two-way binding e la normalizzazione markdown impediva la convergenza dei guard → loop. Fix: flag a scope sincrono `applyingRemote` (sopprime tutti i change durante `setMarkdown`) + guard double-init `WebMessageReceived`. Dettaglio nella voce B1 sotto. **Metodo**: systematic-debugging — isolato il fault confrontando DayNote (TextBox semplice, funziona) vs Notes (editor WebView2, rotto), poi instrumentation ai confini dei componenti.
- **B-layout-window → `fixed` + verified** (commit `f265ad9`): la finestra apriva a 1280×760 (>work area 1280×672 su questo display) → nav alto e bottoni/status bar in basso fuori schermo. `FitToScreen()` clampa Width/Height alla work area + rilassa i minimi. Helper puro `ClampToWorkArea` unit-tested (3 casi). Schermo utente: 1280×720, DPI 96.
- **B-layout-scroll → `fixed` + verified** (commit `147b2f8`): la tab **Abitudine** non era scrollabile (a differenza di Dettaglio) → calendario+quickadd+timer+nota-del-giorno+registrazioni eccedevano l'altezza, editor "Nota del giorno" e Salva tagliati sotto. Avvolta in `ScrollViewer`. (Richiesta utente: scroll verticale + più compatto — scroll fatto; compattezza calendario rimandata, cella già 34px.)
- Suite **WPF 388** verdi (+3 layout), build 0/0. Working tree pulito.
- **Feature richiesta in verifica B1**: "la nota resta ancorata al suo giorno + cronologia modifiche con ripristino". Brainstorm completo → spec `docs/superpowers/archive/specs/2026-06-05-note-version-history-design.md` (commit `2a93832`). Schema **v2.9** (`note_history`, mirror `HabitConfigHistory`).

**Note version history + àncora data — ESEGUITO** (piano `docs/superpowers/archive/plans/2026-06-05-note-version-history.md`, 11 task TDD, eseguito inline executing-plans):
- **Schema v2.9** additivo: tabella `note_history` (`id, note_id, body_md, created_at`, FK cascade su `notes.id`, indice `(note_id, created_at)`); migration `NoteHistoryV29` + bump `schema_version` → "2.9". Commit `8ced678` (Core model+DTO), `1853894` (EF config), `f67414b` (migration+seed).
- **Servizio** (`NoteService`): snapshot del body ad ogni Create/Update che cambia `BodyMd` (stessa `SaveChangesAsync`); `ListVersionsAsync` (newest-first) + `RestoreVersionAsync` (ripristino non-distruttivo → passa per `UpdateAsync`, crea nuova versione); delete history esplicita in `DeleteAsync`. Ordinamento `ListByHabitAsync` per **àncora stabile** (`TargetDate ?? data(CreatedAt)`, poi `UpdatedAtMs`) → l'edit non sposta più la nota. Commit `b4a1181`, `cd3e3a4`.
- **WPF**: `NoteRowViewModel.AnchorDateDisplay` + `WasModified`/`ModifiedAtDisplay` (commit `3209f8d`); `NoteHistoryViewModel` + `NoteVersionRowViewModel` (lista versioni + anteprima read-only OneWay + restore, evento `Restored`); `NotesViewModel` show/hide history (`IsShowingHistory`, `ShowHistoryCommand`/`HideHistoryCommand`, child `History`); XAML — context menu "Cronologia" sulla riga nota + pannello cronologia inline che commuta con l'editor, riga "modificata il" condizionale all'àncora.
- Suite **Core 59 / Data 315 / WPF 396** verdi, build 0/0. **Verifica visiva utente: da fare** (skill `run`): note non saltano più in cima dopo edit; àncora data + "modificata il"; click destro → Cronologia → pannello inline; selezione versione → anteprima; Ripristina → contenuto ripristinato + torna all'editor + nuova versione in cima.

---

## Stato al 2026-06-08

**Blocco limature autorizzato** (utente: "facciamo prima le limature poi verifico"). 5 item chiusi, TDD dove tocca logica. Suite **WPF 388** verdi, build 0/0. Data/Core invariati.

- **R-statePlus → `fixed`** (commit `fix(wpf): R-statePlus`): il "+" su un'abitudine State/Resistance/Journal **già selezionata** non faceva nulla. Root cause: l'handler `OnRowQuickActionExecuted` fa solo `SelectedRow = row`; se la riga è già quella selezionata il setter `[ObservableProperty]` è un no-op → nessun `PropertyChanged` → `MainWindowViewModel` non ricarica la workspace. Fix: nuovo evento `HomeViewModel.HabitActivationRequested` alzato quando la riga è già selezionata; `MainWindowViewModel` lo gestisce ricaricando `HabitScreen.LoadAsync`. 2 test (già-selezionata→evento; non-selezionata→cambio selezione, niente evento). **Stessa classe** di B11-launch/B13: stato non propagato perché il "lavoro" è legato a un cambiamento che non avviene. **Verifica visiva: da fare.**
- **R2 → `fixed`** (commit `feat(wpf): R2`): il banner "WebView2 non disponibile" nell'editor ora ha un **link di download** del Runtime (Hyperlink → `developer.microsoft.com/microsoft-edge/webview2/`, aperto con `Process.Start(UseShellExecute)` nel code-behind di `HabitScreen.xaml.cs`). Nuova stringa `Editor.WebView2.NotAvailable.Download` (EN+IT). **Verifica visiva: da fare** (solo su macchine senza WebView2 Runtime).
- **R-heatmapDead → `fixed`** (commit `refactor(wpf): R-heatmapDead`): rimosso `HeatmapViewModel` (classe + test + `DataTemplate`) — era costruito/caricato/sottoscritto in `HabitScreenViewModel` ma **non più renderizzato** da quando `CalendarNavigator` (cell-markers) l'ha sostituito nella prima tab. La year-heatmap del Dettaglio è `YearHeatmapViewModel`, separata, intatta. Ctor `HabitScreenViewModel` invariato (`ramp`/`goals` ancora usati da `Detail`). 3 test che asserivano su `Heatmap.Cells` ri-mirati su `CompletionList.Rows` (stesso reload-on-event). Invisibile.
- **R-fakeWebView2-dup → `fixed`** (commit `test(wpf): R-fakeWebView2-dup`): estratta fixture condivisa `FakeWebView2Availability(bool available = true)` in `tests/.../Fixtures/`; rimosse 6 copie locali (con varianti `MainWindowFakeWebView2Availability`, `FakeWebView2AvailabilityDiag` per il caso `false`). Lasciata `FakeAvailability` in `MarkdownEditorControlTests` (nome diverso, fuori scope dichiarato). Solo test.
- **R4 → `fixed`** (commit `feat(wpf): R4`): `BackupSchedulerHost` aveva interval hardcoded `FromMinutes(60)` e aspettava **un intero intervallo** prima del primo tick → un backup dovuto al lancio attendeva fino a 60 min. Nuovo `BackupSchedulerOptions` (singleton, `InitialDelay`=2min default + `Interval`=30min default); il loop ora fa `Task.Delay(InitialDelay)` → primo `TickAsync` → poi `PeriodicTimer(Interval)`. Così il primo run è reattivo ma si evita ancora il race con lo startup DB. 2 test (primo tick dopo InitialDelay con timing piccolo; idempotenza). Infra, non visibile.

**Limature NON fatte** (richiedono design/brainstorm, non sono limature): R-builderColorPicker, R-builderIconPicker, R-builderCustomUnit/CustomScale, R-daySummaryUnits, R-summaryGlyphs (icone Lucide), B6b template custom, Spec 2 timer Session.

### Verifica visiva 2026-06-08 (utente) — note-history + limature

Lanciata l'app (skill `run`). Feedback + fix in sessione:
- **Note version history + limature → `verified-fixed`** (utente: "sembra funzionare tutto"). Cronologia/ripristino/àncora ok, R-statePlus ok.
- **#1 ordine note (riemerso in verifica) → `fixed`+`verified`** (commit `9f82a59`): l'ordinamento àncora usava `UpdatedAtMs` come tiebreak tra note dello **stesso giorno** → modificarne una la spostava in cima. Tiebreak cambiato a `CreatedAt` (ordine di creazione stabile): l'edit non sposta più la nota. Test `ListByHabitAsync_same_anchor_keeps_creation_order_after_edit`.
- **#3 freeze transitorio tab Note + "ripristino non faceva niente"** — freeze **non riproducibile** (transitorio, poi recuperava; exit 0). Niente root cause certa; applicati due fix mirati a cause plausibili e comunque reali, poi `verified` dall'utente:
  - **`bae4f61`**: l'anteprima cronologia era un **secondo `MarkdownEditorControl`** (con WebView2) sempre istanziato nella tab Note e — in fallback — perfino **editabile** (FallbackBox non read-only, scriveva su un binding OneWay). Sostituita con `TextBox` read-only → un solo editor nella tab.
  - **`54f656e`**: `RestoreVersionAsync` teneva aperti **due context DB sovrapposti** sullo stesso file SQLCipher (possibile `SQLITE_BUSY` in produzione; il `DbFixture` in-memory `:memory:;Cache=Shared` a connessione singola lo mascherava). Ora legge il body in un context e lo dispone **prima** di aprire il context di scrittura.
- Suite **Data 316 / WPF 388** verdi, build 0/0.

### B14. Journal — editor di completamento vs colonna Note (design)
- **Stato**: `reported` (2026-06-08) — **deferito per scelta utente** ("pensiamoci dopo al journal").
- **Sintomo**: in un'abitudine Journal la tab Abitudine mostra sia l'editor journal (QuickAdd) sia il blocco "Nota del giorno"; scrivendo nell'editor e salvando, **niente compare nella colonna Note**.
- **Non è un bug/regressione**: il Journal registra **`Completion`** con corpo markdown (visibili in Abitudine/Dettaglio), la colonna Note mostra entità **`Note`** — due archivi distinti. Serve una **decisione di design** (es. nascondere DayNote sulle abitudini Journal, o unificare journal-entry↔note). Da brainstorming quando si riprende.

---

## Stato al 2026-06-09

**Verifica visiva utente del builder multi-metrica (B11)** → core **verified-fixed**; raccolto un batch di feedback (nuove voci sotto, da triagiare/decidere ordine).

- **B11 → `verified-fixed`**: builder "da zero" → Lancia → registrazione mostra tutte le metriche con la propria unità/scala; salva e compare in "Registrazioni del giorno" con valori leggibili; Resistance con serie ha "Aggiungi serie". Spec 1 builder ora pienamente registrabile.

**Nuovi punti dal giro:**

*Limature UX chiuse (commit `f8d9238`, WPF 392 verdi) — verifica visiva utente: da fare:*
- **R-navCloseColumns → `fixed`**: `MainWindowViewModel.ShowHome` ora azzera `Home.SelectedRow`; deselezionare collassa le colonne workspace+editor (la loro visibilità/larghezza tracciano `SelectedRow`). Cliccare "Home" riporta a una Home pulita senza passare da Archiviate/Cestinate. Test `ShowHome_deselects_the_row_and_closes_the_workspace`.
- **R-seriesRecordingsFlat → `fixed`**: `CompletionListViewModel.BuildFieldDisplays` raggruppa per `SeriesIndex` — Resistance multi-serie legge "Serie 1 — Conteggio: 12 · Tempo: 30" (una riga per serie) invece della lista piatta; i campi dentro una serie sono ordinati per `MetricDefinition.SortOrder` (corregge anche l'ordine sballato visto a video). Mono-serie invariato (niente label "Serie 1"). Test `LoadDayAsync_groups_fields_by_series_for_multi_series_completion`.
- **R-builderTimeUnit → `fixed`**: il mattone **Tempo** ha un picker unità opzionale (hours/minutes/seconds) — non gate sul Lancia (l'unità tempo è facoltativa) ma persistito sul `MetricDefinition` così la registrazione/grafici mostrano "30 min" invece di "30" nudo. `HabitBuilderViewModel.AvailableTimeUnits` (sottoinsieme filtrato per code) + ComboBox condizionale su `Nature==Time`. Test `InitializeAsync_populates_time_units_subset` + `Launch_persists_picked_time_unit_on_the_metric`.

*Feature di design (richiedono brainstorm — prossimo fronte):*
- **R-builderColorPicker + R-builderIconPicker → `fixed`** (2026-06-09): brainstorm → spec `docs/superpowers/archive/specs/2026-06-09-creation-pickers-design.md` + piano `docs/superpowers/archive/plans/2026-06-09-creation-pickers.md` (6 task TDD), **eseguito** su `fase-4-polish`. Due picker condivisi, gradevoli e VM-agnostici, adottati in **builder e Configura**:
  - **Colore**: `ColorSwatchPicker` con popup — palette curata multi-tonalità (36 swatch) + riga **recenti** (`IRecentColorStore`/`RecentColorStore` su `IAppSettingService`, cap 8, dedup, persistiti in `ui.recent_colors`) + entry hex con anteprima. I recenti si registrano al commit reale (Launch nel builder, color-change valido nell'editor). Commit `f17dd44`→`7de996c`.
  - **Icona**: `IconCatalog` (40 chiavi Lucide curate + `Search`) → `Icons.xaml` +40 geometrie (test di allineamento catalogo↔risorse); `IconPicker` con popup — griglia cliccabile + ricerca + converter `IconKeyToGeometry`. Commit `1621a99`, `82b3631`, `171a508`.
  - Suite **WPF 406** verdi, build 0/0. **Verifica visiva utente parziale (2026-06-09)**: builder — color picker (palette/recenti/hex) + icon picker (griglia Lucide + ricerca) **verificati**. **Restano da verificare**: (4) gli stessi picker in Configura, (5) leggibilità popup nei temi chiaro/scuro.
- **R-recordingsLog** (nuovo): manca un **elenco completo delle registrazioni** "a mo' di lista" (non solo "del giorno"), con **filtro per abitudine**. L'utente propende per metterlo nella tab **Abitudine** (o Dettaglio). → feature, richiede design (dove + filtro + scope cross-giorno).

### Blocco limature 2026-06-09 (sera) — eseguito, verifica visiva utente: da fare

Suite **WPF 415** verdi (build 0/0) modulo un flaky pre-esistente (vedi sotto).

- **R-summaryGlyphs → `fixed`** (commit `2e469f7`): i glyph testuali "✓"/"—" della striscia riepilogo del giorno sostituiti da icona Lucide. Done → `Icon.CheckCircle` reso come `Path` (colore `Brush.Success`); not-done → nessuna icona, solo testo muto. `DayNoteViewModel.SummaryIconKey` observable + resx ripulite dai glyph. Sbloccata dal catalogo icone + converter del lavoro picker. Test `SetDaySummary_exposes_a_done_icon_only_when_done`.
- **R-daySummaryUnits → `fixed`** (commit `db741b3`): la striscia ora arricchisce l'aggregato con unità/scala della metrica primaria — Counter "4 km", State mappa il valore alla sua etichetta scala ("Buono") invece dell'indice nudo. Resistance/Action/Journal invariati (semantica di conteggio). Logica pura `DaySummaryFormatter` (8 unit test); l'host risolve la metrica primaria via `_metrics`/`_refData` (solo per Counter/State). **Non è il mezzo-fix temuto**: caso ambiguo multi-metrica gestito dal pannello "Registrazioni del giorno" (B12) sotto la striscia.
- **R3 → `fixed`** (commit `ce155fe`): toolbar editor markdown — aggiunti `task`/`table`/`codeblock`. Source-view già disponibile via mode-switch Markdown/WYSIWYG nativo di Toast UI (non nascosto). `image` **escluso** di proposito (senza `addImageBlobHook` l'upload file inserirebbe blob URL non persistenti).
- **Deselezione click-a-vuoto Home → `fixed`** (commit `667c623`, complemento di `R-navCloseColumns`): cliccare spazio vuoto **dentro la regione albero** (`TreeRegion`) azzera `SelectedRow` → collassa le colonne workspace/editor. Scoped all'albero così click su top-bar/filtri non deselezionano.
- **R5 (accessibility) → `fixed` parziale** (commit `c9442e4`): chiusa la **lacuna mouse-only dei due picker** appena creati — trigger e celle ora focusabili + attivazione Invio/Spazio; all'apertura il focus entra nel popup (prima cella / search box), Escape chiude e riporta il focus al trigger; `AutomationProperties.Name` su trigger e celle. **Resta aperto l'audit a11y app-wide** (focus order su tutte le view, AutomationPeers, contrasto WCAG, nav-tastiera di tutti i controlli custom) — effort separato, candidato a milestone/brainstorm dedicato.
- **Deselezione click-a-vuoto — fix hit-test** (commit `da1afc1`): la prima implementazione non scattava perché la regione albero era un `ContentControl` (il cui template di default ignora il `Background`) → lo spazio vuoto sotto le righe non era hit-testable, il click cadeva sullo sfondo dell'`UserControl` (fuori dalla regione scoped). Avvolta in un `Border` con `Background="Transparent"` → Row 2 hit-testable end-to-end. **Verified utente**: funziona.
- **B-reentrancy LoadAsync → `fixed`** (commit `5e7d0ff`): la deselezione ha esposto un **crash da rientranza** (`NullReferenceException` in `HabitScreenViewModel.LoadAsync` riga 136 `Calendar.CompletedColorHex = Node.Color`). `OnHomePropertyChanged` è `async void` senza cancellazione → selezione+deselezione rapide producono `LoadAsync` sovrapposti; il load superato, ripreso dopo `Task.WhenAll`, dereferenziava il `Node` azzerato dalla deselezione. Fix: **contatore di generazione** che aborta i load superati dopo ogni await + variabile locale `node` (il campo non può più azzerarsi a metà). Test deterministico che intervalla una deselezione gateando un partecipante di `WhenAll`. Latente anche col deselect del pulsante "Home" (R-navCloseColumns).
- **Flaky test noto**: `HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback` fallisce **a intermittenza solo nella suite completa** (415 test, contesa CPU sul reload async event-driven: `HomeCallCount` resta 1 invece di >1) ma **passa sempre isolato** (verificato con e senza le modifiche di sessione). Non causato da queste limature. Da stabilizzare (es. attesa deterministica dell'handler) in un giro dedicato.

---

## Stato al 2026-06-10

**Batch feedback utente (10 punti) → triage + cluster bug eseguito.** Triage: bug (2,4,6) vs feature (1,3,5,7,8,9,10). Utente ha scelto il cluster bug; #2 e #7 si sono evoluti in corso d'opera.

- **#4 colore live → `fixed` + verified** (commit `3015f19`): le modifiche header in Configura (nome/colore/icona) ora pubblicano `HabitUpdated` sul bus; Home ricarica la riga (accento/nome) e il workspace aperto ricolora i marcatori calendario live. `HabitUpdatedPayload` aggiunto (il nome evento esisteva ma non era mai alzato). L'editor guadagna un `IEventBus` opzionale. Test: il cambio colore pubblica l'evento.
- **#6 colore-nota in Configura → `fixed` + verified** (commit `3015f19`): la sezione Note di Configura usa il `ColorSwatchPicker` condiviso (palette + recenti + hex) invece della sola casella hex; recenti condivisi via `IRecentColorStore`.
- **#7 selettore colore visuale → `fixed`** (commit `d74bd62`): nuovo controllo `HsvColorPicker` (quadrato saturazione/luminosità + striscia tonalità trascinabili) dentro il popup di `ColorSwatchPicker` → tutti e tre i picker permettono di scegliere qualsiasi colore visivamente, non solo digitare l'hex (richiesta esplicita utente: "solo l'hex non serve a molto"). Matematica `HsvColor` pura (16 unit test, round-trip RGB↔HSV). **Verifica visiva: da fare.**
- **#2 Tempo non registrabile → NON era un bug dati** (commit `d74bd62`): l'utente ha scoperto che la cella Tempo **finisce fuori vista** in colonna stretta; allargando, compare. Le celle delle serie Resistance erano in `StackPanel` orizzontale che tagliava a destra → cambiato in `WrapPanel` (vanno a capo). **Verifica visiva: da fare.**

**Richiesta trasversale nuova**: *"tutta la UI dovrebbe essere più godibile e proporzionata quando scalata"* → **R-responsiveUI** (nuovo, feature/design): layout responsivo su ridimensionamento finestra/colonne. Il fix wrap della registrazione è il primo tassello; serve un pass più ampio (proporzioni, clipping, scaling) — brainstorm.

**Altri fix di sessione (2026-06-10)**:
- **Recenti color picker → `fixed`** (commit `f1e7667`): la riga "Recenti" non appariva in Configura perché `HasRecentColors` si ricalcolava solo al cambio del *riferimento* della collezione `RecentColors`, ma in Configura la collezione si popola async **dopo** il bind. Ora il controllo si sottoscrive a `CollectionChanged`.
- **#1+#3 icona → `fixed`** (commit `66d350e`): l'icona era persistita (`HabitNode.Icon`) ma mai mostrata. Cablata via `HomeTreeRow` → proiezione query → `HomeRowViewModel`; render `Path` Lucide (tinta col colore abitudine) prima del nome nella riga Home e nell'header del workspace. Collassa se vuota o non risolvibile (icone legacy free-text). **Verifica visiva: da fare.**
- **#10 slide colonna → `verified-fixed`** (commit `3897ddc`, rifatto `e840fb4`→`87c03d4`): la prima versione animava `ColumnDefinition.Width` → ricalcolo di layout del sotto-albero pesante a ogni frame = scatti (peggiori in software rendering/RDP). Rifatto **transform-based**: l'apertura fissa la larghezza una volta (1 layout) e fa scivolare il contenuto con una `TranslateTransform.X` animata (solo composizione, niente layout per frame); la chiusura tiene la `GridLengthAnimation` sulla colonna ormai vuota (economica). Nettamente più fluido; il residuo su questa macchina è software rendering (no GPU, probabile VM/server), non un limite di WPF. Spec `2026-06-10-column-slide-animation-design.md`. **Verifica visiva: OK.**

- **#5 evidenziazione abitudini → `fixed`** (commit `c88a220`→`fd518dd`): identità visiva habit. `HabitTintBrushConverter` (hex + alpha → brush semitrasparente) + stile condiviso `Recordum.HabitBlock`: le righe Home (habit e gruppo) sono blocchi tinti ~10% del colore abitudine, la riga selezionata passa a ~20% + bordo interno colorato via nuovo flag `HomeRowViewModel.IsSelected` (sincronizzato da `HomeViewModel.SelectedRow` e ri-applicato a ogni `Rebuild`). Workspace: striscia 3px in alto + stile tab `Recordum.HabitTab` con sottolineatura/label del tab attivo nel colore abitudine. Spec `docs/superpowers/archive/specs/2026-06-10-habit-visual-identity-design.md`. **Verifica visiva: OK.**
  - **Estensione identità dentro il workspace → `verified-fixed`** (commit `2a3f536`→`25483b2`): override locale di `Brush.Accent` dentro `HabitScreen` = colore abitudine, via `BindingProxy` (porta `Node.Color` nello scope risorse) + `HexToColorConverter`. Tutti i consumer `DynamicResource Brush.Accent` (hover/focus bordi Button/ComboBox, selezione TextBox, today/giorno-selezionato calendario) adottano il colore, live da Configura; Home/Charts/EditorPane restano neutri. Contrasto: giorno selezionato calendario passa da `Brush.OnAccent` fisso a `ContrastForeground(TintColorHex)`. Spec `2026-06-10-habit-screen-color-identity-design.md`. **Verifica visiva: OK.** (Redesign visivo completo del calendario → filone #3.)

**Feature ancora aperte dal batch (richiedono brainstorm)**:
- **#8 finestra "Nuova abitudine"** a fisarmonica (Configura fisso → toggle Preconfezionati → toggle Personalizzati) + bottone "Crea nuovo gruppo".
- **#9 gruppi** (iniziare) — grande, multi-sessione.
- **R-responsiveUI** — pass layout responsivo (il wrap registrazione è il 1° tassello).

---

## Stato al 2026-06-11 — Calendar view modes + redesign Anno (filone #3)

**Contesto**: continuazione del filone #3 (redesign visivo completo del calendario). Piano `docs/superpowers/archive/plans/2026-06-10-calendar-view-modes.md` + spec `2026-06-10-calendar-view-modes-design.md`.

**Fatto e COMMITTATO** (Task 1-4 del piano, versione "celle quadrate piene"):
- Marker provider passato da per-mese a **range di date** (`CalendarMarkers`/`CalendarMarkerProvider(from,to,ct)`); host `LoadMarkersAsync` cicla i mesi del range e **normalizza il tint su tutto il range** (no modifiche Data-layer).
- Enum `CalendarViewMode {Week,Month,Year}` + nav view-aware (`PrevCommand`/`NextCommand`/`StepAsync`), `HeaderLabel`, `SetViewModeCommand`, `_weekAnchor`.
- `CalendarDayCellViewModel.TooltipText`.
- Toggle Settimana/Mese/Anno in XAML.
- Commit: `refactor(wpf): calendar markers by date range...` → `feat(wpf): calendar Week/Month/Year view modes` → `feat(wpf): TooltipText` → `feat(wpf): view-mode toggle + square cells + year heatmap body`.

**Decisioni di design LOCKATE con l'utente** (via preview browser `docs/calendar-preview.html`):
- **Mese**: cella ~40px (più piccola/compatta della prima versione).
- **Settimana**: cella ~56px.
- **Anno**: **12 mini-mesi** (riquadri-mese con dentro i giorni) come default + **sub-toggle [Mesi | Heatmap]** (heatmap = GitHub 7×53). Default Mesi.
- **Conteggio abitudine** dentro la cella: in **cerchio bianco contrast-aware** (segue il foreground del bottone → bianco in dark, scuro in light), **capsula** quando il valore è largo, **abbreviazione ≥1000** (`1.2k`/`13k`/`125k`/`1.2M`/`12M`, valore esatto nel tooltip). Serve a **distinguerlo dal numero del giorno** (piccolo/muted in alto a sx).
- **Triangolo nota** presente anche in Anno (mini-mesi + heatmap).
- **Giorni-settimana dentro i mini-mesi**: RIMANDATO (d'accordo con l'utente).

**Implementato ma WIP / NON funzionante visivamente** (committato come WIP, build 0/0, suite WPF 448 verde):
- `CalendarMonthBlockViewModel` (new), `MonthBlocks` + `YearWeeks` (53×7, istanze condivise con `Cells`), `VisibleCells`, `BuildYearBlocks`/`BuildYearHeatmap`, `[ObservableProperty] YearAsHeatmap` + `SetYearHeatmapCommand`.
- Host `FormatMag` con abbreviazione (`AbbreviateLarge`).
- `InverseBooleanConverter` (per il radio "Mesi" = !YearAsHeatmap).
- XAML: `DayCellTemplate` con ring/capsula contrast-aware, `YearMonthTemplate`+`YearCellTemplate` (mini-mese), `HeatCellTemplate`, sub-toggle, due body Anno (mini-mesi / heatmap).
- Test VM: `CalendarNavigatorViewModelTests` 17 verdi (view modes, heatmap toggle, blocks).

### B-calYearRender. Le celle-giorno in ANNO non si renderizzano (chiuso: `verified-fixed` 2026-06-11)
- **Stato**: `verified-fixed` (2026-06-11, commit `3c97ead`, utente "ci siamo"). **Root cause**: i Button delle celle Anno (`YearCellTemplate`/`HeatCellTemplate`) non impostavano `HorizontalContentAlignment/VerticalContentAlignment="Stretch"`, quindi ereditavano Left/Top dal `Control` e il ContentPresenter del template chrome dimensionava il Grid (solo-Border-background) alla sua desired size 0×0 → celle invisibili (si vedeva solo il `Border Bd` del template = bordo "oggi" arrotondato + il Path nota con desired 6×6). Fix: aggiunto lo Stretch a entrambi i template (come già faceva `DayCellTemplate`). **Follow-up della stessa sessione** (vedi "Stato al 2026-06-11 (continuazione)"): Settimana/Mese a dimensione fissa via `CalendarNavigatorViewModel.CellSize` (no ballooning, header allineato), base grigia delle celle, splitter senza rimbalzo.
- **Stato (storico)**: `triaged` (root cause parziale, fix tentati insufficienti).
- **Sintomo**: in **Anno → Mesi** i mini-mesi mostrano solo il nome del mese; in **Anno → Heatmap** la griglia è vuota. In entrambi i casi si vedono solo il **bordo "oggi"** (come un ovale/anello sottile) e i **triangoli nota** fluttuanti — le celle-giorno (quadratini tint/base) **collassano o non appaiono**. **Mese e Settimana funzionano** (Mese verificato bello dall'utente).
- **Diagnosi**:
  - Mese/Settimana = **singolo** ItemsControl + `MaxWidth` (larghezza definita) e le celle prendono altezza dal **contenuto testuale** (numero giorno) → niente collasso.
  - Anno usa **ItemsControl annidati (2 livelli)** dentro `ScrollViewer`; le celle Anno **non hanno testo** → desired height 0; in pannelli a vincolo infinito (UniformGrid/StackPanel dentro ScrollViewer) la misura collassa.
- **Tentativi FALLITI** (tutti lasciano la griglia invisibile):
  1. Versione iniziale flat `UniformGrid Rows=7 Cols=53` in ScrollViewer orizzontale → vuoto (classico UniformGrid in larghezza infinita).
  2. `Height="{Binding ActualWidth, RelativeSource=Self}}"` (quadrato auto) → **deadlock a 0** (ActualWidth iniziale 0, nessun contenuto a dare altezza).
  3. **Dimensioni FISSE** (mini 14px, heat 15px) **+ base `Border Background=Brush.Border`** → **ancora vuoto** secondo l'utente. ⇒ il problema NON è solo l'altezza: le celle probabilmente non vengono misurate/arrangiate dai pannelli annidati, o un binding annidato fallisce.
- **PROSSIMI PASSI consigliati (ordine)**:
  1. **Attivare il tracing dei binding WPF** (`PresentationTraceSources.TraceLevel=High` sui binding sospetti, o leggere l'Output) per confermare se i binding annidati falliscono: `Command="{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType=ItemsControl, AncestorLevel=2}}"`, `Fill=...NoteColorHex...AncestorLevel=2`, `Visibility={Binding InCurrentMonth,...}`. Un `AncestorLevel` sbagliato non collassa il layout ma è un segnale.
  2. **Dare dimensioni in pixel ESPLICITE ai contenitori annidati**, non lasciare che il ScrollViewer passi misura infinita a UniformGrid/StackPanel: inner ItemsControl del mini-mese `Width=112 Height=96` (o `ItemHeight/ItemWidth` su UniformGrid); inner ItemsControl-colonna della heatmap `Height` esplicita (7×~17).
  3. In alternativa **abbandonare l'annidamento**: pre-costruire nel VM una struttura già renderizzabile — heatmap come 53 colonne ognuna `StackPanel` verticale a `Height` fissa; mini-mese come **singolo** ItemsControl con `UniformGrid` a dimensione esplicita.
  4. Confermare (test lo fanno già lato VM) che `MonthBlocks[].Cells` e `YearWeeks` siano popolati → è **puramente XAML/layout**, non dati.
- **File WIP**: `CalendarNavigatorViewModel.cs`, `CalendarMonthBlockViewModel.cs` (new), `CalendarDayCellViewModel.cs`, `HabitScreenViewModel.cs`, `Views/HabitScreen/Calendar/CalendarNavigator.xaml`, `Converters/InverseBooleanConverter.cs` (new), `App.xaml`, `Resources/Strings(.it).resx`+`Designer.cs`. Preview design (riferimento visivo concordato): `docs/calendar-preview.html`.
- **Da rivedere anche**: dimensione **Settimana** (l'utente in un giro l'ha vista ancora piccola — verificare che `MaxWidth=420` produca ~56px nella colonna reale).

---

## Ex "Aperti — bloccanti" (tutte chiuse o superate: stato nell'indice in testa)

### B11. L'abitudine costruita non riflette la configurazione nell'abitudine funzionale
- **Stato**: `verified-fixed` (2026-06-09) — registrazione multi-metrica confermata sul build (Quantità+Scala registra entrambe con unità; Resistance con serie mostra "Aggiungi serie"; "Registrazioni del giorno" elenca valori leggibili con unità). Vedi "Stato al 2026-06-09".
- **Fix (spec `2026-06-03-multi-metric-recording-design.md`, plan `2026-06-03-multi-metric-recording.md`)**: registrazione generalizzata a **multi-metrica kind-aware**. `SeriesCellViewModel` ora è consapevole del tipo (misura+unità / scala bottoni-o-slider / testo); `MetricCellFactory` risolve unità/scala da `IReferenceDataQueryService`; nuovo `MeasuredQuickAddViewModel` (1 riga di celle su tutte le metriche) **sostituisce** `CounterQuickAddViewModel`+`StateQuickAddViewModel` (host instrada sia Counter sia State qui); `ResistanceQuickAddViewModel` riusa le stesse celle (mostra unità). XAML: un solo `MetricCellTemplate` kind-aware riusato. Encoding scala preservato (indice 0-based). Cella-scala range-aware: bottoni ≤10 passi, slider >10. Suite **WPF 378 / Data 309** verdi, build 0/0. Commit `4a42118`(plan)→`MeasuredQuickAdd`.
- **Resta Opzione 2 (follow-up)**: Dettaglio/grafici/heatmap/cell-marker restano su metrica primaria (visualizzazione multi-metrica non ancora). Creazione scale custom = R-builderCustomScale.
- **Sintomo**: si costruisce un'abitudine col builder (es. Quantità + Scala, oppure Conteggio + serie), si Lancia, l'abitudine appare in Home ma **la registrazione mostra solo una parte** della configurazione: con Quantità+Scala compare solo "quantità" (senza l'unità scelta), la scala sparisce; in una prova precedente compariva solo "Serie".
- **Root cause**: lo strato di **registrazione (QuickAdd, M3.5) è mono-metrica per tipo canonico**. `CounterQuickAddViewModel.LoadMetricsAsync` prende `metrics.OrderBy(SortOrder).FirstOrDefault()` (la prima metrica) e salva un singolo `ValueInteger` senza mostrare l'unità. State mostra una sola scala, ecc. Il **builder (Spec 1) è più espressivo**: produce Counter multi-metrica (Quantità + Scala risolve a Counter con 2 `MetricDefinition`), che la `CounterQuickAddViewModel` scarta oltre la prima. I dati SONO persistiti correttamente (`ComposedHabitService` aggiunge tutte le metriche) — è la UI di registrazione a non renderizzarle.
- **Tensione di design**: i guardrail della grammatica (`HabitCompositionRules`) impediscono combinazioni *illegali* ma permettono combinazioni *semanticamente fuse* (Quantità+Scala→Counter) che nessuna QuickAdd canonica sa mostrare. Inoltre nessuna QuickAdd mostra l'**unità** della metrica.
- **Opzioni (da decidere in brainstorming)**:
  - (i) Rendere le QuickAdd **multi-metrica** (Counter mostra N righe input, una per metrica, con la propria unità; ecc.) — modifica significativa allo strato M3.5.
  - (ii) **Restringere la grammatica** del builder così da produrre solo composizioni che una QuickAdd canonica sa rendere (es. un solo campo misurato + serie opzionale) — più piccolo, ma limita l'espressività del builder (contraddice la visione "componi liberamente").
  - (iii) Ibrido: la QuickAdd del Counter mostra l'unità della metrica primaria (fix minimo per il caso 1-metrica) + guardrail che impediscono i mix problematici.
- **Priorità**: alta — un builder che genera abitudini non pienamente registrabili è peggio di nessun builder. Bloccante per dichiarare Spec 1 "verified".

### B1. Save note non funzionante
- **Stato**: `fixed` (2026-06-05, commit `64dfa31`) — **verifica visiva utente: ✓** (editing nota esistente ora funziona, niente più lampeggio).
- **Root cause (confermata via instrumentation del bridge)**: feedback-loop infinito (~93k change events) modificando una nota **esistente**. Toast UI `setMarkdown()` emette **più** change events sincroni; il flag one-shot `suppressNext` ne sopprimeva solo uno, il secondo rientrava dal two-way binding, e poiché `getMarkdown()` normalizza il markdown i guard di idempotenza non convergevano mai → loop ("lampeggia", CPU al massimo, Save inutilizzabile). NB: NON era un problema di Save/IsDirty (`canSave=True` per tutto il loop), né l'ipotesi originale (a)/(c). Le note **nuove** (testo plain) non innescavano il loop perché round-trippano senza normalizzazione.
- **Fix**: sopprimere TUTTI i change emessi durante `setMarkdown()` con un flag a scope sincrono (`applyingRemote`) invece del one-shot. + guard su `TryInitAsync` (era doppio-sottoscritto `WebMessageReceived`: Loaded + OnAvailabilityChanged in race prima dell'init → ogni messaggio gestito due volte). Strumentazione temporanea rimossa post-conferma.
- **Storico ipotesi (per memoria)**: il sintomo originale 2026-05-29 era conflato con R1 theming + falso negativo WebView2; il vero bug del save era latente e si manifestava solo sull'editing di note con markdown reale.

<details><summary>Sintomo originale (storico)</summary>

- **Sintomo**: dopo modifica di una nota nella tab Notes (o nel Journal QuickAdd), il bottone Save non persiste o resta disabilitato.
- **Ipotesi root cause**:
  - (a) Toast UI WYSIWYG non emette `'change'` → `MarkdownProperty` (TwoWay) non si aggiorna → `EditorMarkdown` resta uguale a `BodyMd` → `IsDirty` = false → Save disabilitato.
  - (b) Save abilitato ma `INoteService.UpdateAsync` non commita (improbabile, coperto da test).
  - (c) WebView2 non disponibile → fallback `TextBox`; il binding `TextBox.Text → Markdown` DP via `SetCurrentValue` propaga a source TwoWay? Da verificare.
- **Info da raccogliere prima del fix**:
  - Il bottone Save è cliccabile (Enabled) quando digiti, o resta grigio?
  - Hai WebView2 Runtime installato? Banner "WebView2 not available" visibile sopra HabitScreen?
  - Riavviando l'app la nota persiste o sparisce?

</details>

### B2. Costumizzazione note "non funziona particolarmente bene"
- **Stato**: `in-progress` (sotto-bucket "tema editor", 2026-06-23, working tree non committato)
- **Editor theming (working tree)**: l'editor Toast UI ora segue il tema app. `editor.html` ha override CSS completi (chrome/toolbar/superficie/source-mode/mode-switch/popup) su variabili `--rec-*`; `editor.js` ha un handler `setTheme`; `MarkdownEditorControl.PushThemeToEditor` invia le brush sul `ready` **e** — da oggi — sul **cambio tema a caldo** (tripwire `SetResourceReference` su `Brush.Surface`). Anteprima browser fedele in `docs/editor-theme-preview.html` (Toast UI reale + toggle dark/light). Build WPF verde (`EnableWindowsTargeting`). **Da verificare a schermo** (Mac via preview, o app sulla VM).
- **Sintomo**: vago — formattazione markdown nell'editor non si comporta come atteso.
- **Info da raccogliere**: screenshot di un caso specifico (es. "scrivo `**bold**`, vedo `**bold**` invece di **bold**"). Questa è la differenza fra mode WYSIWYG e mode Markdown source.
- **Possibili sotto-bucket**:
  - Toolbar Toast UI ridotta non ha `table`/`task`/`image`/`source-view` toggle.
  - Tema dell'editor: Toast UI usa il suo CSS, non rispetta il dark/light di Recordum. → **affrontato** (vedi "Editor theming" sopra; verifica visiva pendente).
  - Preview/edit toggle nel WYSIWYG.

### B3. "Tante altre cose non funzionano"
- **Stato**: `reported` (2026-05-29 utente)
- **Sintomo**: generico. Da triagiare dettaglio per dettaglio (2026-05-30+).
- **Plan**: per ogni nuovo bug specifico, aprire una nuova voce B<N> qui sotto con sintomo + ipotesi + info da raccogliere.

---

## Rifinitura R1-R10 (stato di ciascuna nell'indice in testa)

### R1. Theming Pass 2 — chrome flat per Button/TabItem/ComboBox
- **Stato**: `fixed` (Theming Pass 1→3, vedi CLAUDE.md Fase 4; tracker allineato 2026-06-11) — Button/TabItem/ComboBox hanno ControlTemplate flat in `BaseStyles.xaml` (Border + ContentPresenter, hover/focus all'accent).
- **Scope (storico)**: BaseStyles Pass 1 (commit `6a15cea`) cabla solo Foreground; il `ControlTemplate` Aero di Button/TabItem/ComboBox usa un gradiente chiaro hardcoded. In dark mode il testo è leggibile ma lo sfondo bottone resta light-gradient = ugly.

### R2. WebView2 fallback UX
- **Stato**: `fixed` (tracker allineato 2026-06-11) — banner di guida con Hyperlink al download del Runtime già presente sopra `HabitScreen` (`HabitScreen.xaml`, `Editor_WebView2_NotAvailable_*`), visibile quando `Availability.IsAvailable=false`.
- **Scope (storico)**: se WebView2 Runtime non è installato, l'editor cade su `TextBox` senza preview markdown. Aggiungere banner di guida con link diretto al Runtime download.

### R3. Toast UI toolbar — opzioni mancanti
- **Stato**: `fixed` (2026-06-09, commit `ce155fe`) — aggiunti `task`/`table`/`codeblock`; source-view già nativo (mode-switch). `image` escluso (no `addImageBlobHook`). Vedi "Blocco limature 2026-06-09 (sera)".

### R4. BackupSchedulerHost — start delay configurabile
- **Stato**: `fixed` (tracker allineato 2026-06-11) — `BackupSchedulerHost` legge `BackupSchedulerOptions` iniettabile (`InitialDelay`/`Interval`); i default sono già reattivi (InitialDelay 2 min, Interval 30 min, "Defaults favour responsiveness"). Configurabile via DI in `ServiceRegistrar`.
- **Scope (storico)**: il fix di M3.10b era "wait one tick = 60min prima del primo run". Per polish: rendere il delay configurabile (es. 5min default per esperienza utente più reattiva).

### R5. Accessibility / keyboard navigation
- **Stato**: `fixed` parziale (2026-06-09, commit `c9442e4`) — color/icon picker resi keyboard-accessible (focus/Invio-Spazio/Escape + AutomationProperties.Name). **Resta aperto** l'audit app-wide: focus order, tab traversal su tutte le view, AutomationPeers, contrasto colori WCAG, nav-tastiera dei controlli custom. Effort separato (candidato milestone/brainstorm dedicato).

### R6. CLAUDE.md ridotto — strategia di consultazione dei file companion
- **Stato**: `fixed` (questo commit) — `CLAUDE.md` rimosso le sezioni storiche, ora pointer a `docs/MILESTONES.md`. Tracker bug in questo file.

### R8. La card Goal non ha più una CTA "apri Configura"
- **Stato**: `reported` (2026-08-08, emerso pulendo A9).
- **Scope**: `GoalProgressCardViewModel` portava un `OpenConfigureRequested` + `OpenConfigureCommand` senza alcun binding XAML né subscriber: la scorciatoia "da qui vai a configurare il goal" era stata pensata e non esiste in interfaccia. Ho **rimosso il codice morto** invece di lasciarlo (dava l'illusione che la feature ci fosse), ma la lacuna d'interfaccia resta: dalla card del goal non c'è modo di saltare alla sezione Goal di Configura.
- **Perché non l'ho ricablato**: aggiungere la CTA significa decidere dove sta il pulsante e come si presenta — giudizio a schermo, e stiamo rimandando le verifiche visive. Da valutare insieme al giro estetico (Onda 3) o quando si guarda la card Goal.

### R7. Template seed "Workout log" — metrica durata senza unità
- **Stato**: `fixed` (2026-08-08, commit `bc23e5a`) — migration data-only **v2.16** `WorkoutLogUnitV216`: il payload seed dichiara `"unit":"minutes"` e `schema_version` sale a 2.16. `SaveAsTemplateAsync` è protetto da `CanLaunch` (guard **anche dentro il metodo**: `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`), quindi il giro salva→istanzia non aggira più il gate — chiude anche il "correlato" qui sotto. Test di guardia: uno percorre **tutti** i template ufficiali e fallisce su qualsiasi metrica `Seconds` la cui unità non sia temporale, così un template aggiunto in futuro non può ripetere l'omissione. **Le abitudini già istanziate dal vecchio template conservano la metrica senza unità**: sono dati utente, coperti dalla decisione "nessun backfill". Trovato dalla review finale del giro "unità durata obbligatoria".
- **Scope**: `HabitTemplateSeed.cs:21` definisce `{"name":"Duration","type":"Seconds"}` senza chiave `unit`, e `HabitTemplateService` lascia `UnitOfMeasureId = null` quando il codice unità manca. Dal 2026-08-07 il builder impedisce di *creare* una durata senza unità e Configura ne assegna una, ma istanziare questo template ufficiale continua a produrre esattamente l'oggetto che la regola vuole rendere impossibile — su ogni installazione nuova, non solo sui dati legacy.
- **Perché non è stato chiuso nello stesso giro**: il seed è `HasData`, quindi correggerlo richiede una migration data-only (stile `SleepRepeatableV215`), esplicitamente fuori dai vincoli di quel piano. Una chiave JSON di distanza dalla correttezza.
- **Correlato**: `HabitBuilderViewModel.SaveAsTemplateAsync` non è protetto da `CanLaunch`, quindi si può salvare un template con un campo Tempo senza unità che non si potrebbe lanciare — il gate del builder è aggirabile col giro salva→istanzia. Simmetrico al buco preesistente sulle scale.

### R9. Il giorno di appartenenza è il giorno **UTC** (e `reset_time` non lo consuma nessuno)
- **Stato**: `fixed` (2026-09-09, branch `r9-day-boundary`) — spec `2026-08-19-day-boundary-design.md`, piano `2026-09-07-r9-day-boundary.md`, 12 task. **Nessun cambio di schema**: `schema_version` resta 2.17, i dati restano istanti, il giorno resta un'interpretazione.
- **Forma della soluzione**: un valore puro `DayBoundary` in Core (`DayOf` / `Today` / `StartOf` / `NoonOf` / `RangeOf`, sincroni, senza I/O) prodotto da `IResetTimeResolver` — che prima non aveva **un solo chiamante**. Si risolve una volta in cima a un'operazione e si passa giù: i servizi Data lo risolvono da sé (le firme pubbliche non cambiano, i ~20 call site restano intatti), i VM WPF lo ricevono come parametro da chi carica la schermata. Convertiti **58 siti di famiglia A** (istante → giorno) e **~20 di famiglia B** (giorno → finestra di query): convertire solo la prima avrebbe dato bucket giusti che pescano da finestre sfalsate, cioè un risultato peggiore di prima.
- **`reset_time` adesso ha un consumatore.** Era configurabile in due punti d'interfaccia — Impostazioni → Generale e la sezione ResetTime dell'editor — e non cambiava il comportamento di niente (stessa forma di F0-14 per le notifiche). È la prima volta che quella manopola fa qualcosa, ed è la cosa da guardare per prima nella verifica a schermo.
- **Rischio dichiarato, non scoperto**: passando da UTC a locale **lo storico si ri-interpreta**. Ogni registrazione fatta fra mezzanotte e l'offset locale cambia giorno, e con essa streak, heatmap ed export del passato: **un numero di streak può cambiare al primo avvio dopo l'aggiornamento**. È corretto — prima erano nel giorno sbagliato — e **non è una regressione**.
- **Conseguenza scelta della spec §6**: `reset_time` **resta fuori da `HabitConfigHistory`** (è un setting, non uno stato versionato), quindi cambiarlo **ri-interpreta anche lo storico**, non solo il futuro. È il prezzo dell'alternativa scelta — i dati sono istanti — e va detto all'utente che sposta il reset: i numeri del passato cambieranno sotto i suoi occhi.
- **Backfill una tantum**: solo le retroattive esistenti, ri-stampigliate a mezzogiorno locale del giorno che già dichiarano (`RetroactiveNoonBackfill`, nell'`ApplicationInitializer`, idempotente, guardato da un AppSetting che nasce alla prima scrittura). Le registrazioni live non si toccano: l'istante è l'istante.
- **Guard**: `DayDerivationGuardTests` rilegge tutto `src/` e fallisce se ricompare un `DateOnly.FromDateTime(...)` o un `.UtcNow.UtcDateTime.Date` fuori da una whitelist di sei siti, ognuno con la sua ragione scritta. `DayBoundaryParityTests` prova che heatmap, calendario, streak, andamento ed export dicono **lo stesso giorno** sugli stessi dati, con reset 00:00 e con reset 04:00, e attraverso il cambio d'ora.
- **Nota per il pass visivo dell'Onda 2, ancora aperto**: le voci §1-§3 della checklist che riguardano cifre e strisce vanno **riguardate dopo R9** — i numeri possono essere cambiati, e legittimamente.
- **Diagnosi allargata (2026-08-19)**: il titolo originale sottostimava. Le registrazioni si stampigliano `clock.UtcNow.UtcDateTime` e si bucketizzano per **data UTC**, quindi in Italia d'estate una registrazione delle 00:30 del 19 **finisce nel 18** — in streak, heatmap, calendario, andamento ed export. Il sistema è coerente in UTC, per questo non produce spazzatura evidente. In più, su 55 punti che derivano un giorno da un istante, **14 calcolano "oggi" in UTC e 9 in locale**: fra mezzanotte e le 02:00 la striscia "Oggi" e il calendario indicano D mentre i dati atterrano su D−1. `reset_time` è la rifinitura configurabile di un confine che non è ancora nemmeno locale.
- **Decisioni prese**: un solo risolutore (locale + cascata `reset_time`) come **valore risolto una volta + funzione pura** in Core, con versione batch per le aggregazioni multi-abitudine; retroattive nuove salvate a **mezzogiorno locale**; **backfill** delle sole retroattive esistenti come passo di manutenzione una tantum allo startup (non una migration: `datetime(…,'localtime')` di SQLite è inaffidabile sulle date passate). Nessun cambio di schema.
- **Scope**: `IResetTimeResolver` è registrato in DI (`ServiceRegistrar.cs:112`) e **non ha un solo chiamante in tutto `src/`**. Il calendario e la heatmap bucketizzano le registrazioni con `DateOnly.FromDateTime(c.CompletedAt)` (`CompletionService.cs:204`, `GetDailyMagnitudeAsync`), cioè mezzanotte civile; gli evaluator di streak (`Services/Evaluators/*`) e le policy di recovery non nominano `ResetTime`. Quindi `reset_time` si configura in due punti d'interfaccia — Impostazioni → Generale (`GeneralSettingsViewModel`) e la sezione ResetTime dell'editor — e **non cambia il comportamento di niente**. Stessa forma di F0-14 (notifiche configurabili senza motore).
- **Non è una regressione**: `docs/MILESTONES.md:152` lo dichiara esplicitamente rimandato — *"L'engine bucketizza per `CompletedAt` calendar date; il bucketing local-by-reset (consumo di `IResetTimeResolver`) arriva in milestone successiva quando 'today' sarà rivisitato"*. Quella milestone non è mai arrivata.
- **Sintomo che si vedrà nel dogfooding**: con reset alle 04:00, una registrazione delle 02:30 conta per il giorno prima nelle *intenzioni* dell'utente ma viene dipinta sul giorno dopo in calendario e heatmap, e lo streak la assegna al giorno civile. Colpisce esattamente chi il reset lo imposta (chi si corica tardi).
- **Perché non si chiude da solo**: tocca streak, heatmap, calendario ed export insieme, e ha un bivio di prodotto vero — o si consuma il risolutore ovunque, o si decide che `reset_time` riguarda **solo** lo streak e si toglie la promessa dall'interfaccia. Sede naturale: **Onda 2.9** ("verità sul core abitudini"), che già ha reset-time nella matrice di parità.
- **Dipendenza**: la spec `docs/superpowers/specs/2026-08-19-calendar-lens-design.md` §7 assume R9 risolto.

### R10. "Oggi" calcolato in UTC nei marker dell'astinenza
- **Stato**: `fixed` (2026-09-09) — chiuso **per costruzione** da R9: i marker e il navigatore ricevono ora lo stesso `DayBoundary` dallo screen, e il guard impedisce che ne ricompaia un secondo. Il test `DetailDayBoundaryTests.Abstinence_markers_use_the_same_today_as_the_calendar` confronta le due superfici fra loro invece di guardarne una due volte: la cella marcata "oggi" dalla griglia e il giorno con cui la colorazione ha chiuso il periodo aperto devono nominare lo stesso giorno.
- **Scope**: `HabitScreenViewModel.BuildAbstinenceMarkersAsync` calcola `var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime)` (`HabitScreenViewModel.cs:915`), mentre `CalendarNavigatorViewModel` costruisce il proprio `_today` convertendo in ora locale (`TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone)`). In Italia (UTC+1/+2), fra mezzanotte e le 01:00/02:00 locali i due "oggi" sono **giorni diversi**: la classificazione clean/lapse/gap dei periodi e l'evidenziazione della griglia si disallineano di un giorno.
- **Fix**: usare la stessa conversione locale del navigatore. Il test lo vuole con un `IClock` fissato a un istante notturno in un fuso positivo.
- **Correlato**: R9 (stessa famiglia — "quale giorno è questa registrazione").

---

## Risolti

### B10. Configura — le sezioni type-specific non compaiono al cambio tipo
- **Stato**: `fixed` (2026-06-01, su `fase-4-polish`)
- **Sintomo (report "Recordum problemi 2")**: creata un'abitudine vuota e aperta Configura, "nulla si inserisce affinché l'abitudine possa essere personalizzata" — niente unità/quantità/cronometro.
- **Root cause**: un'abitudine vuota nasce `Action`; le sezioni Metrics/Scales/Timer sono type-gated (Counter/Resistance/State). La visibilità (`IsVisible = IsApplicable(...)`) era calcolata **solo in `HabitEditorViewModel.LoadAsync`**. `OnSelectedNodeTypeChanged`/`OnSelectedHabitTypeChanged` persistevano il tipo ma **non ricalcolavano** la visibilità → cambiando il tipo a Counter le sezioni non comparivano mai senza riaprire l'editor.
- **Fix**: estratto `RefreshSectionVisibility()`, chiamato in `LoadAsync` + nei due handler di cambio tipo. Test rosso→verde `SectionVisibilityReactivityTests` (Action→Counter rivela Metrics/Timer; Habit→Group nasconde le sezioni habit-only). Suite WPF 337 verdi.
- **NB**: il persist delle sezioni era già corretto (`ApplyAsync`→`IHabitConfigService.UpsertAsync`); il difetto era solo la reattività della visibilità. Restano separati: creazione habit vuota senza prompt + apertura automatica della Cronologia (vedi B3/nuove voci).

### B0. Testo nero su nero in tema scuro — Pass 1
- **Stato**: `fixed` (2026-05-29, commit `6a15cea` su `fase-4-polish`)
- **Verifica utente**: in attesa (Pass 2 chrome flat è R1).
- **Root cause**: `App.xaml` non aveva implicit `Style` per i control type principali → Foreground default = nero anche in dark.
- **Fix**: `Resources/BaseStyles.xaml` con ~20 implicit `Style` cablati ai brush di tema via `DynamicResource`.

### B-prev. Crash tab Dettaglio al click (e poi al launch)
- **Stato**: `verified-fixed` (2026-05-29, commit `abb9e4c` su `fase-4-polish`)
- **Verifica utente**: ✓ confermato — "Dettagli ora va".
- **Root cause**: `CommandParameter="Days30"` string passato a `IAsyncRelayCommand<TrendRange>`. `RelayCommand<T>` throws `InvalidCastException` durante CanExecute check al template materialization.
- **Fix**: `CommandParameter="{x:Static vmd:TrendRange.Days30}"` (et al.) in `HabitScreen.xaml`.

## Onda 1a — Wave 1a: Chart funzionanti + export (2026-07-03 → 2026-07-08, SUSPENDED FOR FINAL REVIEW)

**⚠️ STATUS CHANGE**: Marked "completed" → reconsidered after author visual report. Critical visual/functional blockers W1a-VF1 (charts illegible) and W1a-VF2 (builder UI broken) prevent feature validation. **Author report saved for reckoning at end of all waves** → see `docs/phase-4-issues/2026-07-08-wave1a-visual-findings-author-report.md` (exact resoconto + images).

Spec: `docs/superpowers/archive/specs/2026-07-03-wave-1a-charts-export-design.md`.
Piano: `docs/superpowers/archive/plans/2026-07-03-wave-1a-charts-export.md`.
Branch: `wave-1a-charts-export` (14 task, TDD via subagent-driven-development).
Esecuzione: Tasks 1-13 completati e reviewed (Approved). Task 14 visual verification → findings critici aperti, suite verdi (Data 377/377, WPF 699/699) ma app non usabile per il test effettivo dei chart.

### Findings Onda 0 — status post-Wave-1a

- **F0-1 — Chart builder crash + incomplete UI** · `fixed` (commits `6548921` / `6cedecb`, wave-1a Tasks 11-12-13) · Implementate le righe serie (habit picker, metric picker, label, color swatch, remove button), live preview con RefreshPreviewAsync debounce 300ms, anteprima su colonna 2. Builder UI now complete; Save → error handling try/catch + `ErrorMessage` binding; new test suite ChartBuilderPreviewTests/ChartBuilderRowTests covering crash scenarios. **Nota visual**: vedi W1a-VF1 e W1a-VF2.

- **F0-2 — Streak sempre 0 nel Dettaglio** · `fixed` (commit `c4365ba`, wave-1a Task 5) · StreakEngine fallback a `new HabitConfig` in-memory per nodi orphan (nessuna config row creato); SchedulingAwareStreakAggregator caricato via Task 11 che ri-semina tutte le 9 abitudini con config row valide (Task 9 fix). Verifica visiva Task 14: Dettaglio 5 tipi mostra `CurrentStreak > 0` corretto.

- **F0-3 — Andamento insufficienti (trend) nel Dettaglio** · `fixed` (commit `6548921`, wave-1a Task 11) · TrendChartControl e TrendChartViewModel completati con LiveCharts line series + TooltipCustom + auto-scale. Verifica visiva Task 14: trend line disegnata (non "Dati insufficienti"). **Nota visual**: vedi W1a-VF1.

- **F0-4 — "Ora del giorno" vuota (hourly distribution)** · `fixed` (commit `78f55ab`, wave-1a Task 8) · HourlyHistogramControl nuovo con LiveCharts Column + empty-state placeholder + Rehook on DataContextChanged. Verifica Task 14: orario mostra barre corretto.

- **F0-5 — Export resistance non period-aware** · `fixed` (commit `f858640`, wave-1a Task 3) · ExportQueryService.BuildHabitAsync gains IAbstinenceService, branches Resistance (usa ListPeriodsAsync) vs non-Resistance (completions); ExportPeriodRow + ExportHabitModel.Periods init-property; FormatPeriodDuration helper esportato in CSV/MD/HTML/JSON/PDF. Verifica Task 14: Export Niente fumo (resistance) mostra tabella periodi in tutti i 5 formati.

- **F0-7 — Nome metrica mai esportato** · `fixed` (commit `0b22719`, wave-1a Task 2) · ExportQueryService.BuildHabitAsync carica metricNames dictionary via IMetricDefinitionService.ListByHabitAsync, join su ExportCompletionRow.Metric. Verifica Task 14: Export Lettura (counter) mostra metric="Pagine" in CSV/JSON.

- **F0-9 — Schema backup hardcoded "2.7"** · `fixed` (commit `2549582..39e3dd1`, wave-1a Task 1) · DbSchemaVersion.ReadAsync helper legge il valore reale da AppSetting; BackupService.BuildManifestAsync delega a `DbSchemaVersion.ReadAsync` con fallback `FallbackSchemaVersion="2.7"`; RestoreService fa lo stesso. Verifica Task 14 PDF export: backup nuovo mostra "Schema: 2.12" confermato.

### Visual findings nuovi — Wave 1a Session

Emerse durante Task 14 visual verification (app lanciata, schermo utente):

- **W1a-VF1 — LiveCharts2 incoerente su tema scuro** · `reported` (gravità media-alta, UX) · I grafici trend+hourly (Task 7-8, LiveCharts2) rendono con assi/label/gridline in grigio chiaro (`#E0E0E0` / `#C0C0C0`) che risultano invisibili su sfondo scuro tema app. Il componente LiveCharts2 non sta ricevendo il tema dell'app o non ha overrides per dark. **Impatto**: Detail screen illeggibile; nessun crash. **Onda di fix proposta**: Onda 3 (polish coerenza estetica dual-theme). **Workaround**: forzare LiveCharts2 a usare il tema app via probing `IsDarkTheme` del builder (Task 12) e delle viste di dettaglio; o fork i render renderer per dark.

- **W1a-VF2 — Chart builder: picker abitudine e colonna anteprima non visibili** · `fixed` (2026-07-11, commit `0f4f637`, vedi audit sotto) · Dopo "Nuovo grafico", il form visibilizza Name/Type/Period/AddSeries, ma la riga serie (habit picker ComboBox + metric picker + label + color + remove) non è visibile, né la colonna 2 (anteprima). ~~La layout 2-colonne Grid(*,*) sembra non adattarsi~~ **Root cause vera (audit 2026-07-11)**: il layout è sano e il template riga è completo — `LoadNew()` semplicemente **non creava nessuna riga serie**: il picker non esisteva finché non si cliccava "Aggiungi serie", e la colonna anteprima mostrava solo il testo muto "Scegli un'abitudine per l'anteprima". Fix: `LoadNew()` semina una riga vuota (test `LoadNew_seeds_one_empty_series_row`). Verificato a schermo via UIA che il click su "Aggiungi serie" rende la riga completa. Da ri-verificare dall'autore.

## Audit interattivo 2026-07-11 (sessione Claude: code audit + navigazione UIA + screenshot)

> Contesto: durante la verifica dell'Onda 2.1 l'autore ha segnalato (a) Configura vuota, (b) Mood senza scala, (c) serie non salvabile, (d) chart builder "non c'è niente qui". Audit condotto navigando l'app via UI Automation con cattura schermo. Branch `wave-2-1-home-row`.

**Fixati in sessione (commit sul branch):**
- **A1 — Colonna Configura vuota (editor mai caricato)** · `fixed` (commit `9af35e0`) · Il wiring che caricava `HabitEditorViewModel` quando la colonna si apre viveva in `MainWindowViewModel` ed è andato perso nel passaggio alla workspace shell (`fdb6c2e`): `ConfigureRequested` non aveva più subscriber → la colonna mostrava un editor mai caricato (nome vuoto, chip "azione", zero sezioni). Ora lo screen carica l'editor su apertura chip, su load con colonna già aperta, e su switch di soggetto. Test `HabitScreenEditorWiringTests` (3). **Verificato a schermo**: Mood → Configura mostra tutte le sezioni inclusa "Riga Home".
- **A2 = W1a-VF2** (vedi sopra) · `fixed` (commit `0f4f637`).

**Riparazione dati (danno da bug ComboBox, fix codice in `4d01f9c`):**
- **A3 — Dati azzerati dal wipe pre-fix** · Mood: `ScaleTemplateId` ricollegato a "Mood Scale (1–5)" **in sessione via UIA, verificato**: il quick-add è tornato **slider** con etichette Pessimo…Ottimo (le etichette custom su `labels_json` erano sopravvissute — vivono sulla metrica). **TODO autore**: ri-selezionare l'**unità di misura** nelle Metriche dei counter toccati (Acqua, Allenamento, Lettura, …) — probabile causa del "non riesco a salvare la serie su Allenamento"; se dopo il re-link il salvataggio serie fallisce ancora, aprire finding separato.

**Findings nuovi `reported` (da triare):**
- **A4 — Trend Mood vuoto + card bianche (estende W1a-VF1)** · Dettaglio di Mood in tema scuro: trend e "Ora del giorno" renderizzano su **card bianche** con assi pallidi (screenshot `15-dettaglio-dark.png` della sessione); inoltre il **trend è vuoto** nonostante ~10 giorni di valori registrati (possibile: la query trend per State non legge i valori o risente della scala scollegata fino a poco prima). Verificare anche hourly senza barre.
- **A5 — Heatmap Dettaglio implausibile per Mood** · Le due righe inferiori (sab/dom?) verdi su **tutto l'anno** per un'abitudine con 10 giorni di dati; inoltre ramp **verde GitHub** mentre la striscia Home usa il colore dell'abitudine (giallo) — incoerenza colore=identificatore.
- **A6 — Riga Home: etichetta tipo ridondante e in lingua mista** · Colonna 2 del template riga (`HabitRowTemplate.xaml` ~57) mostra la parola del tipo ("azione"/"contatore"/"journal"/"stato") accanto alla nuova cifra chiave 2.1 — ridondante, e "journal" non è localizzato come gli altri. Candidata a rimozione o spostamento in tooltip.
- **A7 — Albero ristretto: zona destra righe tagliata in modo incoerente** · Con la sidebar stretta (tab aperta) alcune righe mostrano ancora ☐/+, altre li perdono (dipende dalla larghezza di striscia/cifra). Serve una regola di collasso coerente (es. nascondere prima la striscia, poi la cifra, mai ☐/+).
- **A8 — Sezioni editor sbordano dalla colonna Configura** · Il contenuto delle sezioni non si adatta alla larghezza colonna: il ComboBox "Scala" è largo 600px e finisce **oltre il bordo destro della finestra** (rect UIA 1405..2005 con finestra che finiva a ~1760). Le sezioni furono disegnate per il pane editor largo; in colonna serve MaxWidth/wrap.
- **A9 — Eventi/comandi morti (cleanup)** · `fixed` (2026-08-08) — **rimossi**, non ricablati: verificato uno per uno che nessuno rappresentasse una feature persa. `HabitScreenViewModel.ConfigureRequested` + `OpenConfigureCommand`: il lavoro vero lo fa `LoadEditorAsync()`, che A1 aveva già aggiunto accanto all'evento; il ramo "già aperta → ri-emetti" era irraggiungibile (il comando non era bindato) e comunque coperto da `Switching_node_with_Configura_open_reloads_the_editor`. `QuickAddViewModelBase.CompletionSaved` (+ `OnSaved` e i suoi 5 chiamanti): la schermata si aggiorna da sé dopo un salvataggio (fix B5), non ascoltando il quick-add. `ChartBuilderViewModel.SeriesChanged`: emesso sempre in coppia con `RaisePreviewRequested`, che è quello vivo. `CompletionRowViewModel.EditRequested`: il pulsante Modifica **funziona** — `BeginEdit` imposta `IsEditing`, che è ciò che apre l'editor inline; solo l'evento era di troppo. Rimossi anche i 3 test che provavano *soltanto* i meccanismi eliminati (il comportamento reale è coperto da `HabitScreenEditorWiringTests`). Unica eccezione, promossa a finding proprio invece di essere cancellata in silenzio: → **R8**.
- **A10 — Accessibilità UIA** · Item del ComboBox scala espongono `ToString()` come Name (`Recordum.Core.Models.ScaleTemplate`); il bottone chiudi-tab non ha `AutomationProperties.Name`; le righe dell'albero non espongono pattern Invoke/SelectionItem. Impatta screen reader e automazione.
- **A11 — (da confermare con mouse reale) click sull'albero senza effetto dopo l'apertura di una tab** · Nella sessione automatizzata, dopo la prima apertura tab i click sintetici su righe/chevron dell'albero non selezionavano/aprivano più nulla, mentre i pattern UIA (toggle chip, Invoke "+", combo) e i click nella zona workspace funzionavano. Sospetto artefatto dei click sintetici contro `TreeDragDropBehavior` (down/up senza movimento), ma **l'autore verifichi col mouse**: apri una tab, chiudila con ✕, poi clicca un'altra riga — se non si apre nulla, promuovere a bug alto. *(Verifica autore 2026-07-11: navigazione usata normalmente senza lamentele → con ogni probabilità artefatto sintetico; declassato, si chiude alla prossima sessione di verifica.)*

**Esiti verifica autore 2026-07-11 (screenshot, dopo i fix A1/A2):**
- ✅ Configura si popola e segue il soggetto (A1 ok); fix anti-wipe regge; **serie su Allenamento di nuovo salvabile** (screenshot: modulo serie "Minuti 12" attivo); Charts custom: creato un grafico, tipologie apprezzate; Mood ripristinato (riga Home mostra "stato Ok").
- **A12 — Sezioni editor: salvataggio non percepibile** · `reported` (UX) · L'autore cerca "un tasto per salvare" nelle Metriche: l'editor **autosalva a ogni modifica** (`OnUnitOfMeasureIdChanged` → `UpdateAsync`; status Salvataggio→Salvato in fondo alla colonna) ma l'affordance è invisibile — lo status è fuori viewport quando la sezione è in alto. Candidato: status "Salvato ✓" inline per-sezione o vicino al campo modificato.
- **A4 (esteso) — Trend/orario del Dettaglio vuoti ANCHE per i counter** · Screenshot Acqua (seed demo ~100 giorni): trend e "Ora del giorno" = griglia bianca vuota, assi compressi, nessuna serie né empty-state "Dati insufficienti". La **schermata Charts custom invece renderizza** → il difetto è nel percorso Dettaglio (`TrendChartViewModel`/`HourlyHistogramControl` + `ITrendQueryService`), non in LiveCharts2 in sé. Possibile regressione post-1a (merge 1b/2.0/2.1) o dataset non colto dalla query. **Direzione approvata dall'autore**: vuole "semplici grafici x-y" coerenti — candidata unificazione del Dettaglio sul renderer del ChartEngine (stessa estetica + dark theme risolto una volta sola). Priorità alta: da triare prima/durante Onda 3.
- **A5 (confermato anche su Acqua)** — le due righe inferiori della heatmap annuale verdi ovunque anche per Acqua; pattern identico a Mood → bug di mapping riga/giorno del `YearHeatmap`, non dato del singolo habit.
- **A13 — Riga Home: verdetto estetico autore = NON ci siamo (gap 2.1-bis confermato)** · `verified-fixed` (2026-07-11 — approvazione autore "va bene così, mergia"; commit `7d5aa25`: stili striscia per-tipo da spec §1 con `StripStyle`+`ValuesLast14Days`+`HeatmapStrip` a larghezza fissa 96px, banda continua resistance, parola tipo → tooltip (chiude anche **A6**), zero-state ×0/0 muted; sparito anche il bordo-checkbox sull'ultima cella. Core 92/Data 415/WPF 754 verdi) · (a) Camminata mostra "semplicemente i quadranti": lo streak chip è nascosto quando non c'è streak corrente (seed stale) → la riga sembra vuota; serve uno stato zero leggibile. (b) Niente fumo "pieno di quadratini": la striscia resistance è 14 celle discrete vs 7 delle altre — l'autore chiede **stessa larghezza visiva** e una **banda continua** vera, non quadratini. (c) Parola del tipo ridondante (=A6). Scope del giro **"2.1-bis" prima del merge**: stili striscia per-tipo dalla spec §1 (mini-barre counter, ramp State, punti journal, banda continua resistance), larghezza visiva uniforme (14 giorni = celle più strette, stessa larghezza totale), rimozione parola tipo, zero-state del chip.



## Onda 2.3 — pass visivo 2026-07-28 (W23-VF1…VF8) + chiusura 2026-08-07

> Contesto: pass visivo dell'Onda 2.3 (U8 registrazione) sul branch `wave-2-3-registration`. Evidenza
> in `docs/verification/2026-07-28-wave-2-3/` — 8 screenshot + `REPORT.md`. **La cartella è in
> `.gitignore`**: è evidenza locale, non storia del repo, e quella è l'unica copia.
> Nessuno dei `fixed` qui sotto è ancora `verified-fixed`: il pass visivo di conferma è da fare.

**Chiusi in codice:**

- **W23-VF1 — Quattro numeri diversi per lo stesso giorno su un tap-counter** · `fixed` (commit `3cec5d1`) · Due cause indipendenti che si sommavano: nel Data mancava il fallback "conta le occorrenze" per un counter nudo (senza metriche), e il `DayCount` del quick-add a tap non veniva mai riallineato dopo una registrazione. Riga Home, striscia "Oggi", badge calendario e totale Dettaglio divergevano fra loro.
- **W23-VF2 — Metrica durata senza unità: i totali mostrano secondi grezzi** · `fixed` (commit `9c759d9`) · Il fix precedente `d1bbecf` copriva solo il caso *con* unità. Vedi anche il giro del 2026-08-07 qui sotto, che rimuove il caso alla radice per le metriche nuove.
- **W23-VF3 — Le serie nominate non arrivano in Cronologia** · `fixed` (commit `9c759d9`) · Le serie rinominate ("Night"/"Nap") si rileggevano come "Serie 1"/"Serie 2".
- **W23-VF6 — Formato incoerente fra schede di registrazione** · `fixed` (commit `5949f58`) · Una registrazione multi-serie stava su una riga per serie con prefisso, una a serie singola spezzava i campi su una riga ciascuno: due layout per lo stesso oggetto. Ora sempre una riga per serie; la serie sola si limita a non stampare il prefisso.

**Rimandati al giro sul modello counter** (questioni **CM-1…CM-4**, v. §"Onda 2.4 — review finale" in fondo al file):

- **W23-VF4 — Il cronometro non è scopribile dal builder** · `triaged` (UX, media) · Il ⏱ nasce solo dal campo "+ Tempo": chi costruisce l'abitudine non ha modo di sapere che esiste.
- **W23-VF5 — Le abitudini "cronometro" pre-esistenti non ricevono niente** · `triaged` (media) · Il gate guarda il *tipo* metrica `Seconds`, non l'unità: un'abitudine costruita prima resta esclusa dalla feature.
- **W23-VF8 — Badge calendario = occorrenze, totale = somma** · `triaged` (domanda di modello) · Non è un bug ma una scelta mai decisa: due numeri legittimi per lo stesso giorno.

**Pre-esistente, non dell'onda:**

- **W23-VF7 — Grafici Dettaglio vuoti** · `triaged` → Onda 3 · **Duplicato di A4** (audit 2026-07-11), che ha l'analisi più completa: il difetto è nel percorso Dettaglio, non in LiveCharts2, e la direzione approvata dall'autore è unificare il Dettaglio sul renderer del ChartEngine. Tracciare A4, non questo.

**Chiusi nella stessa finestra ma senza numero VF** (emersi analizzando i VF, non dal pass visivo):

- **Goal-actual 60× su metrica durata** · `fixed` (commit `bebfd06`) · Un goal su metrica `Seconds` sommava `value_seconds` grezzi mentre il target era digitato nell'unità: 7,5h leggeva 27000 contro un target di 10. Nuova regola condivisa `SecondsDisplay.ConvertTotal` in Core; goal card ed export convertono; i 3 siti che ricopiavano la conversione a mano ora la chiamano (e guadagnano il guard fuori range che non avevano).
- **Export: valori senza unità, e il goal invisibile** · `fixed` (commit `37ecd8c`, `09b3107`) · Tutti e 5 i formatter stampavano il valore come numero nudo (7.5 senza "hours"; una durata senza unità usciva "27000"). E `GoalActual`/`GoalTarget` esistevano nel modello ma **solo il JSON li emetteva**, perché serializza l'intero model: Markdown/HTML/PDF buttavano via il goal, quindi il fix `bebfd06` era invisibile in ogni formato leggibile. CSV lasciato fuori di proposito dal summary (è il formato macchina a righe).
- **Unità temporale obbligatoria su metrica durata** · `fixed` (commit `7f628c3`→`1d96967`) · Chiude alla radice il caso di VF2 per le metriche nuove. Spec e piano in `docs/superpowers/`; decisioni di modello riassunte in CLAUDE.md §7 e nella memoria `recordum-duration-unit-required`. Vedi **R7** per il buco che resta aperto (template seed).


## Onda 2.4 — review finale (2026-08-09/10): residui parcheggiati + questioni-modello counter

> Contesto: review finale dell'intero branch `wave-2-4-food-diary` (opus, `5d9667a..3925a9e`): due Critical
> trovati e fixati — **C1** WebView2 fantasma (un `ContentControl` con `ContentTemplate` esplicito applica il
> template anche con `Content` null; chiuso con `ContentTemplateSelector`, `1a97aa8`) e **C2** due sezioni di
> Configura che si annullavano su `ui_prefs_json` (nuova merge API `IHabitUiPrefsService.UpdateAsync`,
> `16f6ffb`) — più tre Important (`21038f6`, `f15a8bc`, `17eca02`). Suite alla chiusura: Core 113 / Data 440 /
> WPF 872 / Plugins 66 / ExportEngine 37 / ChartEngine 18 = 1546. I residui sotto erano rimasti solo nella
> prosa di CLAUDE.md §7 e in memorie machine-local: trascritti qui il 2026-08-11 perché il tracker è l'unica
> lista che ogni sessione (e ogni modello) legge.

**Residui della review (nessun secondo giro di fix, decisione 2026-08-10):**

- **W24-R1 — Il gesto journal del builder si scrive dopo la chiusura del dialog** ·
  **`fixed` (2026-08-12, Onda 2.5 T10)** · `HabitBuilderViewModel` scriveva le ui_prefs *dopo* aver impostato
  `CreatedHabitNodeId`, che ha già chiuso il dialog: un errore transitorio nella scrittura sarebbe uscito da
  un comando già "riuscito". Il gesto viaggia ora **dentro** `ComposedHabitRequest.UiPrefs` (capacità creata
  dal T2), che `ComposedHabitService` serializza sul nodo **prima** dell'insert, nella stessa transazione di
  tutto il resto: non c'è più una scrittura post-creazione da fallire.
- **W24-R2 — Il test di regressione dell'interleaving non fallisce contro il codice pre-fix** ·
  **`fixed` (2026-08-12)** · la causa non era solo nel test: `ReloadEntriesAsync` **rileggeva `_day` dopo
  l'await**, quindi un reload superato ricostruiva il giorno *nuovo* e sembrava corretto. Il giorno si cattura
  ora all'ingresso — è anche un difetto vero (un reload deve riguardare il giorno per cui è partito), e solo
  così il test discrimina.
- **W24-R3 — I due test su `IsSaving` asseriscono solo false→false** · **`fixed` (2026-08-12)** ·
  `FakeDiaryService.NextWriteDelay` tiene la scrittura aperta, così lo stato "in volo" è osservabile: i test
  asseriscono ora `false → true → false`.

  > **Verifica del fix, non solo del verde** (è il difetto che questi due item denunciavano): disattivati
  > temporaneamente i tre generation-guard e l'assegnazione `IsSaving = true`, i **tre** test falliscono;
  > ripristinato il codice, tornano verdi. Un test che non sa fallire non è una copertura.
- **W24-Q1 — `NotesTabView` ospita un secondo `MarkdownEditorControl` solo `Collapsed`** ·
  **`verified-fixed` (2026-08-12, Onda 2.5 T9)** · **adjudicata, e l'esito era quello brutto**: il test
  allargato (`Quick_note_journal_materializes_no_markdown_editor_anywhere_in_the_screen`, whole-view scan col
  pannello Note **chiuso**) è nato **rosso** — l'editor del Note si istanziava comunque, quindi la promessa
  "niente WebView2 per un Food diary" era **falsa** anche dopo il fix C1, che copriva solo la colonna Diario.
  La sonda dell'implementatore 2.4 su `Collapsed` era dunque corretta. Chiusa con la stessa cura di C1, ma
  sul Content invece che sul template: `HabitScreenViewModel.NotesForColumn` torna il VM solo mentre
  `ShowNote`, così a colonna chiusa il `ContentControl` ha `Content` null e non c'è nulla da costruire.
  **Lezione confermata**: `Visibility="Collapsed"` non impedisce la materializzazione — solo un Content null
  (o un selector) lo fa.

**Questioni-modello counter CM-1…CM-4** (trasferite dalla memoria machine-local
`recordum-counter-repeatability-rethink`; giro di *decisioni* con l'autore, nessuna esecuzione prima; si
adjudicano preferibilmente col dogfooding, E21):

- **CM-1 — Flag "ripetibile" inerte sui tap-counter** · un counter senza metriche ignora il flag, ma
  l'etichetta in Configura promette un comportamento che non c'è.
- **CM-2 — Sonno: totale-vs-sessioni** · il template Sleep ripetibile somma le serie, ma non è mai stato
  deciso se "il valore del giorno" sia il totale o la lista delle sessioni (imparentato con W23-VF8).
- **CM-3 — Metrica durata + unità separata** · la durata è una metrica `Seconds` + unità di visualizzazione:
  rivedere se l'unità debba far parte del tipo (goal-actual 60× e W23-VF2 nascevano esattamente qui; il giro
  "unità obbligatoria" del 2026-08-07 ha tamponato per le metriche nuove, non risolto il modello).
- **CM-4 — Più metriche durata / più cronometri** · `active_timer` ha UNIQUE su `habit_node_id`: il secondo
  cronometro simultaneo sulla stessa abitudine è strutturalmente impossibile oggi.

**Gotcha ambiente scoperto durante la verifica pre-merge (2026-08-11):**

- **W24-E1 — Il run WPF completo può appendersi con le collezioni xunit parallele (VM)** · `reported`
  (ambiente, non difetto dell'app) · Due hang riprodotti nella notte 2026-08-10/11: 855 e poi 848 test verdi,
  zero falliti, poi stallo a CPU ferma; il blame-dump nomina in volo `MarkdownEditorControlTests`
  (sonda WebView2), `BootstrapSmokeTests`, `HabitScreenDetailSmokeTests`, `CrashLoggerTests`,
  `BackupRoundTripE2ETests`. Gli stessi 5 gruppi sono verdi in isolamento (24/24) e il run **serializzato**
  (`dotnet test -- xUnit.ParallelizeTestCollections=false`) è **872/872 in 25s**. Sospetti: sonda WebView2
  concorrente + I/O DriveFS. Se si ripresenta: serializzare le collezioni, non inseguire i singoli test.
  (Nello stesso giro: il lock `CreateAppHost` di DriveFS ha rotto una build — `-p:UseAppHost=false` resta
  obbligatorio, come da mappa §9.2.)

---

## Onda 2.5 — builder come superficie di composizione completa (2026-08-12)

> Branch `wave-2-5-builder`, 11 task dal piano `docs/superpowers/archive/plans/2026-08-11-wave-2-5-builder.md`.
> Suite a fine onda: **Core 118 / Data 445 / WPF 983 / Plugins 66 / ExportEngine 37 / ChartEngine 18 = 1667**
> (WPF serializzato, W24-E1). **Nessuna migrazione di schema**: resta 2.17.

**Chiuso in quest'onda:** W24-R1 (`fixed`), W24-Q1 (`verified-fixed`, adjudicata — v. sopra).

**Trovato dal guard di parità, chiuso subito (E16 in azione):**

- **W25-1 — Il builder non sapeva comporre una Quantità decimale senza unità** · `fixed` (T10) · il seed
  "Workout log" dichiara `Weight` come `Decimal` senza unità, ma nel builder una Quantità senza unità
  derivava sempre `Integer`: un template ufficiale che l'utente **non** poteva ricostruire, cioè esattamente
  la violazione del principio 8 che E16 esiste per intercettare. Chiuso col mattone mancante
  (`FieldBlockViewModel.AllowsDecimals`, offerto solo finché il campo non ha un'unità — un'unità implica già
  le frazioni), non aggiustando la fixture. **È la prima cattura del guard, al primo run.**

**Review finale di branch (2026-08-12, `main..wave-2-5-builder`, 19 commit / 57 file) — 6 findings, tutti chiusi** (commit `26c487c`, 5 test di regressione):

- **F1 (alto)** — un goal `Fixed`/`Rolling` violava `ck_goal_period_consistency`: `GoalBrickViewModel.Build`
  non ha mai scritto `period_start`/`period_end`/`rolling_days`, ma la ComboBox offriva tutti e tre i
  periodi. Lancia moriva su una violazione di constraint **senza try/catch e senza superficie d'errore**
  (il pulsante sembrava non fare nulla); lo stesso payload salvato come template esplodeva dentro
  `InstantiateAsync` **dopo** che il nodo era già stato committato → abitudine orfana senza config.
  Chiuso restringendo il builder ai **goal aperti** (l'unica forma che i template ufficiali usano) e
  togliendo la manopola `PeriodType`, che sarebbe rimasta una bugia un livello più sotto. Fixed/Rolling
  restano in Configura, che ha i campi e la validazione.
- **F2 (alto)** / **F3 (medio)** — il builder era **l'unico writer che saltava**
  `HabitConfigService.ValidateCrossFields`, perché `ComposedHabitService` scrive la config direttamente
  sul DbSet. "Volte a settimana"/"ogni X giorni" col campo vuoto o a zero creava un'abitudine su cui poi
  esplodeva *ogni* modifica in Configura e *ogni* istanziazione da template; "giorni specifici" senza
  giorni falliva ancora più in silenzio (mai programmata, streak ferma a zero). Chiusi da un unico gate
  `Schedule.IsComplete` su Lancia **e** Salva-come-template.
- **F4 (medio)** — la spunta "Decimali" occupava la stessa colonna del picker unità e ne copriva la
  freccia su ogni campo Quantità appena aggiunto. Ha una colonna sua.
- **F5 (basso)** — `IsHitTestVisible=False` sullo `ScrollViewer` uccideva rotella e cursore della
  scrollbar: un'anteprima più alta della finestra aveva il fondo irraggiungibile. L'inerzia è passata al
  contenuto.
- **F6 (basso)** — rimuovere un campo lasciava aperto il micro-form puntato su un VM staccato.

**Difetto del seed emerso da F3 — nuovo:**

- **W25-2 — Il template ufficiale "Workout log" crea un'abitudine mai programmata** · `fixed` (2026-09-09,
  migration data-only **`WorkoutLogWeeklyTargetV218`**, schema **2.18**) · Il seed dichiara ora
  `"schedule_type":"WeeklyX"` con `"schedule_weekly_target":3` — **tre volte a settimana, i giorni che
  vuoi**. È anche l'unica lettura coerente con lo `streak_mode: "Weekly"` che il payload già portava:
  `SpecificDays` lì dentro era un residuo, non una scelta. Scartata l'alternativa "giorni fissi
  lun/mer/ven": un template spedito indovinerebbe la settimana di uno sconosciuto, e chi li vuole fissi
  li mette da Configura (principio 8 — il template dà il mattone, non la settimana di qualcun altro).
  Nota di modello emersa decidendo: **la scelta non tocca la correlabilità dei dati**, perché statistiche
  e incroci leggono le completions e i loro istanti, non lo `schedule_type` — che decide solo in quali
  giorni l'abitudine è attesa e come si calcola la streak. **Guard**:
  `No_shipped_template_ships_a_schedule_that_never_comes_due` percorre tutti i template ufficiali e
  fallisce sulle tre forme non programmabili (SpecificDays senza giorni, WeeklyX senza target,
  EveryXDays senza intervallo), provato rosso contro il seed vecchio; più il test specifico
  `Shipped_Workout_log_template_materializes_a_schedule_that_comes_due`. Aggiornata anche la fixture di
  `BuilderTemplateParityTests`, che fin qui **documentava** il difetto. **Le abitudini già istanziate dal
  vecchio template restano non programmate**: sono dati utente, coperte dal "nessun backfill" — si
  sistemano da Configura. Testo originale del finding:
  il payload dichiara `"schedule_type":"SpecificDays"` ma **non dice quali giorni**, e
  `ScheduleEvaluator.MatchesDayMask(date, null)` è `false` per ogni data: l'abitudine istanziata dal
  template non scade mai e la sua streak resta a zero per sempre. Non è un difetto del builder — che ora
  quella composizione la **rifiuta** — ma del template spedito. La fixture di parità lo documenta
  (`BuilderTemplateParityTests`, "Workout log"). **Fix = migration data-only** (dichiarare i giorni, o
  passare a `WeeklyX` con un target, coerentemente con `streak_mode: Weekly`) → **fuori dall'Onda 2.5**,
  che non tocca lo schema. Quando si fa, il gate naturale è un test-guardia gemello di R7: percorrere
  tutti i template ufficiali e fallire su qualsiasi config non programmabile.

**Deviazioni dal piano, con la ragione:**

- **T9, terzo riquadro dell'anteprima** — il piano prescriveva i DataTemplate quick-add veri. Vivono dentro
  le risorse di `HabitScreen.xaml` (≈500 righe, cablate a VM DB-backed): estrarle dalla schermata che
  l'autore non ha ancora verificato a schermo comprava una regressione, non fedeltà. Usato il "modello
  visivo minimo" che la spec §3 autorizza esplicitamente. Effetto collaterale desiderabile: "niente WebView2
  nell'anteprima" diventa vero **per costruzione**, non per un binding che si dà il caso sia null.
- **T7, badge "nuova" sulle voci staged** — non fatto. L'unico segnale bindabile in XAML era `IsOfficial`,
  che marcherebbe anche le unità definite dall'utente in Configura: un badge che mente è peggio di nessun
  badge. → decisione al pass visivo.
- **T9, tier 1** — `HomeRowViewModel` di produzione non è riusabile (4 servizi + machinery albero), come la
  spec prevedeva: usato `HomeRowPreviewModel` + il `HeatmapStrip` **vero**, con stile per-tipo risolto da
  `HomeRowPresentation` (Core), che era già documentato come condiviso con l'anteprima del builder.

**Da guardare al pass visivo unico dell'Onda 2** (nessuno di questi è un difetto noto: sono scelte da
validare a schermo):

- larghezza del dialog portata da 720 a **1080** per la colonna anteprima: verificare su schermo piccolo;
- l'anteprima è **inerte** (`IsHitTestVisible=False`): verificare che non sembri "rotta";
- micro-form unità/scala resi **inline** sotto la lista campi invece che in un `Popup` accanto al picker;
- il riquadro "Oggi" monta il componente vero: controllare che non sbordi dai 360px.

---

## Pass visivo unico dell'Onda 2 (2026-08-14) — findings `W2V-*`

Sessione con l'autore sulla build di `main` (`d6cc97e`, schema 2.17), DB **vuoto** → 8 template ufficiali
istanziati + registrazioni su 12/13/14 agosto. Checklist vincolante:
`docs/superpowers/plans/2026-08-11-wave-2-visual-pass-checklist.md`. Evidenza fotografica:
`C:\Users\sonit\Pictures\Screenshots\report_recordum` (7 screenshot, **fuori dal repo** — da spostare in
`docs/verification/2026-08-14-wave-2-pass/`).

**Confermato funzionante** in questo giro (sezioni 1–3 della checklist, per quanto gli screenshot coprono):
serie nominate dai template col rinomina inline (`Night` su Sleep tracker → **W23-VF3 chiuso a schermo**);
durata coerente nei tre posti (`12 minutes` in riga Home e fascia Oggi = aggregato nell'unità, `Duration: 12:00`
nelle Registrazioni = evento singolo come orologio → **il fix 60× regge**); nessuna unità doppiata accanto al
campo (**W23-VF2**); goal di Water intake con barra + `100%`; fascia "Oggi" completa e per-tipo (action:
`Segna come fatto · Streak ×1 · 3% ultimi 30 giorni · di solito verso le 20:00`); calendario con selezionato =
anello e completato = fill; cinque stili di striscia distinti e allineati a destra su tutte le righe.

**Falso allarme scartato dopo lettura del codice**: la banda verde continua di *Stop smoking* a zero periodi
non dichiara 14 giorni di astinenza — i giorni scoperti sono la traccia debole ad alpha 24/255
(`HeatmapStrip.xaml.cs:129-131`). Comportamento corretto.

- **W2V-1 — Le date sono in formato americano dentro un'app in italiano** (`verified`, screenshot + codice).
  Sintomo: `Registrando per: 8/14/2026` e `Nota del giorno 8/14/2026` su ogni tipo. **Non è F0-6** (che chiede
  il problema opposto: poter *presentare* l'app in inglese). Causa: `HabitScreen.xaml:212` (e i 4 gemelli alle
  righe 458/523/673/704) usano `Text="{Binding CompletionDate, StringFormat=d}"`, e `StringFormat` in XAML
  formatta con il `Language` del `FrameworkElement`, che WPF lascia a **en-US** a prescindere da
  `CultureInfo.CurrentCulture`. Le stringhe passano invece dalle `.resx` (`CurrentUICulture` = it) e i numeri
  da `CurrentCulture` in C# — infatti la stessa schermata stampa `Weight: 30,0` con la **virgola** e i timestamp
  delle Registrazioni in ISO. Tre convenzioni contemporaneamente. Fix: override di
  `FrameworkElement.LanguageProperty` con `XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)`
  in `App.OnStartup` — una riga, ma **va censito ogni `StringFormat` della UI** perché il difetto è di famiglia,
  non di quel binding. Priorità autore: **alta**.
  → **`fixed` 2026-08-17** (mini-giro pre-pass, commit `culture`): `CultureBootstrap.Apply(CultureInfo.CurrentCulture)`
  in `App.OnStartup`, prima di qualsiasi finestra. **Censire i singoli `StringFormat` non serve**: l'override
  del metadato copre l'intero albero visuale in un colpo solo. I test asseriscono la **conseguenza** (una data
  bindata rende `14/08/2026`), non il meccanismo. `LanguageFor` è separato da `Apply` perché
  `OverrideMetadata` si può chiamare **una volta sola** per processo — quindi cambiare cultura richiederà un
  riavvio, vincolo che F0-6 deve rispettare (già annotato lì).
- **W2V-2 — Zero-state muto proprio sui tipi che ne hanno più bisogno** (`verified`, screenshot + codice).
  Sintomo: a DB appena seminato *Stop smoking* e *Mood tracker* non mostrano **nessuna** cifra chiave, mentre
  gli altri sei mostrano `×0` / `0 l` / `0 hours` / `—`. Causa: `HomeRowViewModel.cs:228-242` — `LiveClock`,
  `StateValue` e `JournalEntriesWeek` tornano `string.Empty` quando non c'è dato, e `ShowKeyFigure` nasconde
  il chip. È **la stessa lezione già imparata e applicata solo allo streak**: il commento a
  `HomeRowViewModel.cs:286` dice che `×0` smorzato fu scelto apposta perché *"an empty right zone made the row
  read as broken during author verification"* (A13a, 2.1-bis). Caso peggiore l'astinenza: la riga non invita in
  nessun modo a iniziare e il gesto si scopre solo dal `+`.
- **W2V-3 — I nomi delle abitudini sono tagliati netto, senza ellissi** (`verified`, screenshot + codice).
  Sintomo: con la colonna Home stretta (workspace aperto) si legge `Mood track`, `Sleep tracke`, `Workout`.
  Causa: `HabitRowTemplate.xaml:46` — il `TextBlock` del nome non ha `TextTrimming="CharacterEllipsis"`.
  Imparentato con **A7** ma distinto: A7 è la *zona destra* che perde ☐/+ in modo incoerente, questo è il
  **nome** che sparisce senza segnalarlo. Da fixare insieme ad A7 (stessa regola di collasso).
- **W2V-4 — Nel calendario il cerchietto del conteggio si sovrappone al numero del giorno** (`verified`,
  screenshot ingrandito 3× + codice). Sintomo: nella cella del 14 agosto di *Morning routine* il bordo del
  cerchio `2×` **taglia la cifra "4"** del "14"; su *Mood tracker* il cerchio ① tocca il "13". Causa:
  `CalendarNavigator.xaml:64-72` — il numero del giorno è ancorato in alto a sinistra con `Margin="5,3"` a
  `FontSize=10`, il badge è centrato con `Height=18` e `MinWidth=18`+`Padding="4,0"`: alla dimensione reale
  della cella i due riquadri si intersecano, e il problema peggiora sui giorni a due cifre e sui badge larghi
  (`2×`, `12`). Serve o spostare il badge (in basso a destra) o rimpicciolirlo, non entrambi al centro.
- **W2V-5 — Lo stesso cerchietto dice tre cose diverse a seconda del tipo, e per un contatore non dice il
  valore** (`verified`, screenshot + codice). Sintomo: *Mood tracker* mostra `3` (= il **livello** della scala,
  Neutral), *Morning routine* mostra `2×` (= le **volte**), *Water intake* mostra `1` **mentre il totale del
  giorno è 12 litri**. Causa: `HabitScreenViewModel.NumberFor` (:1014-1025) sceglie tre semantiche diverse, e
  per Counter/Resistance la magnitudine arriva da `CompletionService.GetDailyMagnitudeAsync` (:211-212) come
  **numero di occorrenze/serie**, non come somma dei valori. Conseguenza più grave del badge: la stessa
  `mag` alimenta `TintStepFor`, quindi la **heatmap del mese di un contatore è piatta** — un giorno da 1 litro
  e uno da 12 hanno identica tinta, perché entrambi valgono "1 occorrenza". Il comportamento è **conforme alla
  spec §12** (che definisce la magnitudine di un counter come occorrenze), quindi non è una regressione
  d'implementazione: è la spec a produrre un calendario che non mostra il dato che l'utente ha inserito.
  → **decisione di modello**, stessa famiglia di CM-1…CM-4: da adjudicare col dogfooding, non a tavolino.
  Nel frattempo resta almeno da disambiguare il badge (tre significati, nessuna etichetta).
- **W2V-6 — Le azioni di astinenza non aggiornano la Home, e dopo una ricaduta la riga continua a scorrere un
  cronometro morto** (`verified`, autore + codice). **Priorità alta: è un dato falso a schermo.** Sintomi
  osservati in sequenza: (a) premuto **Inizio** su Stop smoking, la riga Home **non** parte — il cronometro
  compare solo dopo un giro in "Archiviate" e ritorno (cioè dopo un reload); (b) premuta **Ricaduta**, la riga
  Home **continua a contare** come se l'astinenza fosse ancora in corso; (c) solo un altro reload la azzera.
  Causa: `HomeViewModel.cs:91-94` si ricarica su `CompletionRecorded/Updated/Deleted` + `HabitUpdated`, ma
  l'astinenza a stato esplicito (U9) **non produce completion** e `WellKnownEvents` **non ha nessun evento
  `abstinence.*`** (`src/Recordum.Plugins.Contracts/Events/WellKnownEvents.cs`: solo completion.*, note.*,
  habit.updated). Una mutazione di dati che nessuna altra superficie può osservare. Aggravante: la riga ha un
  ticker attivo ogni secondo (`HomeRowViewModel.RefreshClock`) che continua a ricalcolare
  `now − AbstinenceOpenSinceMs` su uno stato ormai chiuso, quindi non è un valore fermo ma **un valore vivo e
  sbagliato**. Fix: pubblicare eventi di astinenza (start / lapse / period-deleted) da `IAbstinenceService` e
  sottoscriverli dove i periodi si leggono (Home, Dettaglio, calendario, quick-add).
  → **`fixed` 2026-08-17** (mini-giro pre-pass): tre nomi nuovi in `WellKnownEvents`
  (`abstinence.started` / `.lapsed` / `.period.deleted`) e **un solo** `AbstinenceChangedPayload` per tutti e
  tre — ogni ascoltatore reagisce allo stesso modo, il nome distingue per chi servisse. `AbstinenceService`
  pubblica **dopo** `SaveChanges`, e i **tre guard** (secondo Start, Lapse senza periodo aperto, id ignoto)
  restano muti: un test lo blocca, perché annunciare un cambiamento che non è avvenuto farebbe ricaricare
  tutti per niente. `HomeViewModel` si iscrive ai tre. **Non toccato** `HabitScreenViewModel.OnAbstinenceChanged`:
  la schermata ha già un refresh locale via evento C# della quick-add (U9.1) e funziona; ora però il bus
  renderebbe possibile toglierlo — resta un'occasione di semplificazione, non un difetto (nota per l'Onda 3).
- **W2V-7 — Il dialog di conferma è una MessageBox di Windows, fuori dalla famiglia visiva** (`verified`,
  screenshot + codice). "Eliminare questa registrazione d'astinenza?" arriva come finestra di sistema chiara,
  con l'icona "?" blu Win32 e i pulsanti Sì/No standard, dentro un'app dark interamente tematizzata. Causa:
  `Services/MessageBoxConfirmationDialog.cs:9` → `MessageBox.Show(..., MessageBoxImage.Question)`.
  L'astrazione `IConfirmationDialog` esiste già (U9.1): serve solo un'implementazione tematizzata, che vale
  per ogni conferma futura. Nota di design brief: "niente controlli desktop reinventati" **non** copre questo —
  qui non si reinventa un controllo, si applica il tema a un dialog dell'app.
- **W2V-8 — Il composer nota veloce non dice dove scrivere** (`verified`, screenshot + codice). Il campo di
  testo è una hairline senza etichetta né placeholder: la voce del giorno si scrive in un punto che non si
  vede. Il placeholder **esiste come stringa ma non viene mai reso**: `JournalQuickEntryView.xaml` lo passa
  come `Strings.Diario_QuickEntry_Placeholder` sul `TextBox.Tag`, ma in `BaseStyles.xaml` non c'è nessun
  meccanismo Tag-driven che lo disegni (stesso pattern morto di `EditorPane.xaml:62`). Era **parcheggiato**
  in CLAUDE.md §7 "in attesa del pass visivo": ora è osservato → promosso a finding. Minore collegato: la
  lista "Voci di questo giorno" non ha stato vuoto (resta il solo titolo).

**Confermato funzionante in questo blocco**: Food diary apre il **composer di note veloci** e non l'editor
Markdown, senza flash né processo browser → **C1** (Onda 2.4) e **W24-Q1** (Onda 2.5) tengono a schermo, e la
promessa "niente WebView2 per un journal QuickNote" è vera in esecuzione, non solo nei test. La ricaduta
registra correttamente l'**intervallo di tempo**, la cancellazione chiede conferma e funziona (U9.1).

**Builder (§6) — i tre dubbi aperti del piano 2.5 sono chiusi, tutti in positivo** (autore, 2026-08-14):

- **Sfarfallio dell'anteprima mentre si scrive**: **non si verifica**. Era un sospetto *da codice* (il
  DataContext del pannello è un record nuovo a ogni proprietà, quindi il riquadro "Oggi" ricostruisce il
  proprio sotto-albero visuale a ogni tasto) annotato in checklist §6 e mai visto girare: scrivendo un nome
  lungo lettera per lettera l'anteprima non lampeggia. **La cura proposta — ricostruire il piano "Oggi" solo
  al cambio di tipo derivato — non serve**: nessun intervento.
- **Larghezza 1080px**: il dialog si apre correttamente sullo schermo dell'autore. Era il cambio più invasivo
  dell'onda sul layout.
- **Anteprima inerte** (`IsHitTestVisible=False`): non sembra rotta né disabilitata — giudizio dell'autore
  "ottima". La scelta regge.

Altro verificato a schermo nel builder (composizione "Abitudine di prova creazione": Quantità in grammi,
obiettivo 3, sessione a 2 segmenti): chip **"Si comporta come: Counter"** (tipo derivato) corretto; **"+"**
accanto al picker unità presente; la spunta **"Decimali" correttamente assente** perché il campo ha già
un'unità (Gram); **Programma e streak** collassato che legge "Ogni giorno"; **Obiettivo e Sessione visibili
solo sul counter**; segmenti **Work/Rest** coi default e "Riposo" spuntato su Rest; il **gate F1/F2 della
review 2.5 funziona** — con l'obiettivo attivo e il target vuoto *Lancia* e *Salva come template* sono
disabilitati; l'anteprima **reagisce** (chip dei segmenti aggiornati, barra goal al 67% con target 3).
**Prova decisiva della composizione**: l'abitudine lanciata porta davvero i segmenti `Work | Prova` nel timer
di sessione, il campo Quantità con l'unità `g` e la card goal — il builder compone, non finge.

- **W2V-9 — I ToggleButton del builder non sono tematizzati: azzurro di sistema da accesi, illeggibili da
  spenti** (`verified`, screenshot + codice). Sintomo: "+ Obiettivo" e "+ Sessione" hanno fondo **chiaro** a
  riposo con testo chiaro sopra (sembrano disabilitati) e diventano **azzurro Windows** quando attivi — un
  colore che non esiste nella palette dell'app, in un dialog interamente dark. Causa: in
  `HabitBuilderView.xaml:596` e `:626` i due `ToggleButton` sono nudi (nessuno `Style`), e lo stile implicito
  in `BaseStyles.xaml:779-781` imposta **solo il `Foreground`**: il `ControlTemplate` resta quello Aero di
  sistema, quindi il `Brush.Text` chiaro finisce su un fondo chiaro. Stesso difetto sui **day-toggle del
  Programma** (`HabitBuilderView.xaml:543-545`, stessa forma nuda). Fix a portata: `Recordum.PillToggle`
  esiste già (`BaseStyles.xaml:785`) ed è **keyed apposta** per non essere implicita — basta applicarla ai tre
  siti. Nota: lo stile implicito che tocca il solo `Foreground` è una **trappola di famiglia** — dà
  l'impressione che il controllo sia tematizzato mentre ne peggiora il contrasto.
- **W2V-10 — Con Lancia disabilitato, l'hint elenca tutte le condizioni invece di quella che manca**
  (`verified`, screenshot). Il testo in fondo al dialog recita per intero *"Dai un nome, scegli una scala per
  ogni campo Scala e un'unità per ogni campo Tempo, completa il programma (i giorni, o quante volte) — e un
  target positivo sull'obiettivo, se attivo — per lanciare"*, anche quando l'unico requisito mancante è il
  target del goal. Il gate è corretto (è il fix F1/F2 della review 2.5): è la **diagnosi** a non essere
  specifica. Minore, ma è la stessa famiglia UX di F0-8 (bottone disabilitato che non dice perché).

### Segmento 2026-08-16 — unità staged nel builder

**Confermati a schermo** (checklist §6): il micro-form "Nuova unità…" si apre **inline sotto la lista campi**
e confermando l'unità è già selezionata sul campo; un **secondo** campo Quantità la **riusa** dal proprio
picker; **annullare** il builder non lascia nulla nel catalogo (staging). La spunta **"Decimali"** compare
correttamente su un campo Quantità **senza** unità. Visibile in foto anche **W2V-9** (i due ToggleButton
"+ Obiettivo"/"+ Sessione" chiari e illeggibili) — già a tracker, non ri-segnalato.

**Non è un difetto — chiarito**: dopo *Salva come template* l'unità **non** è nel catalogo alla riapertura del
builder. È il progetto: il payload la porta come definizione incorporata e la crea `InstantiateAsync`
(risolvi-o-crea). **Resta da verificare a schermo** la seconda metà del round-trip: istanziare il template
(l'unità appare) e istanziarlo una **seconda** volta (non deve nascere `pagina-2`) — il guard è
`HabitTemplateService.cs:161`, `unitsByCode.ContainsKey(u.Code) → continue` case-insensitive.

- **W2V-11 — Unità e scale si possono solo aggiungere: nessun editor, nessuna cancellazione** (`verified`,
  autore + codice). `IReferenceDataQueryService` espone **solo** `ListUnitsAsync`/`ListScalesAsync` — non
  esiste update né delete in nessun punto dell'app. L'unico creatore è il builder
  (`HabitBuilderViewModel.ConfirmCustomUnit/Scale`, staged) più `HabitTemplateService` all'istanziazione;
  Configura → Metriche le sa solo **scegliere** da un picker (`MetricsSectionViewModel`), e `Settings` ha la
  sola `SettingsWindow`. Conseguenze concrete: un refuso nel nome è **permanente**; il catalogo cresce in modo
  monotono; e poiché `ReferenceCodeSlug.From` deduplica col suffisso, ricreare "Pagina" dopo averla già
  lanciata produce un secondo `pagina-2` con lo **stesso nome visibile**, indistinguibile nel picker e
  incancellabile. Aggravante da dogfooding (E21): è la fase in cui si sperimenta di più con le composizioni.
  Nota di scope: un editor va progettato con la regola di cosa succede a un'unità **in uso** (bloccare
  l'eliminazione, o staccarla dalle metriche) — non è un CRUD banale. Priorità autore: da concordare nel giro
  fix.
- **W2V-12 — Il "Simbolo" di un'unità non compare da nessuna parte: campo scritto e mai letto** (`verified`,
  autore + codice). Sintomo: il micro-form chiede "Simbolo (opzionale)" e non si capisce dove finisca. Causa
  verificata per esclusione: `UnitOfMeasure.Symbol` è persistito (`UnitOfMeasureConfiguration.cs:15`,
  maxLength 16), scritto dal builder (`HabitBuilderViewModel.cs:155`), incorporato nel payload template
  (`:568`) e riletto all'istanziazione (`HabitTemplateService.cs:168`) — ma **nessun binding e nessuna lettura
  in UI**: l'unico `Symbol` in tutto lo strato di presentazione è il `TextBox` che lo scrive
  (`HabitBuilderView.xaml:463`). Tutte le superfici che mostrano l'unità usano la **Code**:
  `SeriesCellViewModel.cs:116` (`UnitLabel = unitCode`), `CompletionListViewModel.cs:154` (`" " + m.Unit`), e
  gli aggregati riga Home / fascia Oggi. Il difetto è invisibile sui seed perché le unità ufficiali hanno
  `Code == Symbol` (`l`, `g`, `km`) con il nome esteso a parte ("Litre"); per un'unità utente la Code è lo
  **slug del nome**, quindi "Pagina" + simbolo "pg" si legge a schermo `2 pagina` — mai `2 pg`. Due strade,
  decisione dell'autore: **(a)** togliere il campo (l'etichetta a schermo resta lo slug del nome, e allora
  serve almeno poterlo correggere → W2V-11); **(b)** rendere il simbolo **l'etichetta di visualizzazione**
  (`display = Symbol ?? Code`), che è ciò che il campo implicitamente promette — con un'anteprima viva accanto
  al campo ("es. `2 pg`") che risponda da sola alla domanda "dove compare". Il TextBox libero **non** è il
  problema (i simboli reali sono testo: `pg`, `tz`, `kcal`): non serve un picker di glifi.
- **W2V-13 — Due unità con lo stesso nome nel picker, e a schermo si legge lo slug** (`verified`, screenshot +
  codice). Osservato: dopo aver creato "Facciate" due volte il picker Quantità elenca **due voci identiche
  "Facciate"** e l'abitudine lanciata mostra `0 facciate-2` nella fascia Oggi e `facciate-2` accanto al campo.
  È W2V-11 + W2V-12 che si materializzano insieme, e mostra l'inversione esatta: il picker usa
  `DisplayMemberPath="Name"` (`HabitBuilderView.xaml:302`) → i duplicati sono **indistinguibili nel punto in
  cui scegli**; tutto il resto dell'app usa la `Code` → si legge lo **slug che l'utente non ha mai digitato**,
  suffisso di deduplica incluso. Il suffisso arriva da `ReferenceCodeSlug.From` (dedup `-2`, `-3`… contro i
  codici già presi), che è corretto come chiave di join dei payload ma non è mai stato pensato per essere
  letto. Chiudere W2V-12 (display = simbolo, o comunque un'etichetta scelta) e W2V-11 (poter rinominare /
  eliminare) chiude anche questo; separatamente, il picker dovrebbe distinguere due voci omonime.
- **W2V-14 — I mattoni illegali per la composizione corrente non sono disabilitati: il click è un no-op muto**
  (`verified`, autore + codice + spec). Con un campo **Testo libero** nella composizione, "+ Quantità" e
  "+ Scala" restano di aspetto normale e cliccabili, ma non fanno **nulla**: `AddField` esce subito
  (`HabitBuilderViewModel.cs:288`, `if (!CanAddField(nature)) return;`) perché
  `HabitCompositionRules.CanAddField` nega Quantity/Scale quando c'è un Text. In XAML la palette ha un solo
  `IsEnabled="{Binding FieldsAllowed}"` sull'intero `WrapPanel` (`HabitBuilderView.xaml:219`), che copre **solo**
  il caso contatore-a-tap: non esiste alcun gating per-bottone. **Non è una richiesta nuova**: la spec del
  builder §4.4 lo prescriveva già — *"Mattoni illegali per il tipo corrente: disabilitati in palette con
  tooltip che spiega"* (`2026-06-02-modular-habit-builder-design.md:69`) — ed è rimasta non implementata. Il
  VM espone già `CanAddField(FieldNature)`: serve la superficie (4 proprietà calcolate, o un converter), non
  la regola.
- **W2V-15 — Il builder compone un journal con una metrica durata che nessuna superficie sa registrare**
  (`verified`, codice). La grammatica permette **Testo libero + Tempo** (`CanAddField`: Text ammesso solo se
  tutti i campi esistenti sono Time; Time ammesso con Text se non ce n'è già uno) — è il **"rilassamento
  journal+tempo"** deciso in brainstorming e documentato in `2026-06-02-modular-habit-builder-design.md:64`
  ("Scrittura cronometrata"). Ma la registrazione di un journal è `JournalQuickAddViewModel`, che salva
  **solo il testo della nota** via `IDiaryService` e non ha alcun campo metrica (né il composer nota veloce,
  né l'editor Markdown ne hanno). Risultato: una composizione lecita produce un `MetricDefinition` di tipo
  `Seconds` **invisibile per sempre** — esattamente le "metriche fantasma" che l'Onda 2.4 ha tolto dal seed
  Food diary con la migration `FoodDiaryQuickNoteV217`, e che un test-guardia impedisce ai template ufficiali.
  Il guard di parità (E16) non lo intercetta: confronta template↔builder, non builder↔superficie di
  registrazione. Tre strade: (a) togliere il rilassamento dalla grammatica (il journal cronometrato si compone
  come counter con un campo testo… che però la grammatica manda in Journal — quindi la strada implica
  ripensare la regola #1); (b) dare al journal una superficie per la durata (cronometro nella colonna Diario);
  (c) tenere la regola e accettare la metrica invisibile — **da escludere**, è il difetto appena migrato via.
  → **decisione di modello**, famiglia CM-*.
- **W2V-16 — "Ripetibile" non si chiama ripetibile e compare prima dei mattoni che lo abilitano**
  (`verified`, autore + codice). L'autore ha chiesto *"dove dovrei spuntare ripetibile?"* con la spunta a
  schermo: si chiama **"Si può registrare più volte al giorno"** (`Strings.Editor_Registration_Repeatable`) —
  copy corretta in sé, ma la checklist, la spec e la conversazione dicono "ripetibile", e il termine non
  compare da nessuna parte. Aggravante di posizione: il blocco è visibile solo quando `DerivedType == Counter`
  (`HabitBuilderView.xaml:142`) e sta **sopra** la sezione "Mattoni", cioè appare all'improvviso più in alto
  di dove stai lavorando nel momento in cui aggiungi il primo campo. Le serie nominate stanno annidate sotto,
  visibili solo a spunta attiva: chi non trova la spunta non scopre mai i chip.
- **W2V-17 — La spunta "Decimali" insegna il modello sbagliato, e l'input numerico non dice le sue regole**
  (`verified`, autore + codice). Tre cose distinte osservate insieme. (a) La spunta **sparisce** quando scegli
  un'unità e l'autore ne ha dedotto *"andrebbe spuntata prima"*: in realtà un'unità **implica** i decimali
  (`FieldBlockViewModel.cs:39` — `Quantity` → `Decimal` se c'è unità **o** se la spunta è attiva), quindi
  spuntarla prima è inutile. La UI comunica una perdita di controllo dove c'è un automatismo. (b)
  **L'anteprima non riflette la scelta**: `SamplePreviewFactory` campiona sempre `SampleValue = 2m` formattato
  `"0.##"`, quindi un campo decimale mostra `2` identico a un intero — l'unica manopola che cambia il tipo di
  dato non ha riscontro nel piano che serve a mostrarti cosa otterrai. (c) **Separatore e valori non numerici**
  (domande dell'autore, risposte verificate): le celle numeriche sono `TextBox` con binding nudo
  (`HabitScreen.xaml:275/304/312`, nessun `ConverterCulture`, nessuna `ValidationRule`, nessun filtro di
  input), quindi la conversione stringa→`decimal` usa la cultura del `FrameworkElement`, che WPF lascia a
  **en-US**. È la **stessa radice di W2V-1**, qui però non è cosmetica.
  → **CORREZIONE (2026-08-17), misurata scrivendo il test invece che dedotta dal codice.** La prima
  descrizione — "la virgola è rifiutata in silenzio, i valori alfabetici pure" — era **sbagliata in
  entrambe le metà**, e la verità è peggiore su quella che conta:
  - **`abc` WPF lo segnala già**: la conversione fallisce, il binding va in `Validation.HasError`, la
    sorgente conserva il valore precedente. Non era silenzioso (mancava solo una resa decente dell'errore,
    perché il template di default è un riquadro rosso Aero fuori famiglia).
  - **`2,5` non veniva rifiutato: diventava `25`.** Il convertitore del binding accetta il separatore delle
    **migliaia**, quindi la virgola in en-US non è un errore, è un raggruppamento. Un valore **sbagliato di
    dieci volte, scritto senza una parola** — e il fix di W2V-1 da solo avrebbe **ribaltato la trappola sul
    punto** (`2.5` → 25 in italiano), cioè l'avrebbe spostata addosso all'abitudine di digitazione più
    probabile. Caratterizzato in `NumericInputRuleTests.Wpf_alone_flags_unreadable_text_but_not_a_misread_number`.
  → **`fixed` 2026-08-17** (la parte (c); (a) e (b) restano aperte): nuova `NumericInputRule`
  (`ValidationStep.RawProposedValue`, cioè **prima** della conversione, l'unico punto in cui il testo grezzo
  esiste ancora) che accetta solo un numero semplice nella cultura corrente — segno, cifre, al massimo un
  separatore decimale, **nessun raggruppamento**; vuoto è valido, perché svuotare un campo non è un errore.
  Applicata ai tre campi di registrazione **e** ai due target di obiettivo (builder + Configura): sono i
  valori che si rileggono come dato. Restano senza regola i campi interi di configurazione (giorni,
  conteggi, tolleranza, timer) — stesso difetto di classe, blast radius minore, **follow-up**. La resa: lo
  stile condiviso `TextBox` annulla il riquadro rosso di default e porta l'errore sulla **sottolineatura**
  (`Brush.Danger`) con la ragione nel ToolTip, dopo il trigger del focus così un valore rifiutato resta
  visibile mentre lo correggi.
- **W2V-18 — Contatore e Astinenza sono checkbox in mezzo a una palette di mattoni-bottone** (`verified`,
  autore + codice). Obiezione di grammatica visiva dell'autore: *"perché non sono mattoni anche loro?"*.
  Nel modello **lo sono** — la spec li chiama mattoni-base (`2026-06-12-multivalue-habits-revision-design.md`
  §3.1: "Astinenza — *nuovo* mattone-base esclusivo"), e `HabitCompositionRules` li tratta alla pari dei campi
  nella risoluzione del tipo. È solo la resa a dissentire: quattro `Button` "+ …" per i campi, due `CheckBox`
  per i due mattoni esclusivi (`HabitBuilderView.xaml:241-252`). Sono davvero diversi — sono esclusivi e
  senza sotto-campi, non aggiungono una riga alla lista — ma la differenza è *modale*, non *categoriale*, e
  presentarla come un'altra specie di controllo suggerisce che siano opzioni di contorno invece che due dei
  cinque modi di far nascere un tipo. Nota: la spec del builder §5 immaginava una palette unica di
  "`+ mattone ▾`". Rifinitura UX, non un difetto funzionale — ma tocca la leggibilità del pezzo centrale.
- **W2V-19 — L'anteprima "In Home" è piatta: le strisce che codificano una grandezza non la mostrano**
  (`verified`, autore + codice). Osservato: l'anteprima non sembra cambiare con il tipo. Gli stili **sono**
  per-tipo (`HomeRowPresentation.ResolveStripStyle`: Counter→`Bars`, Resistance→`Band`, Journal→`Dots`,
  State→`Ramp`, resto→`Squares`), ma il campione li rende indistinguibili: `SamplePreviewFactory` usa
  `DayPattern = [1,0,1,1,0,1,1]` (**binario**) e poi calcola `fractions = c == 0 ? 0 : 0.4 + 0.2 * (c % 3)` —
  con `c ∈ {0,1}` il risultato è **sempre 0.6**. Quindi in anteprima le barre del counter hanno tutte la
  **stessa altezza** e la ramp dello state ha una **tinta unica**, cioè le due strisce che esistono apposta per
  mostrare *quanto*, in anteprima mostrano solo *se*: a colpo d'occhio sono la stessa cosa dei quadretti
  action. In Home vera la differenza si vede (le barre di Water intake sono di altezze diverse), quindi
  l'anteprima è **meno fedele della cosa che anticipa**. Fix piccolo e circoscritto: un campione di valori
  **non binario** (il pattern dei giorni resta, cambiano le magnitudini), verificando che `Fractions` copra
  l'intervallo utile.
- **W2V-20 — "Gesto primario" è gergo di progetto esposto all'utente** (`verified`, autore + codice).
  L'etichetta della manopola journal è letteralmente `Gesto primario`
  (`Strings.it.resx:1399`), con le opzioni "Scrivi la voce di oggi" / "Nota veloce con foto". Le **opzioni**
  sono chiare; è l'**etichetta** a nominare il concetto interno invece di porre la domanda ("Aprendo il
  diario, cosa vuoi fare?"). Compare identica nel builder e in Configura → Registrazione. Copy, non
  comportamento — ma è la prima cosa che si legge di U7.

## SEC-1 — La cifratura del database era spenta (trovato 2026-09-08, `fixed`)

- **Stato**: `fixed` (2026-09-08). Trovato di rimbalzo mentre eseguivo R9: 14 test rossi (12 Data +
  2 WPF) che, guardati uno per uno, erano **tutti e soli** quelli che toccano la cifratura.
- **Sintomo**: `SqlCipherConnectionFactoryTests.Open_with_wrong_key_throws_on_first_read` non
  lanciava più; `BootstrapSmokeTests` idem; ogni test di backup moriva su
  `SQLite Error 1: 'no such function: sqlcipher_export'`.
- **Causa**: `Recordum.Data` referenziava `Microsoft.EntityFrameworkCore.Sqlite` — il pacchetto
  **completo**, non `.Sqlite.Core` — che si porta dietro `SQLitePCLRaw.bundle_e_sqlite3`.
  Quel bundle e `SQLitePCLRaw.bundle_e_sqlcipher` spediscono **entrambi** un assembly che si chiama
  `SQLitePCLRaw.batteries_v2.dll`: nella cartella di output ne sopravvive uno solo. Quando vince
  quello in chiaro, `Batteries_V2.Init()` registra il provider `e_sqlite3`, **`PRAGMA key` diventa
  un no-op silenzioso** e il database viene scritto **non cifrato** — senza errore, senza warning.
- **Perché è comparsa da sola**: nessun commit l'ha rotta. `project.assets.json` risolveva
  `bundle_e_sqlcipher/2.1.11` (il nostro riferimento esplicito, `2.1.*`) e
  `bundle_e_sqlite3/2.1.12` (transitivo). Finché il bundle in chiaro stava sotto la 2.1.11 poteva
  vincere il cipher; uscita la 2.1.12 l'ha scavalcato. È cambiato **il mondo esterno sotto un
  wildcard di versione**, ed è per questo che CLAUDE.md registra la suite Data tutta verde
  all'11 agosto e questa sessione l'ha trovata rossa.
- **Fix**: `Microsoft.EntityFrameworkCore.Sqlite.Core` al posto di `.Sqlite`, in
  `src/Recordum.Data` **e** in `tests/Recordum.Data.Tests` (stesso identico difetto nel progetto di
  test). Dopo il fix `bundle_e_sqlite3` sparisce dagli assets e nell'output resta solo
  `e_sqlcipher.dll`. Suite: **Data 482 / WPF 1022 / Core 143 / Plugins 66, zero rosse.**
- **Guard**: `SqlCipherProviderGuardTests.The_active_sqlite_provider_is_sqlcipher_not_plain_sqlite`
  — `PRAGMA cipher_version` deve tornare una versione. Gli altri test di cifratura *rilevavano* già
  il problema, ma il loro rosso si legge come "problema d'ambiente" e si archivia: questo lo nomina
  e dice cosa controllare. Provato rosso rimettendo `.Sqlite` nel csproj.
- **Lezioni**:
  1. **Un rosso che sembra ambiente va provato, non archiviato.** Qui bastava uno `git stash` per
     vedere che falliva anche su `HEAD` pulito — e da lì è venuto tutto il resto.
  2. **Due pacchetti che spediscono lo stesso nome di assembly sono un conflitto silenzioso**, e se
     uno dei due decide se i dati sono cifrati, il silenzio è il difetto peggiore possibile.
  3. **La prima versione del guard era sbagliata e passava lo stesso.** `SELECT sqlcipher_export IS
     NOT NULL` non chiama la funzione: un identificatore nudo è una **colonna**, quindi l'errore
     diceva `no such column` e l'assert (che cercava `no such function`) lo lasciava passare. Solo
     eseguendolo nella configurazione rotta si è visto. *Misurato, non dedotto* — la stessa lezione
     di W2V-17c.
- **Non fatto** (deliberato): **non** ho pinnato le versioni. Pinnare non chiude il meccanismo — un
  *nuovo* pacchetto transitivo potrebbe reintrodurre un bundle domani lo stesso. Il guard sì.


## Review di branch R9 (2026-09-09) — due Critical, un Important, tutti chiusi

Review sull'**intero diff** del branch (`4c63af7..8961005`, 136 file), non per-task: è la forma che
nell'ultimo anno ha trovato ciò che le review per-task non potevano vedere (C1/C2 dell'Onda 2.4, i sei
findings della 2.5). Il piano R9 aveva **predetto la forma** del difetto — *"una superficie dimenticata,
un sito marcato C in §0 che nessun task ha toccato"* — e la predizione era giusta due volte su tre.

**Chiusi in questo giro:**

- **R9-C1 — `StateTodayContent` riceveva il confine e non lo assegnava** (`fixed`). Il **quarto** caso
  della stessa famiglia dei tre trovati nel WIP: `_day` restava `DayBoundary.Utc` per sempre, quindi
  A54/A55/B15 erano convertiti solo in apparenza. La seconda conseguenza è **perdita di dati**:
  `RecordAsync` fa last-wins cancellando le completion nella finestra del giorno, e con la finestra in
  UTC alle 00:30 locali del 19 cancellava il valore registrato alle 20:00 del **18**. Passato perché
  `TodayStripDayBoundaryTests` copriva Action, Resistance, Counter e HomeRow — **non State**. Ora c'è
  anche il quinto test.
- **R9-C2 — `RangeOf` esplodeva su `DateOnly.MinValue`, e un chiamante glielo passa** (`fixed`).
  Regressione **introdotta da questo branch**: `AbstinencePeriodListViewModel` scrive
  `from ?? DateOnly.MinValue` per "tutte le registrazioni", e `StartOf(0001-01-01)` in un fuso a offset
  **positivo** costruisce un istante che in UTC cade nell'anno 0 → `ArgumentOutOfRangeException`. Su
  un'abitudine Resistance bastava scegliere Registrazioni → periodo "Tutto". Invisibile ai test perché
  l'unico che passa `MinValue` gira su `DbFixture`, il cui `FakeClock` ha fuso **UTC** (offset zero).
  `MinValue` è ora sentinella simmetrica a `MaxValue`; il test lo prova in un fuso Rome-like, non in UTC.
- **R9-I1 — la Panoramica di un gruppo riceveva il confine del soggetto precedente** (`fixed`). Il
  `_dayBoundary` si risolveva **dopo** il ramo `if (node.NodeType == NodeType.Group)`: al primo
  caricamento il gruppo prendeva `DayBoundary.Utc`, dopo un'abitudine con reset 04:00 prendeva quello
  dell'abitudine — stato di un soggetto che sanguina su un altro. Risoluzione spostata sopra ogni
  pannello; `PanoramicaViewModel.LastLoadedBoundary` rende la cosa **osservabile**, perché il campo
  dello screen a fine caricamento è corretto comunque ed è ciò che rendeva il difetto invisibile.
- **R9-M4 — bound esclusivo passato a un filtro inclusivo** (`fixed`). `ActionTodayContent` e
  `StateTodayContent` passavano `RangeOf(...).ToExclusive` a `ListByHabitAsync`, che filtra
  `CompletedAtMs <= tMs`: un millisecondo di sovrapposizione col giorno dopo. Difetto preesistente
  (`LocalDayWindow` faceva lo stesso), ma la stessa domanda aveva **due risposte nello stesso branch**.
- **R9-M5 — `ThrowIfNull(resetTimes)` mancante** dove tutti gli altri parametri erano guardati (`fixed`).

**Aperti, deliberatamente — nessuno blocca il merge:**

- **R9-M1 — il guard anti-regressione è a granularità di file** (`reported`).
  `DayDerivationGuardTests` salta l'**intero file** whitelistato: una derivazione nuova dentro
  `QuickAddViewModelBase.cs` non verrebbe mai vista. Inoltre il pattern è `UtcNow.UtcDateTime.Date`,
  mentre §0 contava tre siti scritti `.UtcDateTime.Date` **senza** `UtcNow` davanti: quella forma oggi
  non esiste più, ma il guard non la riprenderebbe. Fix: whitelist per `file:pattern` (o marcatore di
  riga) e secondo pattern allargato.
- **R9-M2 — `RetroactiveNoonBackfill`: N+1, flag non atomico, idempotenza con un'eccezione geografica**
  (`reported`). Una `FirstAsync` per ogni riga spostata dentro il loop, all'avvio su DB cifrato;
  `SaveChangesAsync` e la scrittura del flag non sono nella stessa transazione, quindi un crash fra le
  due fa rigirare il passo. E rigirando **non è idempotente ovunque**: `declared` si ricava dalla data
  UTC dell'istante, che dopo il primo giro è mezzogiorno locale — con offset in (−12, +12) la data UTC
  coincide e nulla si muove, ma a **UTC+13/+14** mezzogiorno locale cade nel giorno UTC precedente e
  ogni giro sposterebbe la riga indietro di un giorno. Non è un rischio per l'autore; la docstring però
  dice "idempotente" senza condizioni. O flag nella stessa transazione, o si nomina il limite.
- **R9-M3 — il range dei grafici usa il confine dell'app, i bucket quello dell'abitudine**
  (`reported`). Scelta dichiarata e difendibile (un chart può avere serie su abitudini diverse), ma la
  conseguenza non è scritta: con `reset_time` 04:00 il bucket agli estremi del range può risultare
  tagliato rispetto a ciò che la heatmap della stessa abitudine mostra.
- **R9-M6 — `CalendarNavigatorViewModel._today` è leggibile prima di `LoadAsync`** (`reported`).
  Il commento dice "le celle nascono solo dentro LoadAsync", ma `:298` (`_weekAnchor = SelectedDate ?? _today`)
  legge il campo dal cambio di vista: con `SelectedDate` null e nessun load, l'ancora finisce nell'anno 1.
  Nel flusso reale non capita; un `DateOnly?` renderebbe l'invariante **verificabile** invece che documentata.
- **R9-M7 — `HabitTreeQueryService` ha spostato un `GROUP BY` SQL in memoria** (`reported`). Inevitabile
  (il bucket dipende dall'abitudine e i giorni non durano 24 ore) e `HomeTreeBudgetTests` resta verde, ma
  ora il costo scala col **numero di registrazioni**, non di abitudini: va accanto al budget E20.
- **R9-M8 — `CompletionRowViewModel.Display` mostra ancora l'ora UTC** (`reported`). La modifica inline
  converte ora dal fuso dell'abitudine in entrambe le direzioni, ma `Display` no. Oggi invisibile perché
  quell'editor **non ha superficie XAML**; il giorno che ce l'avrà, riga ed editor mostreranno due orari
  diversi. Vale per tutte le righe della Cronologia, quindi è una decisione di presentazione, non un fix
  da infilare qui.

**Difetti del piano, non dell'esecuzione** (da correggere in §0 quando lo si rilegge): la tabella non
elenca `PanoramicaViewModel`/il goal di gruppo fra i siti — da cui I1 — e classifica B4 come semplice
"converti" senza notare che il suo chiamante passa `DateOnly.MinValue` come sentinella — da cui C2.
Entrambi sono buchi dell'**audit**: la tabella §0 è ora aggiornata con le due righe mancanti.


## Verifica sulla VM di R9 (2026-09-11) — suite WPF e sonde "rosso prima"

Il branch era stato scritto su Linux, dove la suite WPF non può girare: i test WPF di R9 **compilavano
ma non erano mai stati eseguiti**, né visti rossi. Sulla VM:

- **Suite complete**: Core 145 / Data 496 (DPAPI inclusi) / WPF 1044 (serializzata) / Plugins 66 /
  ExportEngine 37 / ChartEngine 18 — tutte verdi.
- **Un solo rosso al primo run, ed era il test**: `TodayStripDayBoundaryTests.State_reads_todays_value_through_the_reset_time_it_is_given`
  (quello aggiunto per R9-C1). La scala del `FakeReferenceData` non ha `MinValue`/`MaxValue` né
  etichette, quindi `StateTodayContent` risolveva un solo gradino (`Max = 0`) e il `Clamp` schiacciava a
  0 l'indice letto — il valore del 18 veniva trovato (`LastValueLabel` non null). Etichette esplicite
  sulla metrica (`23be772`), provato rosso senza `_day = day`.
- **Sonde "rosso prima"**: una rottura isolata per volta, test delle classi R9 rilanciati, file
  ripristinato con `git checkout`. Tutte rosse sul test atteso:

  | Sonda | Rottura | Rosso |
  |---|---|---|
  | Task 8 (piano) | `ResistanceTodayContent`: oggi in UTC | `Resistance_today_uses_the_same_today_as_its_siblings` |
  | Task 9 (piano) | `BuildAbstinenceMarkersAsync`: oggi in UTC | `Abstinence_markers_use_the_same_today_as_the_calendar` |
  | Task 10 (piano) | retroattiva a mezzanotte UTC | i tre test retroattivi, casi DST compresi |
  | I1 | ramo gruppo sopra la risoluzione del confine | `A_group_gets_its_own_boundary_…` (e solo lui) |
  | C1 | `StateTodayContent` senza `_day = day` | `State_reads_todays_value_…` |
  | WIP | `TrendChartViewModel` / `NotesViewModel` / `AbstinenceQuickAddViewModel` senza `_day = day` | i rispettivi tre test |
  | — | `ActionTodayContent` senza `_day = day`; `CounterTodayContent`, `HomeRowViewModel`, `GoalProgressCardViewModel` con oggi in UTC | i rispettivi quattro test |

- **Due sonde restavano verdi, ed era copertura mancante** (non difetti del codice):
  - `DetailViewModel` senza `_day = day`: il campo serve **solo ai reload scatenati dal bus**
    (`CompletionRecorded/Updated/Deleted`, note del journal). Il primo caricamento passa il confine per
    parametro e resta giusto, ma il reload dopo una registrazione tornava in UTC senza che nessun test lo
    vedesse. Chiuso con `Detail_reload_after_a_completion_event_keeps_the_boundary` (un contatore di chiamate
    su `FakeTrendQueryService` impedisce che passi a vuoto rileggendo la finestra del primo load).
  - `CompletionRowViewModel` che ignora il confine: serve **solo alla modifica inline dell'orario**, senza
    superficie XAML (R9-M8). Chiuso con `Inline_edit_reads_and_writes_wall_time_in_the_habit_zone`
    (lettura come orario di parete + round-trip del salvataggio).
  Entrambi provati rossi contro la rottura corrispondente.
- **Gotcha del filtro**: `QuickAddViewModelTests.cs` contiene la classe `QuickAddViewModelBaseTests`. Un
  `--filter FullyQualifiedName~QuickAddViewModelTests` **non** la seleziona: la prima sonda del Task 10
  sembrava verde perché i suoi test non giravano. Filtrare per nome di classe, e controllare sempre il
  totale eseguito.
- **Non fatto qui**: la verifica a schermo (Windows, con l'autore) e il Task 11 Step 7 — il backfill sul
  DB vero, che vuole prima la disciplina dati di E21 (backup, prova su copia).

## Onda F0 — review finale di branch (2026-09-20): residui e scoperte (`F0-*`)

Estrazione di `Recordum.Presentation` (piano `2026-09-10-wave-f0-presentation-extraction.md`). La review
finale (opus, `bb6c645..HEAD`) ha dato "With fixes", 0 Critical; il fix wave ha chiuso I1/M1/M2/M3/M7.
Restano questi, ciascuno con la sua ragione.

- **F0-Q1 — `QuitCommand` non ha consumatori e ha una dipendenza obbligatoria** (`reported`,
  **decisione dell'autore**). `MainWindowViewModel.QuitCommand` non è legato a nessun XAML né code-behind;
  la stringa `Settings.Quit` esiste in entrambi i `.resx` senza binding; ed è l'**unico** consumatore di
  `IApplicationLifetime`, che F0 ha reso `ThrowIfNull` nel costruttore. Stessa forma di A9/R8: o è
  un'**affordance persa** (cablare una voce "Esci") o è **scaffolding morto** (cancellare comando + stringa +
  iniezione). Non lasciarlo com'è: il seam esiste per un comando che nessuno può invocare.
- **F0-M4 — la guardia di C1 copre solo la metà lettura di `StateTodayContent`** (`reported`, ereditato da
  R9). `RecordAsync` (`StateTodayContent.cs:129-140`) decide con `_day.RangeOf(today)` quali registrazioni
  cancellare (last-wins): era la metà che perdeva dati. `StateTodayContentTests` passa `DayBoundary.Utc`
  a ogni chiamata, quindi nulla fallisce se la finestra torna alla clock. Test a basso costo: completion
  alle 20:00 di D−1 con confine `+02:00 / 04:00`, `RecordAsync` alle 00:30 di D, asserire che la riga D−1
  sopravviva.
- **F0-M5 — `ChartBuilderViewModel` possiede un `ITickSource` e non è `IDisposable`** (`reported`).
  Limitato in pratica (debounce a un colpo, ≤300 ms, fermato da `OnPreviewTick`), ma in quella finestra il
  VM è tenuto in vita dal dispatcher e può rendere un'anteprima in un VM scartato.
- **F0-M6 — nomi di file/tipo poco scopribili** (`reported`, estetica): `MarkerTint` vive in
  `Core/Abstractions/IApplicationLifetime.cs`; `ViewModels/DropPlan.cs` contiene `DropZone` + `DropPlan` +
  `DropResolver`.
- **F0-T1 — la fixture `FakeReferenceData` ha una scala "Mood (1-5)" con Min/Max a 0** (`reported`,
  preesistente): un nome che mente sul proprio intervallo. Ha reso *impossibile da passare* la guardia di C1
  finché non l'ha corretta il test locale; il prossimo autore di un test State/scala ricade nella stessa
  trappola. Una riga nella fixture (o un commento "nessun range di proposito") la chiude.
- **F0-T2 — i test di ViewModel Settings restano in `Recordum.WPF.Tests`** (`reported`, informativo):
  `GeneralSettingsViewModelTests`, `GeneralSettingsCultureTests`, `SettingsWindowViewModelTests` dipendono da
  `CultureService`/`ThemeService`/`AppFontService`, che vivono in `Recordum.WPF.Theming`. Sono servizi che
  un'onda successiva vorrà portabili.
- **F0-Y1 — heatmap-anno riempita per righe** (`reported`, **pre-esistente in WPF**, probabile causa di A5).
  `YearHeatmapViewModel` emette `anchor.AddDays(i)` per i=0..370 consecutivi (anchor = lunedì) e
  `UniformGrid Rows=7 Columns=53` li dispone per **riga**: un anno reale si legge come 7 righe di giorni
  successivi, non come colonne-settimana stile GitHub. Il test `Year_heatmap_grid_is_7_rows_by_53_columns_and_fills_ROW_MAJOR`
  lo documenta, così correggerlo (emissione column-major nel VM, o pannello diverso) farà scattare un test
  *consapevolmente*. Da fare nel VM, non in un porting.
- **F0-Y2 — il re-tint live del calendario al cambio tema non c'è** (`fixed` nel calendario Avalonia di F2a, test `The_calendar_retints_on_a_theme_switch`; `verified-fixed` dopo lo sguardo a schermo dell'autore; era `reported`, Onda 3). Il
  `HabitColorTintConverter` legge `Brush.Danger` imperativamente dentro un `MultiBinding` le cui tre sorgenti
  (`TintColorHex`, `TintStep`, `Tint`) non cambiano a un cambio di tema, e `App.ApplyTheme` invalida solo i
  `DynamicResource`. Non è una regressione (prima era uguale); serve una sottoscrizione a
  `IThemeService.EffectiveThemeChanged` che forzi un reload. Un commento di F0 prometteva il contrario
  ed è stato corretto.
- **F0-F1 — `FontWeight.SemiBold` risolve sul Bold** (`reported`, informativo). Source Serif 4 è spedito in
  Regular/Italic/Bold/BoldItalic; il Semibold statico ha due nomi di famiglia nel file ed è stato lasciato
  fuori. I titoli dell'editor (`MarkdownLivePreviewColorizer.cs:54`) escono Bold, non semi. Nessun test per
  lo stack di ripiego; `Font.System` resta sans di sistema (l'aspetto non è ancora identico ovunque per il
  testo sans).
- **F0-D1 — debito verso la VM** (`verified-fixed` 2026-09-25: la CI su Windows, run `36179927020`, ha eseguito la suite WPF dopo la rinomina — verde; il primo run aveva trovato solo due difetti di test, corretti in `f27d3a7`). *Storia, aggiornata il 2026-09-25*: la metà R9 è chiusa — la suite
  WPF di R9 è verde sulla VM dall'11/09 (§"Verifica sulla VM di R9"), e i tre commit relativi sono stati riportati su questo
  branch (i due test nuovi ora in `Recordum.Presentation.Tests`, la fixture State tenuta nella versione di F0). Resta la metà
  F0: `Recordum.WPF.Tests` (38 file) dopo la rinomina non è mai stato eseguito. Tre costrutti F0 falliscono solo a runtime su Windows: `IApplicationLifetime`
  obbligatorio in `MainWindowViewModel` (registrato a `ServiceRegistrar.cs:233`, gate `BootstrapSmokeTests`),
  la registrazione forwarder di `IEditorAvailability` e di `ITickSourceFactory`
  (`ServiceRegistrarTickSourceTests` asserisce il tipo concreto), e `SubscribeOnUi` su
  `SynchronizationContext`. Comando in `docs/dev-environment-sync.md` §"Handoff per la VM — onda F0".
  Se il primo dei due giri è rosso il colpevole è R9, se lo è solo il secondo è il porting.
  Se anche il rosso è di R9, il fix dovrà atterrare attraverso la rinomina di 126 file (rischio accettato
  dall'autore scavalcando il prerequisito del Task 7).
- **F0-T3 — nessun test asserisce che `HomeClockTicker.Register` riavvii la sorgente dopo `Clear()`**
  (`reported`, trovato dalla mutazione del re-reviewer). Sostituire lo `Stop()` di `Clear()` con un
  `Dispose()` non fa fallire nessun test (10/10 verdi), ma la sorgente disposta ignora ogni `Start()`
  successivo: l'orologio della Home morirebbe dopo il primo reload. Non è il leak che `TickingClockTests`
  sorveglia — è il suo opposto — e si chiude con un test "Register dopo Clear ridà `RunningCount == 1`".

## Onda F1 — Home su Avalonia (`F1-*`)

> Trovati eseguendo i Task 1-12 di `docs/superpowers/plans/2026-09-20-wave-f1-home.md` e controllati contro il codice a
> `6200ede`. Nessun Critical nelle review dei singoli task. Tutti `reported`: nessuno blocca il codice completo dell'onda,
> ma **F1-PERF1** va chiuso prima del dogfooding e **F1-SEC1** prima di rendere pubblico qualcosa.

- **F1-1 — `Icons.xaml` (WPF, congelato) disegna cinque icone storte** (`Dumbbell`, `Sun`, `ShieldCheck`,
  `CheckCircle`, `Pill`) per un errore di concatenazione dei path SVG: un `m` relativo diventato `M` assoluto senza
  convertire i lineto che seguono. WPF è congelato fino a F8; `Themes/Icons.axaml` le scrive correttamente, quindi da F1
  i due cataloghi non sono più identici su queste cinque chiavi (`IconCatalogTests` sorveglia la versione Avalonia). Un
  confronto fra i due cataloghi troverà queste differenze e non deve sorprendere. `reported`.
- **F1-2 — Il commento di `ApplicationInitializer.cs` (WPF) sulla verifica della chiave è falso.** Dice che la prima
  apertura "forces an error here if the key is wrong": `PRAGMA key` non verifica nulla, l'errore arriva alla prima
  lettura vera (`MigrateAsync`). `DbUnlockService` (Avalonia) verifica invece con una lettura esplicita. Da correggere
  se WPF viene toccato di nuovo, o quando F8 lo toglie. `reported`.
- **F1-3 — `DbUnlockService.UnlockAsync` ha un solo esito `Failed` per cause diverse.** Per esempio rifiuta di ricreare
  il salt se esiste già un database non vuoto (un altro avvio l'ha appena creato, o altro) e torna lo stesso
  `UnlockOutcome.Failed` col messaggio generico. Ogni guasto lascia una riga nel log (con eccezione dal Task 11; i rifiuti senza eccezione —
  chiave DPAPI che non legge il DB, creazione rifiutata, passphrase senza DB, nessuna chiave di piattaforma — dalla
  review finale di F1), quindi la causa si ricostruisce; la distinzione per l'utente vale una voce solo se il dogfooding la incontra. `reported`.
- **F1-5 — La riga gruppo offre "Rinomina" nel menu contestuale ma non ha una casella di rinomina.** Difetto già in WPF
  (`BeginRenameCommand` mette `IsRenaming = true` e non succede nulla di visibile), portato com'è per decisione D6 del
  piano: un difetto di WPF non si risolve dentro un porting. `GroupRow.axaml` ha la voce di menu e nessun editor. Si
  risolve fuori da F1: la casella in `GroupRow`, o la voce tolta. `reported`.
- **F1-DND1 — la Home su Avalonia non ha il fantasma del trascinamento** (rifinitura, non parità). Avalonia 12 non espone
  né `QueryContinueDrag` né un'API di immagine trascinata (verificato per reflection sul pacchetto 12.1.2), e gli
  eventi di movimento del puntatore non arrivano all'applicazione mentre il ciclo di trascinamento del sistema è
  attivo: la riga sorgente si limita a sfumare a 0,4 di opacità. `src/Recordum.WPF/Behaviors/DragGhost.cs` e
  `Interop/NativeMethods.cs` restano solo per WPF, congelato. Da riconsiderare come rifinitura. `reported`.
- **F1-8 — `AutomationProperties.Name` scritto in inglese sui tre pulsanti di didascalia di `MainWindow` (WPF)**
  (`"Minimize"` ecc., `MainWindow.xaml` e `WindowChromeStyles.xaml`). La versione Avalonia usa le stringhe
  `Window_Minimize/Maximize/Restore/Close` delle risorse. WPF resta com'è: è congelato. `reported`.
- **F1-9 — Captive dependency nel contenitore Avalonia**, la stessa forma già presente in WPF: i ViewModel transient
  (`HomeViewModel`, `MainWindowViewModel`, …) prendono servizi scoped dal contenitore radice, perché
  `AppComposition.BuildServices` — come `ServiceRegistrar` — non valida gli scope. Con `ValidateScopes=true` quei VM non
  si risolvono più. Conservato di proposito (commento in `AppComposition.cs`); da annotare, non da risolvere in F1. `reported`.
- **F1-10 — Icone di didascalia duplicate in WPF**: `MainWindow.xaml` ha le proprie `Caption.*` invece di riusare
  `Chrome.Caption*` di `WindowChromeStyles.xaml`. La versione Avalonia ne ha **una sola** copia (`Path.caption` in
  `MainWindow.axaml`): quando F6 porta le finestre figlie, quella copia va promossa a stile condiviso, non ricopiata. `reported`.
- **F1-ICON1 — Il glifo di "riduci a icona" non disegnava niente.** `MainWindow.axaml` lo disegnava con
  `Data="M5 12h14"` e `Stretch="Uniform"` (stile `Path.caption`): una geometria alta zero ha scala verticale 0 e sotto
  `Uniform` min(sx, sy) = 0, quindi 0 px. **Misurato** nella review finale di F1 (render Skia headless: 0 pixel di inchiostro
  per il minimizza, 88 per il massimizza); dipende dal layout, non dalla piattaforma, quindi su Windows il pulsante era vuoto.
  Corretto: `Stretch="None" Data="M0 5.5H11"` (11 px di inchiostro). Guardia: `MainWindowTests.Every_caption_glyph_draws_something_when_the_caption_buttons_are_ours`
  (provato rosso: larghezza resa 0). `fixed` (da verificare a schermo su Windows).
- **F1-PERF1 — La Home con molte righe è lenta: budget E9/E20 (< 500 ms).** Misurato in headless (Task 7, una sola
  misura per caso): 300 righe (20 gruppi da 10 figli + 80 abitudini) ≈ **3,8 s a caldo, ≈ 7 s a freddo**; con 4 righe
  318 ms (Task 12). Cause misurate: lo `StyleInclude` di `RowCardStyles` **per riga**, un `ContextMenu` da 10 voci
  costruito subito **per ogni riga**, nessuna virtualizzazione (~24 nodi visuali per riga); e ogni `LoadAsync` (dopo
  ogni registrazione o archiviazione) ricostruiva l'intero albero.
  **Fix** (branch `f2-perf1-home`, misure di `HomePerformanceTests`, `Category=Performance`, fuori dalla CI):
  1. `ff5e44f` — stili inclusi una volta in `App.axaml`; il menu di riga è una risorsa XAML agganciata alla prima
     `ContextRequested` (`RowMenu`). Caldo 1720 → 1261 ms.
  2. `28a98ee` — la Home è una **lista piatta** (`Rows`) in un `VirtualizingStackPanel`, non più l'albero
     `Roots`/`Children` (che resta per WPF). Virtualizzare l'albero annidato era stato provato e scartato: l'estensione
     stimata saltava da 42 900 a 3 800 px scorrendo, perché un gruppo aperto è alto quanto 11 righe. Le cornici dei
     gruppi aperti si disegnano a fette, riga per riga: il ViewModel dà a ogni riga le `GroupFrameSlice` che la
     attraversano, `GroupFrames` le disegna senza figli visuali, `RowCardMarginConverter` rientra la card con le misure
     del layout annidato (confronto delle schermate in chiaro e in scuro: nessun pixel che differisce di più di 8).
  Dopo: 300 righe **≈ 115 ms a caldo, ≈ 120 ms la ricarica**, 16 righe con una vista; estensione stabile entro il 15%
  scorrendo. Limiti noti: la misura è headless (senza il costo del rendering vero), e la ricarica ricrea ancora tutti i
  ViewModel di riga (oggi costa poco: si rimette mano solo se una misura lo chiede). `fixed`; diventa `verified-fixed`
  con la verifica a schermo su GNOME (scorrimento, cornici anche annidate, menu col tasto destro e Shift+F10,
  trascinamento); su Windows basta uno sguardo alla Home durante la checklist D7 di F1, per le linee da 1 px a scala
  125-150%.
- **F1-SEC1 — `recordum.db`, `-wal` e `-shm` nascono 0644 dentro la cartella dati 0700** (visto avviando la shell vera su
  Linux, Task 11; il comportamento è precedente). La cartella 0700 li protegge e il contenuto è cifrato, ma i file
  stessi non sono privati al proprietario come lo sono la cartella e il log. **Nella lista del
  `/security-review` prima di rendere pubblico qualcosa.** `reported`.
- **F1-RAIL1 — Pulsanti del rail e scorciatoie spenti finché non ha senso accenderli.** Sono `IsVisible="False"`
  (statico, con un commento nel XAML che nomina l'onda) per non mostrare comandi che non fanno niente: **Grafici** (F7;
  con il suo `Border` ospite), il separatore sopra **Backup**, fra i pulsanti dei workspace e le azioni, e **Backup** stesso (F2), **Esporta** (F6), **Diagnostica** e
  **Impostazioni** (F6, tutti e due puntano a `NullSettingsWindowLauncher`). Omesse anche le scorciatoie **Ctrl+4**
  (Grafici, F7) e **Ctrl+5** (Cronologia backup, F6) in `MainWindow.axaml`. Ogni onda riaccende le sue: toglie
  l'`IsVisible="False"`, riaggiunge la scorciatoia, e riprova il test dei pulsanti visibili del rail. Resta visibile
  solo Home. Spenti nella review finale di F1 anche (**F5**, selettore di template e builder: oggi `OpenNewHabitCommand`
  apre un `NullHabitTemplatePickerLauncher` e non fa niente): la voce **"＋ Nuovo → Abitudine"** di `HomeView.axaml` (il
  menu resta, con "Gruppo", che funziona) e il pulsante **"Crea la tua prima abitudine"** di `EmptyState.axaml`; F5 toglie
  l'`IsVisible="False"` e nell'`EmptyState` rimette il binding "solo su Attive". Guardie: `HomeViewTests.The_new_menu_hides_the_habit_item_…`
  e `EmptyStateTests.The_active_tab_shows_the_seedling_and_hides_the_call_to_action_until_f5` (da riaccendere con lo stesso
  codice). `reported`.
- **F1-LOG1 — Il log degli errori sta ora in `<cartella-dati>/logs`** (cartella 0700, file 0600 su Unix; Task 10-11) e
  ogni guasto di sblocco, di qualunque tipo, vi lascia una riga. Con `--data-dir` il log segue la cartella scelta e non
  scrive più nella cartella utente vera. Con la cartella predefinita WPF (congelata) e Avalonia scrivono lo **stesso**
  file, `%LocalAppData%/Recordum/logs/recordum.log` (su Linux `~/.local/share/Recordum/logs/recordum.log`): le righe delle due
  shell si mescolano e non dicono quale shell le ha scritte. "Quale shell ha girato" conta solo con `--data-dir`, dove
  Avalonia scrive nella cartella scelta e WPF no. `reported`.
- **F1-11 — decaduto (D7)**: nessun ripiego del chrome su Linux, nessuna maniglia fatta in casa (la barra di sistema resta).

## Onda F2a — Registrazione su Avalonia (`F2a-*`)

- **F2a-WPF1 — ✎ muto nelle registrazioni della Cronologia (WPF)** · `reported` (task 7) · In `HabitScreen.xaml` il
  pulsante ✎ chiama `BeginEditCommand`, che accende `IsEditing`, ma nessun template ha mai avuto un editor: non succede
  niente di visibile (stesso schema di F1-5). Su Avalonia il pulsante non c'è (decisione 9); l'editor arriva con F3.
  WPF è congelata: nessun fix lì. Nota collegata: la cancellazione di una registrazione ora chiede conferma anche in WPF
  (`CompletionListViewModel` riceve `IConfirmationDialog`) e l'ora in elenco è quella di parete dell'abitudine, non UTC.

- **F2a-1 — un gruppo non ha chip né colonne** · `reported` (Ruling R5) · In F2a la schermata di un gruppo mostra solo
  l'intestazione; la Panoramica del gruppo (chip, colonne per abitudine) arriva con F3. Non è una regressione.
- **F2a-2 — calendario: niente vista Anno né salto mese/anno** · `reported` (decisione del controller, ledger F2a, ruling R9: non è la R9 del reset) · Il brief chiede Mese/Settimana e
  precedente/successivo: per tornare indietro di molti mesi si clicca più volte. Vista Anno (mesi + heatmap) e menu di salto
  restano a F3.
- **F2a-3 — il cronometro di cella di una serie finisce nella barra dei timer** · `reported` (Ruling R12) ·
  `active_timer` non ha un marcatore d'origine (cella della serie o cronometro di sessione), quindi la barra mostra anche il
  cronometro di cella e il suo "ferma" registra una sessione di sola durata, cancellabile dalla Cronologia. È la questione
  di modello **CM-4**, rimandata al dogfooding. In F2a la cella si risincronizza sugli eventi dei timer e il suo stop
  tollera `ActiveTimerNotFound`.
- **F2a-4 — il purge cancella `active_timer` senza evento** · `reported` (preesistente) · La barra dei timer tiene una riga
  morta: il suo Stop lancia perché la riga non c'è più. Da risolvere pubblicando `TimerStopped` dal purge (Data).
- **F2a-5 — i catch di Presentation tracciano con `Debug.WriteLine`** · `reported` · Il codice è compilato via in Release,
  quindi un guasto in background (gestori di eventi `async void`, ricarichi) non lascia traccia nelle build spedite.
  Presentation non ha un logger; serve un'astrazione (come il sink di log di F1 in UI) prima della v1.0 pubblica.
- **F2a-6 — F0-Y2 `fixed` nel calendario Avalonia; W2V-4 e U4 ancora aperti** · F0-Y2 (il calendario non si ritingeva al
  cambio di tema) è `fixed` per costruzione: le celle leggono i pennelli come risorse dinamiche e il test
  `The_calendar_retints_on_a_theme_switch` legge il pixel in chiaro e in scuro (resta lo sguardo a schermo). **W2V-4**
  (il cerchietto del conteggio copre il numero del giorno) e **U4** (numero scentrato) **non** sono risolti: negli
  screenshot di F2a (`habit-counter-dark.png`, `habit-state-dark.png`) il cerchietto si sovrappone ancora al numero del
  giorno. Restano per F3.
- **F2a-7 — la rinomina dalla Home non aggiornava l'intestazione della schermata** · `fixed` (Ruling R4) ·
  `HomeRowViewModel.CommitRename` scriveva il nome senza pubblicare `HabitUpdated`, e `HabitScreenViewModel` ricarica il
  nodo solo su quell'evento (difetto preesistente, anche WPF). Ora `HabitNodeService.UpdateAsync` pubblica `HabitUpdated`
  dopo il commit, un solo punto per tutti i chiamanti; l'editor non lo pubblica più. Test:
  `RetroactivePathIntegrationTests.Renaming_from_the_home_updates_the_open_habit_screen_header` e
  `HabitNodeUpdateEventTests`.
- **F2a-8 — fascia astinenza: "1 tentativi (90g)" su un periodo appena iniziato** · `reported` · Visto in
  `habit-resistance-light.png` (screenshot di F2a, `HabitScreenScreenshotTests`): subito dopo `StartAsync` la fascia "Oggi"
  mostra "0:00:00 · Ricaduta · 1 tentativi (90g)": plurale sbagliato e il "(90g)" non ha senso con un periodo di zero secondi.
  Non corretto in F2a.
- **F2a-9 — il flip di Ripetibile non pubblica `HabitUpdated`** · `reported` (preesistente; F5 Configura) · Il flip passa da
  `IHabitConfigService.UpsertAsync`, che non pubblica nulla: il ramo "ricostruisci il quick-add al cambio di Ripetibile" di
  `HabitScreenViewModel` non è raggiungibile dall'editor.
- **F2a-10 — `ActiveTimerService.StopAsync` non è atomico** · `fixed` (`1ac8e1d`) · Cancellava la riga del timer e poi
  registrava il completamento: se `RecordAsync` falliva (per esempio uno stato senza valori, cioè il cronometro di cella
  fermato dalla barra) il tempo della sessione era perso. Ora registra prima e cancella dopo: se la registrazione fallisce,
  il timer resta. Limite rimasto (commento `ponytail:` nel codice): due transazioni, quindi se la cancellazione fallisse
  dopo una registrazione riuscita la sessione sarebbe registrata e il timer ancora vivo. Test
  `ActiveTimerServiceStopTests`.
- **F2a-11 — le eccezioni dei comandi async arrivano al `CrashLogger` con `Handled=false` e chiudono l'app** · `fixed`
  (`65b69ac`, scelta dell'autore 2026-10-13) · Su Avalonia `CrashLogger` gestisce le sole `RecordumDomainException`
  (timer non trovato, abitudine sparita, registrazione non valida per il tipo): log, un avviso con solo "OK"
  (`RecoverableErrorNotice`, uno alla volta), l'app continua. Un errore inatteso la chiude ancora, col log. La riga della
  barra e il cronometro della Cronologia, se il timer non c'è più, si ricaricano senza eccezione (caso del purge, F2a-4).
  WPF resta com'era (congelato). Test `CrashLoggerTests`, `RecoverableErrorNoticeTests`,
  `TimerStatusBarViewModelTests`, `TimerControlViewModelTests`.
- **F2a-12 — `TimerStatusBarViewModel` è transient, risolto dalla radice, e tiene un `IHabitNodeService` scoped** ·
  `reported` (preesistente; stessa nota della decisione 8 di F1) · Dipendenza catturata (captive dependency).
