# Color Picker v2 — design

**Data**: 2026-06-23 · **Branch**: `fase-4-polish` · **Stato**: approvato (mockup `docs/color-picker-preview.html`)

## Contesto

La selezione colori è un unico controllo riusabile, `ColorSwatchPicker` (`src/Recordum.WPF/Views/ColorSwatchPicker.xaml`), usato in tre punti: editor habit (`EditorPane.xaml`), costruttore (`HabitBuilderView.xaml`) e colore note (`HabitEditor/Sections/NotesSectionView.xaml`). Migliorare il controllo li aggiorna tutti automaticamente.

Stato attuale del popup: trigger swatch 28px → popup con `HsvColorPicker` (quadrato SV + striscia hue), palette 12×3 (36 colori Tailwind, `ColorSwatchPicker.Palette`), recenti (`IRecentColorStore`, cap 8, AppSetting `ui.recent_colors`), campo hex. È funzionale ma: popup raggio 6 **senza ombra** (fuori dalla nuova famiglia flottante), swatch senza hover né indicatore di selezione, SV/hue poco arrotondati, hex senza "#".

API disponibili: `HsvColor` (statico: `FromHex`→(H,S,V)?, `ToHex`, `RgbToHsv`, `HsvToRgb`); `IRecentColorStore` (`GetAsync`/`AddAsync`); `ColorSwatchPicker.Color` DP (#RRGGBB, two-way) e `RecentColors` DP alimentata dal VM host.

## Obiettivi

Quattro feature + un polish estetico, tutto dentro `ColorSwatchPicker` (e servizi paralleli). `HsvColorPicker` riusato, solo restyle.

### Polish estetico (baseline)
- Popup nella **famiglia flottante**: raggio 8, `DropShadowEffect` morbido, margine trasparente per il bleed (come menù/dropdown/tooltip già fatti).
- `HsvColorPicker`: SV e hue a raggio 6, thumb rifiniti (invariati nel comportamento).
- Hex: prefisso **"#"** fisso (l'input contiene solo le 6 cifre), font monospace, anteprima live accanto.
- Swatch: angoli coerenti + stato **hover** (anello `Brush.TextMuted` tenue).

### Feature 1 — Indicatore di selezione
Lo swatch (palette/recenti/preferiti) il cui colore == `Color` corrente mostra un **anello accento** (doppio box-shadow: alone colore-superficie + anello `Brush.Accent`). Implementazione: converter `ColorEqualsConverter` (confronto case-insensitive di #RRGGBB) + `BindingProxy` che porta `ColorSwatchPicker.Color` nello scope degli `ItemsControl` (le cui voci sono la stringa hex). Trigger XAML sullo swatch.

### Feature 2 — Varianti tonali (shade ramp)
Sotto l'HSV, una riga di **7 tonalità** generate dal colore corrente: chiare→scure variando V (e leggermente S) attorno al colore scelto, via `HsvColor`. Nuovo metodo puro `ShadeRamp.For(string hex, int steps=7) → IReadOnlyList<string>` in `Services/` (testabile). La riga si ricalcola quando `Color` cambia (DP callback). Click su una tonalità imposta `Color`.

### Feature 3 — Contagocce schermo *(isolato, VM-only)*
Pulsante in basso nel popup; al click apre un overlay `Window` topmost full-screen trasparente che cattura il mouse; al click campiona il pixel sotto il cursore e imposta `Color`; `Esc` annulla. Loupe (lente d'ingrandimento) opzionale accanto al cursore.

Implementazione Win32 isolata in `Services/ScreenColorPicker.cs` (interop `GetCursorPos` + lettura pixel via DC dello schermo `GetDC(IntPtr.Zero)`/`GetPixel`, o copia schermo→bitmap) dietro un'interfaccia `IScreenColorPicker { Task<string?> PickAsync() }`. Il controllo dipende dall'interfaccia; se assente/fallisce, il pulsante si disabilita.

> ⚠️ **Caveat**: Win32 **Windows-only**, **non eseguibile né verificabile su macOS** — scritto alla cieca, verifica solo sulla VM. Per questo è dietro interfaccia e **isolato**: se sulla VM dà problemi si disattiva (nasconde il pulsante) senza toccare le altre 3 feature. **Implementato per ultimo.**

### Feature 4 — Preferiti salvabili
Fila "pinnata" persistente, separata dai recenti automatici. Nuovo `IFavoriteColorStore` (mirror di `RecentColorStore`): AppSetting `ui.favorite_colors` (CSV #RRGGBB), con `GetAsync`/`AddAsync`/`RemoveAsync`, cap 12, niente duplicati. Nuova `FavoriteColors` DP sul controllo (alimentata dal VM host come `RecentColors`). UI: label "Preferiti" con **"+ aggiungi"** (appunta il colore corrente) e **✕ su hover** per rimuovere. Sezione nascosta se vuota e non si sta aggiungendo.

## Architettura / nuovi pezzi

| Pezzo | Tipo | Responsabilità |
|---|---|---|
| `ShadeRamp.For` | static (Services) | genera la ramp tonale da un hex (puro, testabile) |
| `ColorEqualsConverter` | IValueConverter/IMultiValueConverter | confronto hex per l'anello di selezione |
| `IFavoriteColorStore` / `FavoriteColorStore` | service | persistenza preferiti (AppSetting `ui.favorite_colors`, cap 12, add/remove) |
| `IScreenColorPicker` / `ScreenColorPicker` | service (Win32) | contagocce schermo, isolato |
| `ColorSwatchPicker` | UserControl | restyle popup + nuove sezioni (ramp, preferiti, contagocce) + DP `FavoriteColors` |
| host VM (Editor/Builder/Notes) | esistenti | alimentano `FavoriteColors` come già fanno con `RecentColors`; "appunta corrente" → `FavoriteColorStore.AddAsync` |

Nessuna migrazione DB (solo nuovi AppSetting runtime, non seedati — come `ui.recent_colors`).

## Testing
- I nuovi pezzi logici (`ShadeRamp.For`, `FavoriteColorStore`, `ColorEqualsConverter`) vivono in `Recordum.WPF` (dipendono da `HsvColor`), quindi i loro test stanno in `Recordum.WPF.Tests` (`net10.0-windows`) → **scritti test-first ma compile-only su Mac, run sulla VM**. Casi: `ShadeRamp.For` (7 step, include/ordina chiaro→scuro attorno al colore base, hex validi); `FavoriteColorStore` (add/remove/cap 12/no-dup/persistenza CSV); `ColorEqualsConverter` (match case-insensitive, mismatch, null).
- **WPF compile-only su Mac** per la UI; verifica visiva (popup, anello selezione, ramp, preferiti, hex) **sulla VM**.
- **Contagocce**: nessuna verifica possibile su Mac → interamente sulla VM.

## Sequenza
1. Polish estetico + Feature 1 (selezione) + Feature 2 (ramp) — verificabili (logica testata, UI compile-only).
2. Feature 4 (preferiti) — service testato + UI.
3. Feature 3 (contagocce) — **per ultimo, isolato, cieco su Mac**.

## Fuori scope
Alpha/opacità (l'app usa #RRGGBB), palette personalizzabili dall'utente, sync dei preferiti.
