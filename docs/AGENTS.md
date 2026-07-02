# Recordum/docs

> Vedi [`../AGENTS.md`](../AGENTS.md) per le regole globali del progetto.

## Ambito

Questa directory contiene tutta la documentazione del progetto Recordum, inclusi design document, decisioni architetturali, specifiche e piani.

## Struttura

| Path | Contenuto |
|---|---|
| `ARCHITECTURE.md` | Panoramica narrativa dell'architettura (complementa `../CLAUDE.md`) |
| `MILESTONES.md` | Storico milestone dettagliato (decisioni passate, deviazioni, schema delta, known quirks) |
| `phase-4-issues.md` | Tracker issue/bug della Fase 4 (stati: reported/verified/triaged/in-progress/fixed/verified-fixed) |
| `plugin-api.md` | Documentazione API per sviluppatori di plugin |
| `dev-environment-sync.md` | Sincronizzazione ambiente VM Windows / Mac per sviluppo |
| `decisions/` | Architecture Decision Records (ADR) — es. `0001-ui-framework-wpf-over-avalonia.md` |
| `design/` | Mockup e anteprime UI (16 file HTML di preview: context-menu, slider, brand-mark, combobox, column-scroll, tooltip, ecc.) |
| `superpowers/plans/` | Piani strategici e milestone (47 file .md) |
| `superpowers/specs/` | Specifiche di design dettagliate (39 file .md) |

## Regole operative

### Documentazione

- **CLAUDE.md** è il contesto denso di progetto (invarianti, stack, roadmap, convenzioni). Ogni altro documento può referenziarlo ma non duplicarlo.
- **ARCHITECTURE.md** è la panoramica narrativa per chi legge il codebase per la prima volta.
- **MILESTONES.md** è il companion storico di CLAUDE.md — consultare per decisioni passate e schema delta.
- **phase-4-issues.md** è il tracker autoritativo dei bug e delle attività di stabilizzazione.
- I design mockup in `design/` sono anteprime di riferimento per l'implementazione UI.
- Le specifiche in `superpowers/specs/` sono autoritative per le feature che descrivono.
- I piani in `superpowers/plans/` contengono la cronologia delle decisioni di implementazione.

### Convenzioni di scrittura

- Documenti in Markdown (.md).
- Italiano per testi interni; inglese per documentazione pubblica.
- ADR in `decisions/` con naming `NNNN-titolo.md`.
- Link relativi tra documenti (es. `../CLAUDE.md`).

### Precedenza documenti

In caso di conflitto tra documenti:

1. Plugin spec §2.4 + §11 (provenance, encryption)
2. DatabaseSchema v2.2
3. DatabaseSchema v2.1
4. ProductSpec v2
5. Roadmap v1
6. CLAUDE.md (riassunto)

## Verifica tipica

1. I link tra documenti sono relativi e funzionanti
2. Le sezioni aggiornate hanno data e contesto
3. I mockup in `design/` sono sincronizzati con l'implementazione corrente

## Indice AGENTS.md

- Vedi `../AGENTS.md`
