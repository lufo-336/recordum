# Wave 1a Visual Findings — Author Report (2026-07-08)

**Status**: SAVED FOR FINAL REVIEW (end of all waves with Opus/Fable model review)

**Date**: 2026-07-08, Task 14 Step 3 visual verification session

**Source**: Author resoconto during live app verification

---

## Author's Exact Report

> i grafici direi che siano presenti ma non sono visualizzabili e sono esteticamente incoerenti esteticamente con l'estetica del programma, soprattutto col tema scuro. Secondo punto: [Image #2] dove dovrei selezionare l'abitudine? Ho cliccato nuovo grafico ma l'interfaccia è questa."C:\Users\luca.foglia\Desktop\recordum export\Lettura_verifica.pdf". MOstra 2.12. Ti invio questo messaggio, aspetta che chiuda l'app.

---

## Visual Evidence

### Finding 1: Charts Illegible on Dark Theme (Detail Screen)
**Image**: `trend-hourly-dark-illegible.png` (Image #1 from session)
- Trend chart: grid/axes/labels in light gray (nearly invisible on dark background)
- Hourly distribution: same issue — illegible rendering
- Both controls present but **not usable visually**
- **User statement**: "i grafici... non sono visualizzabili e sono esteticamente incoerenti con l'estetica del programma, soprattutto col tema scuro"

### Finding 2: Chart Builder Picker Invisible (New Chart Screen)
**Image**: `chart-builder-no-picker.png` (Image #2 from session)
- After clicking "Nuovo grafico" (New Chart), the builder form displays: Name field, Type combo, Period combo, "Aggiungi serie" button, Save button
- **Missing**: Habit picker ComboBox for series rows
- **Missing**: Column 2 (preview pane) entirely absent
- No way to select which habit to chart
- **User question**: "dove dovrei selezionare l'abitudine?" (where should I select the habit?)
- **User statement**: "Ho cliccato nuovo grafico ma l'interfaccia è questa" (I clicked new chart but the interface is this)

### Confirmation: Export Works
- Export PDF verified: Schema 2.12 displayed correctly in restore preview
- This is the only functioning component from Wave 1a visible to user

---

## Assessment

**Functional Impact**: Both findings are BLOCKERS:
1. Trend/hourly charts unusable on dark theme (default app theme)
2. Chart builder UI incomplete — cannot create a chart

**Exit Criteria Status**:
- E1 (Detail charts sensible) — **FAILED** (illegible)
- E2 (Charts custom end-to-end) — **FAILED** (builder UI incomplete)

**Wave 1a Declaration**: Marked "completata" but should be reconsidered — critical visual/functional issues prevent proper feature validation.

---

## Note for Final Review

**This report will be presented to Opus/Fable at the end of all wave execution** (after Onda 5 completion, before v1.0 release decision).

**Requested by author**: Save this resoconto exactly as written + image evidence, for final reckoning on whether Wave 1a deliverables actually meet the stated scope and exit criteria.

**Unanswered questions** (for final review):
- Why are LiveCharts2 axes/labels rendering in light gray on dark theme when task brief specified dark-theme support?
- Why is the 2-column Grid builder layout rendering with the picker column completely hidden?
- Are these regressions from Task 12/13 implementation, or pre-existing issues in the Task 11 baseline that went unnoticed until visual verification?

---

## File Locations

- Trend + hourly illegibility screenshot: Embedded above as Image #1
- Chart builder missing picker screenshot: Embedded above as Image #2
- Export PDF verification: `C:\Users\luca.foglia\Desktop\recordum export\Lettura_verifica.pdf` (Schema 2.12 confirmed by author)

---

**Saved by**: Claude Code (finishing-a-development-branch session, 2026-07-08)  
**Review scheduled**: End of Wave 5 execution, before v1.0 release decision
