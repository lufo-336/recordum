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
| E16 | Parità template↔builder: ogni template ufficiale ricostruibile a mano (incluse unità/scale custom) | Matrice spuntata (Onda 2.9) | ☐ |

## 9. Metodo di lavoro (vincolante)

Chi esegue le onde saranno **sessioni future, anche con modelli diversi (Opus/Sonnet)**, senza il contesto di questa conversazione. Quindi:

1. **Ogni item = ciclo superpowers completo**: brainstorm → spec in `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md` → writing-plans → esecuzione subagent-driven TDD con review per task (implementer → spec-review → code-quality-review), ledger in `.superpowers/sdd/progress.md`.
2. **Ogni plan è autosufficiente**: sezione "Environment facts" + "Interface facts" in testa (modello: ledger U9.1). Minimo indispensabile da ricopiare: build `-p:UseAppHost=false`; hook che nega Edit/Write su `*.Designer.cs` → `Strings.Designer.cs` si edita **solo via shell**; nuova stringa = resx EN + resx IT + riga nel Designer; `IClock` mai `DateTime.Now`; feature-branch → merge in `main` (hook blocca commit diretti); test WPF girano solo su Windows.
3. **Tracker** `docs/phase-4-issues.md` = lista vivente dei findings; questo documento = mappa. Aggiornare entrambi a fine onda, mai in conflitto (in dubbio vince il tracker sul dettaglio, questa mappa sull'ordine).
4. **Decisioni di design solo nei brainstorm con l'autore** (Onda 1b, Onda 2, onboarding). Le esecuzioni non improvvisano UX: se il plan non copre un caso, ci si ferma e si chiede.
5. **Verifica visiva dell'autore** chiude ogni onda (stato `verified-fixed`), non la sola suite verde.

## 10. Stato delle onde (aggiornare qui)

| Onda | Nome | Stato | Note |
|---|---|---|---|
| 0 | Verità di base | ✅ **completata 2026-07-03** | Sessione VM con l'autore. 13 findings F0-1…F0-13 nel tracker (§"Onda 0"). Solidi: heatmap, calendario, astinenza, backup-sicurezza (CR1/CR3/CR4 a video), perf (p95<300ms). Rotti: chart builder (crash), pipeline streak/andamento/orario, export resistance/nome-metrica. |
| 1a | Chart funzionanti + export | ☐ **prossima** | Scope da Onda 0: F0-1, F0-2, F0-3, F0-4 (chart/pipeline dati) + F0-5, F0-7 (export) + F0-9 (stamp schema backup, fix piccolo). Brainstorm→spec→plan. |
| 1b | Allegati foto | ☐ | Brainstorm dedicato; unico item che tocca lo schema |
| 2 | Esperienza per-tipo | ☐ | Brainstorm unico, poi U6 → U10 → U8 → U7 → builder. Include F0-11 (Configura/enum grezzi) e selezione-vs-completato nel calendario (ex F0-13). |
| 2.9 | **Verità sul core abitudini** | ☐ | **Aggiunta 2026-07-03** (domanda dell'autore: "siamo sicuri che tutto il core funzioni?"). Sessione di misura stile Onda 0, DOPO l'Onda 2: checklist scheduling (weekly ×N, every X days, monthly) / recovery a schermo (GraceDay, Manual, Retroactive) / pause / goal end-to-end / timer (stopwatch, countdown, session + recovery post-crash) / tag e filtri / cascata reset_time / archivia-cestino-ripristina / **rassegna del catalogo template ufficiali uno per uno** / **matrice di parità template↔builder**. Findings triaged nelle onde 3-4. |
| 3 | Coerenza estetica | ☐ | Dopo la 2.9; include U4 + sweep R-* + F0-8/F0-10 (UX dialog backup/restore) + F0-12 (banda bianca testata Home, da riprodurre) |
| 4 | Preparazione al pubblico | ☐ | Onboarding, packaging, hardening, security review, beta. Include F0-6 (default EN + selettore lingua), **F0-14 motore notifiche minimo** (decisione autore 2026-07-03: toast Windows ad app aperta/tray — oggi le notifiche si configurano ma nessun motore le fa scattare) e i deferiti 0.3-residuo (auto-backup nel tempo, passphrase opt-in) |
