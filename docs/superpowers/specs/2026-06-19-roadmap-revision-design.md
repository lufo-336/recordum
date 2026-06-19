# Recordum — Revisione roadmap: v1.0 desktop → mobile

> **Data**: 2026-06-19 · **Stato**: approvato (brainstorm con l'autore)
> **Scopo**: ridefinire le fasi di sviluppo post-Fase 3. Sostituisce l'ordine roadmap precedente (4 Polish → 5 Web → 6 Sync → 7 MAUI → 8 Plugin marketplace → 9 AI). Questo documento è la sorgente della nuova §7 di `CLAUDE.md`.

## 1. Contesto e motivazione

- **Freeze del 2026-06-16**: decisione dell'autore di non procedere con nuove fasi finché l'app desktop non è "davvero funzionante e gradevole". La Fase 4 smette di essere "polish a tempo indeterminato" e diventa la strada verso una **v1.0 desktop**.
- **Code review del 2026-06-19** (`docs/phase-4-issues.md`, CR1–CR24): emersi 4 findings ad alta priorità su **perdita-dati/sicurezza** del backup-restore (CR1 restore non atomico, CR2 guard compat stale, CR3 DoS crypto da input non fidato, CR4 dump in chiaro non distrutto) + bug visibili (durata invisibile, editor senza generation guard, ecc.).
- **Vincolo di risorse**: progetto solo-dev senza budget per hosting. Questo esclude *ora* un server di sync proprietario e rende prioritario tagliare lo scope verso una v1.0 mantenibile.
- **Fatti correttivi emersi nel brainstorm**: i **chart NON funzionano** (nonostante M3.9 fosse marcata completata); **export e plugin sono da verificare**. Vanno trattati come lavoro di stabilizzazione, non come "fatto".

## 2. Principio guida

Arrivare a una **v1.0 desktop funzionante nei suoi elementi basici** (incluso l'esplorazione dati di base), poi **portarla su mobile**. Tagliare lo scope (YAGNI): rimandare Web/PWA e AI; mantenere però il **sync via servizi cloud di terzi** come fase pianificata (non nel vago), perché è ciò che rende desktop e mobile veri compagni a costo-server zero.

## 3. Roadmap rivista

### Fase 4 — Stabilizzazione → Desktop v1.0 *(corrente)*
Portare il desktop WPF a una v1.0 solida e gradevole. **Asticella = "Core solido + UX gradevole + esplorazione dati funzionante"** (opzione 3 del brainstorm).

**In scope:**
- Chiudere i findings di `docs/phase-4-issues.md`, **priorità CR1–CR4** (perdita-dati/sicurezza backup-restore).
- **Far funzionare i chart** (oggi rotti).
- **Verificare e fixare export e plugin** (Chart Engine / Export Engine in-process).
- Esplorazione dati di base solida: heatmap, statistiche, distribuzione, goal progress.
- Perf (budget home <500ms), correttezza dei 5 tipi di habit, registrazione, streak (incl. astinenza), calendario, gruppi, note, backup/restore.
- UX/theming al livello "gradevole" del design brief; accessibilità di base.

**Fuori scope (rimandato):** AI/correlazioni; audit accessibilità completo (→ 1.x).

**Exit criteria:** tracker `docs/phase-4-issues.md` senza bug bloccanti aperti; aree core (chart/export/plugin/backup-restore/registrazione) verificate a schermo dall'autore; nessuna perdita-dati nota. `/security-review` superata sui findings di sicurezza prima di dichiarare la v1.0.

### Fase 5 — Mobile (MAUI), standalone offline *(era Fase 7)*
Portare i basics su telefono. Riusa `Core`/`Data`/`Plugins` **senza modifiche** (architettura già predisposta). Dati **locali sul dispositivo**, nessun account. Il trasferimento dati tra dispositivi avviene via **export/import `.recbak`** (già esistente, cifrato) come "sync manuale". Nessun sync automatico in questa fase.

### Fase 6 — Sync cloud via provider terzi *(confermata)*
Modello **"bring your own cloud"**: l'utente collega il **proprio** Dropbox / Google Drive / OneDrive / iCloud; l'app legge/scrive lì. **Zero costi server** per il progetto. Rende desktop + mobile veri compagni con dati condivisi. Il last-write-wins è già supportato dallo schema (`updated_at_ms`/`completed_at_ms` Unix ms su ogni entità). Costo = complessità di sviluppo (OAuth, risoluzione conflitti), non denaro.

### Future *(rimandate, senza numero fisso finché la Fase 6 non è fatta; ordine deciso)*
1. **Plugin runtime WebView2 + marketplace + widget + accessibilità completa** *(era Fase 8)* — i plugin first-party in-process (Chart/Export) bastano fino a qui.
2. **Web/PWA** *(era Fase 5)* — rimandata dopo il plugin runtime.
3. **AI insight + correlazioni** *(era Fase 9)* — la più isolabile e meno essenziale al "tracker che funziona".
4. **Sync con server proprietario** — molto futuro, **solo quando ci saranno risorse monetarie** per l'hosting. Sarebbe un'opzione *in aggiunta* al sync via provider terzi, non un rimpiazzo.

## 4. Cosa cambia rispetto alla roadmap precedente

| Prima | Dopo |
|---|---|
| 4 Polish → 5 Web → 6 Sync → 7 MAUI → 8 Plugin → 9 AI | 4 Stabilizzazione/v1.0 → 5 Mobile → 6 Sync terzi → Future (Plugin → Web → AI → server proprietario) |
| Web/PWA come prossimo passo | Web rimandato dopo il plugin runtime |
| Mobile in fondo (Fase 7) | Mobile anticipato a Fase 5 |
| Sync generico Fase 6 | Sync terzi confermato (6); server proprietario separato e molto futuro |
| AI Fase 9 | AI rimandata nel gruppo "Future" |

## 5. Invarianti che NON cambiano

I principi (§2 di CLAUDE.md) restano: offline-first, nessun account obbligatorio, privacy by default, composizione, open source, English-first. Lo **schema è finale dalla Fase 3** — nessuna migrazione invasiva per le fasi successive (mobile/sync riusano lo schema esistente). Ogni fase resta un prodotto funzionante.

## 6. Impatto

- Riscrivere `CLAUDE.md` §7 (tabella roadmap + milestone) secondo questa struttura, citando il freeze e l'asticella v1.0.
- `docs/phase-4-issues.md` resta il tracker autoritativo della Fase 4; i CR1–CR24 sono il backlog di stabilizzazione.
- Nessuna azione di codice innescata da questo documento (è planning); il lavoro di Fase 4 procede dal tracker.
