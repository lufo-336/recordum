# Piano di ridefinizione Recordum → v1.0 pubblica

**Data**: 2026-07-03 · **Stato**: approvato dall'autore (brainstorm 2026-07-03)
**Ruolo di questo documento**: è la **mappa** del percorso verso la v1.0 — ordina il lavoro in onde, fissa gli exit criteria e il metodo. Non sostituisce il tracker (`docs/phase-4-issues.md`, lista vivente dei findings) né le spec di dettaglio: **ogni item di lavoro nasce dal proprio ciclo brainstorm → spec → plan**. Quando un'onda avanza, aggiornare la tabella di stato in fondo.

---

## 1. Obiettivo e vincoli (decisi dall'autore, 2026-07-03)

- **v1.0 = rilascio pubblico usabile da estranei.** Qualcuno scarica la release da GitHub e la usa senza l'autore accanto: servono packaging, onboarding al primo avvio, robustezza su macchine altrui, docs utente.
- **Comanda la qualità, non il calendario.** Nessuna scadenza: si rilascia quando la tabella exit criteria (§8) è tutta verde.
- **Linguaggio visivo attuale consolidato, non ridiscusso.** Palatino + flat scuro + chrome custom + famiglia popup con ombra: il lavoro estetico è portarlo *ovunque* con un audit sistematico, non reinventarlo.
- **Scope irrinunciabile v1.0** (tutto selezionato dall'autore): casi d'uso template rifiniti (U6/U7/U8/U10), chart/esplorazione dati funzionanti, packaging + primo avvio, foto/allegati nelle note, **builder "Crea nuova abitudine" con anteprima sensata e coerente col tipo** ("perché contatore dovrebbe essere un mero checkbox?").
- **Fuori scope v1.0** (invariato da CLAUDE.md §7): AI/correlazioni, audit accessibilità completo, web, mobile, sync (→ 1.x / fasi successive).
- **Principio "template = composizione salvata"** (autore, 2026-07-03): un template non è mai un'esperienza esclusiva fornita dal programma — è una composizione che l'utente deve poter ricostruire da solo (e salvare a sua volta come template) con builder + Configura. Se un template fa X, l'utente può fare X. Corollario del principio di progetto "complessità per composizione"; è il razionale di E16 e della matrice di parità (Onda 2.9) e vincola ogni template futuro. Da riportare in CLAUDE.md §2 al merge di questo piano.

## 1-bis. Revisione 2026-08-10 (approvata dall'autore)

Cinque decisioni prese col punto-della-situazione del 2026-08-10 (più il punto 6, aggiunto il 2026-08-11 dal brainstorm della 2.5). Modificano l'**ordine** del percorso, non lo scope delle onde.

1. **Onda 5 riconfermata intera.** Le nove feature restano in v1.0 (l'opzione di sfoltirla è stata considerata e respinta dall'autore). Il suo **ordine interno** però non è più quello scritto a tavolino a luglio: dopo ≥2 settimane di dogfooding (punto 2) l'autore riordina le feature in base a ciò che gli manca nell'uso reale. **Nessuna esecuzione 5.x parte prima di quel riordino.**

2. **Dogfooding = tappa formale (nuovo E21).** Subito dopo il pass visivo dell'Onda 2, l'autore migra le proprie abitudini reali in Recordum e lo usa ogni giorno, mentre 2.9/3/5 procedono in parallelo. I findings d'uso entrano nel tracker con prefisso `DF-*`, triati come gli altri. **Disciplina dati** (i dati sono veri, le build di sviluppo no — il data-wipe `4d01f9c` è il precedente):
   - il dogfooding gira **solo sulla build di `main`**, mai su branch di lavoro;
   - auto-backup attivo dal giorno uno + backup manuale `.recbak` **prima** di lanciare qualsiasi build nuova sul DB reale;
   - ogni migration nuova si prova su una **copia** del DB reale prima che sull'originale.

3. **E14 (motore notifiche minimo) anticipato.** Esce dall'Onda 4 e diventa un blocchetto autonomo subito dopo il pass visivo dell'Onda 2: è il prerequisito del dogfooding (senza promemoria l'uso quotidiano non regge). Contesto per il brainstorm, già deciso dall'autore il 2026-07-03 — **toast Windows ad app aperta/tray**, niente servizio ad app chiusa (quello è quick capture, Onda 5). Oggi le notifiche si *configurano* nell'editor ma nessun motore le fa scattare (F0-14).

4. **La 2.5 è anche il collaudo del principio "template = composizione salvata".** Oltre al redesign del builder con anteprima viva (E7), la spec 2.5 include il **test di guardia round-trip**: componi nel builder ogni capacità usata dai template ufficiali (ripetibile, serie nominate, unità durata, gesto journal, scale custom, goal, segmenti session) → salva come template → ristanzia → confronto campo per campo. È il gemello del guard di R7. Automatizza la parte meccanica di E16: la matrice di parità dell'Onda 2.9 si riduce a verificare la copertura del test + spot-check a schermo.

5. **Push di `main` su `origin`.** `main` è ~110 commit avanti a `origin/main`, non pushato per scelta dell'autore. Decisione parcheggiata **con scadenza**: entro l'inizio dell'Onda 4 il push è necessario (beta e release passano da GitHub). **Risolta il 2026-08-11 (delega dell'autore)**: verificato che `origin` è **privato** (404 non autenticato, raggiungibile con credenziali) → il push è backup, non pubblicazione — le due decisioni erano impacchettate insieme per errore. `main` e i branch di lavoro si pushano da subito come assicurazione (l'unica copia completa viveva su un mount Google Drive, con le evidenze dei pass visivi gitignorate = copia unica locale). La decisione sul *pubblicare* resta all'autore, scadenza invariata (inizio Onda 4).

6. **"Comporre e riconfigurare sono la stessa cosa in due momenti"** (autore, brainstorm 2.5 del 2026-08-11 — spec `2026-08-11-wave-2-5-builder-design.md`, decisione 3). La superficie di composizione è **una**: oggi ospitata nel dialog "Crea nuova abitudine", la direzione di lungo periodo è aprirla anche su un habit esistente al posto delle sezioni di Configura. Conseguenze: i mattoni della 2.5 si costruiscono host-agnostici (viste sul draft, non figli del dialog); la duplicazione UI builder/Configura è debito transitorio dichiarato, non un rischio da mitigare; **"aprire la superficie su un habit esistente" è un progetto candidato al riordino dell'Onda 5 post-dogfooding** (i vincoli sono noti: dati accumulati, cambio di tipo derivato, autosave-vs-atomicità, `HabitConfigHistory`).

**Ordine aggiornato del percorso**: merge 2.4 → **2.5** (builder + parità) → **pass visivo Onda 2** (+ giro fix; checklist scritta obbligatoria, compilata prima della sessione dalle spec d'onda + tracker — **compilata il 2026-08-11**: `docs/superpowers/plans/2026-08-11-wave-2-visual-pass-checklist.md`, resta da confermare la sezione 2.5 a fine esecuzione) → **E14 minimo** → ★ **dogfooding parte qui e non si ferma più** → 2.9 → 3 → 5 (riordinata) → 4. **Emendamento 2026-08-11 (delega dell'autore)**: E14 **non dipende dall'Onda 2** e non deve aspettare il pass visivo — il suo ciclo brainstorm→spec→plan può partire in parallelo alla coda della 2.5 o alla preparazione del pass; il vincolo vero resta uno solo: il dogfooding parte quando **sia** il pass visivo **sia** E14 sono fatti. La leva è la data del dogfooding (memoria di progetto: calendario osservato, non sforzo teorico).

**Nota per gli esecutori (integra §9.2)**: i fatti d'ambiente canonici vivono in **CLAUDE.md §8** (startup project delle migration = `Recordum.Data`; pragma CA1861 a mano su ogni migration; un bump `schema_version` tocca 8 riferimenti nei test, nomi dei metodi inclusi; `Strings.Designer.cs` si edita solo via shell). In ogni plan WPF ricopiare anche le **trappole WPF pagate care**: mai `Clear()` su una collection che è `ItemsSource` bindata senza sopprimere la persistenza per tutta l'operazione (data-wipe `4d01f9c`, ricomparso identico in `1d96967`); `ContentControl` con `ContentTemplate` statico sempre accoppiato a un selector o a un guard, mai al solo binding null (C1, Onda 2.4); `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute` — il guard va anche dentro il metodo (R7). Residui e questioni-modello stanno nel **tracker** (§"Onda 2.4 — review finale" e questioni CM-*), **non** in memorie machine-local di Claude Code.

## 2. Struttura: onde trasversali, verifica prima di costruire

Approccio scelto: **onde sequenziali**, ognuna lascia l'app in uno stato stabile e produce un output verificabile. Dentro ogni onda il lavoro meccanico si parallelizza subagent-driven (come U9.1). Razionale delle scelte chiave:

- **Onda 0 prima di tutto**: le informazioni sullo stato di chart/export/sweep risalgono a metà giugno 2026; pianificare fix su ipotesi vecchie produce piani sbagliati.
- **Onda 2 (per-tipo) prima dell'Onda 3 (estetica)**: non si lucidano schermate che la ridefinizione per-tipo ridisegna.
- **Foto/allegati nell'Onda 1**: è l'unico item che tocca lo **schema congelato** (invariante: mobile/sync riusano lo schema senza migrazioni invasive) → si progetta presto e bene, con migrazione additiva.

---

## 3. Onda 0 — Verità di base *(1–2 sessioni, autore alla tastiera)*

Nessun fix, solo misura. Sessione strutturata sulla VM (skill `run`) con checklist scritta; ogni esito finisce nel tracker con stato e sintomo esatto.

| # | Area | Cosa verificare |
|---|---|---|
| 0.1 | **Chart** | Schermata Charts (custom, plugin ChartEngine) + grafici del Dettaglio, per ognuno dei 5 tipi (resistance inclusa, ora period-aware). Il tracker li dà "rotti" al 2026-06-19: sintomo esatto per ciascuno (errore / vuoto / crash / dati insensati). |
| 0.2 | **Export** | I 5 formati (PDF/HTML/Markdown/JSON/CSV) su dati veri; file aperti e letti. Include il percorso plugin ExportEngine. |
| 0.3 | **Backup/restore runtime** | Restore di un `.recbak` corrotto a metà (verifica a video CR1–CR4, finora solo test-verified); auto-backup scheduler; passphrase opt-in. |
| 0.4 | **Sweep visivo accumulato** | Le rese mai ri-verificate a schermo: context menu, tooltip, splitter/sash, tab strip, empty-state, Color Picker v2 F1–F4, CR5/CR13/CR20/CR22 a video (lista completa nel tracker §"Ancora aperto sulla VM" 2026-06-25). |
| 0.5 | **Perf** | Home <500ms misurata su dataset realistico (budget da CLAUDE.md). |

**Output**: tracker aggiornato con lo stato *vero* + findings nuovi triati. **Exit**: nessun'area con stato "non so".

## 4. Onda 1 — Fondamenta funzionali

Le due capability trasversali, da chiudere prima di toccare le schermate.

### 1a. Chart / esplorazione dati funzionanti
Fix guidati dai findings 0.1. Scope minimo v1.0: grafici del Dettaglio sensati per ogni tipo (resistance = periodi, non completion), schermata Charts custom funzionante end-to-end (creazione → resa → persistenza), statistiche di base credibili. **Exit**: ogni tipo di habit ha grafici verdi a schermo, verificati dall'autore.

### 1b. Allegati foto alle note (capability generale, serve a U7)
**Brainstorm dedicato obbligatorio** prima di toccare codice. Punti che la spec dovrà decidere:
- **Schema**: tabella additiva (es. `attachment`: id GUID, `note_id` FK, path/filename relativo, mime, size, created_at/updated_at_ms) — evento-figlio ⇒ **niente** `created_by_plugin_id`; migrazione additiva + bump `schema_version`.
- **Storage**: file su disco sotto `%LocalAppData%\Recordum` (non BLOB nel DB cifrato — decidere ed esplicitare il trade-off privacy: il DB è SQLCipher, i file no).
- **Backup**: inclusione nel `.recbak` (inner ZIP) + restore; purge coerente col soft-delete (ordine: file → tabelle figlie → HabitNode).
- **Export**: comportamento per formato (embed in PDF/HTML? path in JSON/CSV?).
- **Riuso mobile** (Fase 5): niente dipendenze WPF nel layer Data.

**Exit**: foto allegabile a una nota veloce, visibile, inclusa nel backup/restore, coperta da test.

## 5. Onda 2 — Esperienza per-tipo *(il cuore della ridefinizione funzionale)*

U6, U7, U8, U10 e il builder hanno lo stesso filo rosso: **ogni tipo di habit merita la sua faccia**; oggi l'app tende a trattarli tutti da checkbox. Si parte con **un brainstorm unico "per-type experience"** che decide, tipo per tipo (action, counter, resistance, journal, state), quattro superfici:

| Superficie | Cosa risolve |
|---|---|
| Quadratini riga Home | **U6** — oggi uguali per tutti; devono essere type-aware + personalizzabili (quanti/cosa mostrare) |
| Landing dell'abitudine | **U10** — "qui non c'è niente" aprendo una action; cosa mostra di default ogni tipo |
| Quick-add / registrazione | **U8** — serie opzionale (check "ripetibile" nel builder), unità tempo scelta/convertibile (s→min/h…), via il tasto "1" che confonde |
| Anteprima nel builder | Il builder mostra un'anteprima viva e fedele dell'abitudine in costruzione, coerente col tipo |

Dal brainstorm unico discendono **esecuzioni separate** (una spec+plan ciascuna), in quest'ordine indicativo: **U6 → U10 → U8 → U7 → builder**. U7 (food diary) arriva dopo perché dipende da 1b (foto) e dalla decisione landing (U10): esperienza note-first orientata alla velocità — apri, nota veloce con data/ora automatiche, foto — *non* diario-come-home. I template seed si riallineano man mano (come fatto per "Niente fumo" con U9).

**Exit**: i template dimostrano i 5 tipi senza spiegazioni; l'autore verifica ogni esecuzione a schermo.

## 6. Onda 3 — Coerenza estetica sistematica

1. **Checklist del linguaggio scritta** (documento breve in `docs/design/`): chrome scura custom su ogni Window; famiglia popup (ombra + raggio 8 + hover accento) su menu/combo/tooltip; underline sugli input; Palatino/`Font.Default` ovunque (attenzione agli stili impliciti che non matchano le sottoclassi — gotcha noto); empty-state icona+titolo; scrollbar overlay auto-hide; niente Aero residuo; niente nero pieno fuori palette (finestre backup/export segnalate nel resoconto).
2. **Inventario di tutte le superfici** (Window, dialog, pannelli, popup) e audit spuntato voce per voce contro la checklist; fix in batch subagent-driven.
3. Chiude anche **U4** (badge scentrato nel cerchio heatmap) e lo **sweep R-*** residuo dal tracker (R-ribbonSlide opzionale, decidere lì).

**Exit**: una verifica visiva unica dell'autore sull'intera app; nessuna superficie fuori linguaggio.

## 7. Onda 4 — Preparazione al pubblico

| Blocco | Contenuto |
|---|---|
| **Onboarding primo avvio** | Welcome, creazione guidata della prima abitudine, i 5 tipi spiegati in 30 secondi (progressive disclosure, English-first — l'app pubblica parla EN prima di IT). Brainstorm dedicato. |
| **Packaging** | Canale GitHub Releases. Decidere zip self-contained vs installer (MSIX/Inno). **Nota tecnica**: `Directory.Build.props` pinna `SelfContained=false` + RID `win-x64` per il peso della cartella di lavoro — la build di *release* va configurata a parte senza rompere il pin dev. Detection/bootstrap runtime WebView2 (Evergreen). Versioning + CHANGELOG. |
| **Hardening macchina pulita** | Install in `Program Files` (working dir read-only — hazard WebView2 già fixato, cercarne altri), Windows 10 fallback, primo avvio senza dati, DB creato in `%LocalAppData%`, nessun crash senza rete. |
| **Docs utente** | README orientato all'utente (screenshot, quick start) separato da `ARCHITECTURE.md` (collaboratori). |
| **Chiusura** | `/security-review` sull'intero delta dalla baseline 2026-07-01 (CR1–CR4 restano il riferimento di severità); beta con 1–2 persone esterne su macchine pulite; release checklist (tag, binari, note). |

**Exit**: un estraneo installa e usa l'app su una macchina pulita senza assistenza.

---

## 8. Exit criteria v1.0 (tabella di rilascio)

Si rilascia quando ogni riga è ✅ (verificata, non presunta).

| # | Criterio | Verifica | Stato |
|---|---|---|---|
| E1 | Chart del Dettaglio sensati per i 5 tipi | Autore a schermo | ☐ |
| E2 | Charts custom end-to-end | Autore a schermo | ☐ |
| E3 | Export 5 formati leggibili | Autore, file aperti | ☐ |
| E4 | Backup/restore robusto incl. file corrotti | Test + prova runtime | ☐ |
| E5 | Foto nelle note + inclusione backup | Test + autore | ☐ |
| E6 | Template dei 5 tipi auto-esplicativi (U6/U7/U8/U10) | Autore a schermo | ☐ |
| E7 | Builder con anteprima viva coerente col tipo | Autore a schermo | ☐ |
| E8 | Audit estetico completo, zero superfici fuori linguaggio | Checklist spuntata | ☐ |
| E9 | Home <500ms su dataset realistico | Misura | ☐ |
| E10 | Onboarding primo avvio | Beta tester esterno | ☐ |
| E11 | Packaging + install su macchina pulita | Beta tester esterno | ☐ |
| E12 | `/security-review` finale senza findings alti aperti | Skill + triage | ☐ |
| E13 | Suite test Data+WPF verdi al 100% sulla VM | `dotnet test` | ☐ |
| E14 | Promemoria minimi funzionanti (toast ad app aperta/tray) | Autore a schermo | ☐ |
| E15 | Verità core abitudini: checklist Onda 2.9 completata, findings chiusi o esplicitamente rimandati | Sessione di misura + triage | ☐ |
| E16 | Principio "template = composizione salvata" rispettato: ogni template ufficiale ricostruibile a mano dall'utente (incluse unità/scale custom, gesto journal, nomi serie) e ogni composizione utente salvabile come template | Matrice spuntata (Onda 2.9) | ☐ |
| E17 | Aggiornamento sicuro: backup automatico pre-migrazione + percorso upgrade 1.0→1.1 testato (auto-update Velopack incluso) | Test + prova su VM pulita | ☐ |
| E18 | Impostazioni v1.0 complete: ogni comportamento configurabile ha la sua voce (niente setting nascosti), finestra ristrutturata e a norma di linguaggio | Autore a schermo + inventario spuntato | ☐ |
| E19 | Onda 5: le nove feature di potenziamento funzionanti e verificate (risparmio, craving, pause, routine, ricerca, digest, quick capture, import, **command palette + quick switcher**) | Autore a schermo | ☐ |
| E20 | Reattività percepita: avvio→Home interattiva ≤ 3s sulla VM di sviluppo (misurato in Onda 2.9, con budget anti-regressione per le migrazioni accumulate) | Misura + esit. 2.9 | ☐ |
| E21 | **Dogfooding**: l'autore usa Recordum come propria app reale da ≥4 settimane; le questioni-modello parcheggiate (CM-1…CM-4, W23-VF4/VF5/VF8, W24-*) adjudicate dall'uso o esplicitamente rimandate a 1.x | Tracker `DF-*` triato + decisioni CM | ☐ |

## 9. Metodo di lavoro (vincolante)

Chi esegue le onde saranno **sessioni future, anche con modelli diversi (Opus/Sonnet)**, senza il contesto di questa conversazione. Quindi:

1. **Ogni item = ciclo superpowers completo**: brainstorm → spec in `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md` → writing-plans → esecuzione subagent-driven TDD con review per task (implementer → spec-review → code-quality-review), ledger in `.superpowers/sdd/progress.md`.
2. **Ogni plan è autosufficiente**: sezione "Environment facts" + "Interface facts" in testa (modello: ledger U9.1). Minimo indispensabile da ricopiare: build `-p:UseAppHost=false`; hook che nega Edit/Write su `*.Designer.cs` → `Strings.Designer.cs` si edita **solo via shell**; nuova stringa = resx EN + resx IT + riga nel Designer; `IClock` mai `DateTime.Now`; feature-branch → merge in `main` (hook blocca commit diretti); test WPF girano solo su Windows.
3. **Tracker** `docs/phase-4-issues.md` = lista vivente dei findings; questo documento = mappa. Aggiornare entrambi a fine onda, mai in conflitto (in dubbio vince il tracker sul dettaglio, questa mappa sull'ordine).
4. **Decisioni di design solo nei brainstorm con l'autore** (Onda 1b, Onda 2, onboarding). Le esecuzioni non improvvisano UX: se il plan non copre un caso, ci si ferma e si chiede.
5. **Verifica visiva dell'autore** chiude ogni onda (stato `verified-fixed`), non la sola suite verde.

## 9-bis. Candidati post-v1.0 (parcheggio idee)

**2026-07-03 (sera): le otto idee originarie sono state PROMOSSE in v1.0 come Onda 5** (decisione autore, "v1.0 più completa") — vedi `2026-07-03-wave-5-power-features-design.md`. Restano parcheggiate per 1.x:

1. **Import da formati terzi** (Loop Habit Tracker in testa) — in v1.0 c'è il CSV simmetrico all'export (Onda 5.5).
2. **Import multi-metrica/serie** da CSV.
3. **Digest mensile/annuale** (in v1.0 solo settimanale).
4. **Quick capture ad app chiusa** (servizio/autostart; in v1.0 funziona con app in esecuzione, tray incluso).
5. **Statistiche craving dedicate** (in v1.0 il counter collegato ha già heatmap/trend propri).

## 10. Stato delle onde (aggiornare qui)

| Onda | Nome | Stato | Note |
|---|---|---|---|
| 0 | Verità di base | ✅ **completata 2026-07-03** | Sessione VM con l'autore. 13 findings F0-1…F0-13 nel tracker (§"Onda 0"). Solidi: heatmap, calendario, astinenza, backup-sicurezza (CR1/CR3/CR4 a video), perf (p95<300ms). Rotti: chart builder (crash), pipeline streak/andamento/orario, export resistance/nome-metrica. |
| 1a | Chart funzionanti + export | ✅ **completata 2026-07-08** | 14 task TDD, branch wave-1a-charts-export (merged in main 2026-07-08 sera). Chiusi: F0-1 (builder crash + UI complete), F0-2 (streak pipeline ✓), F0-3 (trend chart ✓), F0-4 (hourly ✓), F0-5 (export periods ✓), F0-7 (metric name ✓), F0-9 (schema backup ✓). Suite finali Data 377/377, WPF 699/699 verdi. Visual findings nuovi: W1a-VF1 (LiveCharts2 dark theme illeggibile, Onda 3), W1a-VF2 (builder picker non visibile, Onda 3). Verifica autore 2026-07-08. **Aggiornamento 2026-07-11 (audit)**: **W1a-VF2 risolto** — root cause vera = `LoadNew()` non seminava nessuna riga serie (layout e template erano sani); fix `0f4f637` su branch wave-2-1-home-row. Resta W1a-VF1 (Onda 3) **+ nuovi A4/A5** (trend Mood vuoto su card bianche; heatmap anno implausibile + ramp verde vs accento habit) → tracker §"Audit interattivo 2026-07-11". |
| 1b | Allegati foto | ✅ **mergiata in main 2026-07-09** | Schema v2.13 (allegati foto alle note); 4 bug reali corretti in corso d'opera. |
| 2 | Esperienza per-tipo | ◐ in corso | **2.0 ✅ mergiata in main 2026-07-09** (schema **v2.14** — non 2.13, già preso da 1b; piano `2026-07-09-wave-2-0-schema-and-prefs.md`): colonne `ui_prefs_json`/`repeatable`(+CHECK, versionata)/`series_labels_json`(versionata) + backfill data-aware + `IHabitUiPrefsService` (JSON camelCase) + governance ARCHITECTURE. Data 403/403, WPF 726/726. **2.1 (U6 riga Home) ✅ approvata e mergiata in main 2026-07-11** (Core 92 / Data 415 / WPF 754): chip valore-chiave + striscia per-tipo, clock vivo coi secondi, sezione Configura "Riga Home", F0-11; in sessione di verifica trovati e fixati anche il **data-wipe ComboBox** (`4d01f9c`), la **Configura vuota** (regressione workspace-shell, `9af35e0`) e W1a-VF2 (`0f4f637`); il giro **"2.1-bis"** (`7d5aa25`) ha chiuso il gap estetico A13: stili striscia per-tipo da spec §1 (quadretti/mini-barre-valore/banda-continua/punti/ramp, `ValuesLast14Days` nella query), larghezza fissa 96px, via la parola tipo (A6), zero-state ×0 muted. Include selezione-vs-completato nel calendario (ex F0-13, → piano 2.2). **2.2 (U10 fascia "Oggi" + colonne per-habit) ✅ mergiata in main 2026-07-13** (ff a `06a3ef4`, 8 task, WPF 786 / Data 415). **2.3 (U8 registrazione) ✅ mergiata in main 2026-08-08** (ff a `eacf534`, 42 commit): flag `repeatable` + serie nominate, migration data-only v2.15, cella durata con selettore h/min/sec, totale = somma della metrica; più il pass visivo 2026-07-28 (findings W23-VF1…VF8, i quattro di codice chiusi), il fix goal-actual 60×, l'export che nomina le unità e stampa il goal, il giro **"unità durata obbligatoria"** (spec+piano dedicati, schema **v2.16** con R7) e i cleanup R7/A9. Suite alla fusione: Core 113 / Data 432 / WPF 842 / Plugins 66 / ExportEngine 37 / ChartEngine 18. **2.4 (U7 food diary) ✅ mergiata in main 2026-08-11** (branch `wave-2-4-food-diary`, 18 commit + docs; schema **v2.17**; review finale opus sull'intero branch: due Critical fixati — C1 WebView2 fantasma via `ContentTemplateSelector` `1a97aa8`, C2 merge API `ui_prefs_json` `16f6ffb` — più tre Important; residui parcheggiati trascritti nel tracker §"Onda 2.4 — review finale" come W24-R1/R2/R3 e W24-Q1). **Prossima: 2.5 builder** — mandato doppio: anteprima viva coerente col tipo (E7) **+ test round-trip di parità template↔builder (§1-bis punto 4)**. ⚠️ **Verifica visiva di 2.2, 2.3 e 2.4 NON fatta**: per decisione dell'autore (2026-08-08) l'Onda 2 si costruisce per intero e si verifica in un'unica sessione a valle della 2.5, come previsto da §8 della spec per-tipo ("verifica visiva finale dell'autore su tutte le superfici, tipo per tipo"). |
| 2.9 | **Verità sul core abitudini** | ☐ | **Aggiunta 2026-07-03** (domanda dell'autore: "siamo sicuri che tutto il core funzioni?"). Sessione di misura stile Onda 0, DOPO l'Onda 2: checklist scheduling (weekly ×N, every X days, monthly) / recovery a schermo (GraceDay, Manual, Retroactive) / pause / goal end-to-end / timer (stopwatch, countdown, session + recovery post-crash) / tag e filtri / cascata reset_time / archivia-cestino-ripristina / **rassegna del catalogo template ufficiali uno per uno** / **gruppi** (goal di gruppo, aggregazione, esperienza a schermo — esclusi dall'Onda 2 e mai verificati) / **confini temporali** (cambio ora legale, cambio timezone, reset_time a cavallo di mezzanotte — motore testato, esperienza mai) / **cold start** (avvio→Home interattiva, budget ≤3s — E20; mai misurato, le migrazioni si accumulano). **Sgonfiata il 2026-08-11**: la matrice di parità template↔builder esce dalla checklist manuale — è automatizzata dal guard round-trip della 2.5 (spec `2026-08-11-wave-2-5-builder-design.md` §4: copertura seed obbligatoria + whitelist chiavi payload + ristanziazione); qui resta solo verificare che il guard copra i template correnti + uno spot-check a schermo (§1-bis punto 4). |
| E14→ | **Notifiche minime (anticipata)** | ☐ | **Spostata qui dalla Onda 4 (revisione 2026-08-10, §1-bis punto 3)**: blocchetto autonomo, prerequisito del dogfooding. Scope già deciso: toast ad app aperta/tray (F0-14). Ciclo brainstorm→spec→plan proprio. **Emendamento 2026-08-11**: non aspetta il pass visivo — non dipende dall'Onda 2; può partire in parallelo alla coda della 2.5. Il dogfooding richiede pass visivo **e** E14, in qualunque ordine arrivino. |
| DF | **Dogfooding (E21)** | ☐ | Parte dopo E14, non si ferma più; corre in parallelo a 2.9/3/5. Findings `DF-*` nel tracker; disciplina dati in §1-bis punto 2. Dopo ≥2 settimane: riordino Onda 5. |
| 3 | Coerenza estetica | ☐ | Dopo la 2.9; include U4 + sweep R-* + F0-8/F0-10 (UX dialog backup/restore) + F0-12 (banda bianca testata Home, da riprodurre) |
| 5 | **Potenziamento** | ☐ | **Aggiunta 2026-07-03** (autore: "v1.0 più completa" — de-freeze controllato). Spec `2026-07-03-wave-5-power-features-design.md`: risparmio + craving collegato (resistance), vacation mode UI, routine ordinata, ricerca FTS5, digest settimanale, quick capture globale, import CSV. Schema **v2.14** additivo. **Prima della 4** così beta e security review le coprono. |
| 4 | Preparazione al pubblico | ☐ | Onboarding, packaging, hardening, security review, beta. Include F0-6 (default EN + selettore lingua) e i deferiti 0.3-residuo (auto-backup nel tempo, passphrase opt-in) |
