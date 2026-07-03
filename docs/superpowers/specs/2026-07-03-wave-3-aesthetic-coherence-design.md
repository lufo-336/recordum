# Onda 3 — Coerenza estetica sistematica (design)

**Data**: 2026-07-03 · **Stato**: approvato dall'autore (brainstorm 2026-07-03)
**Contesto**: piano v1.0 (`2026-07-03-v1-redefinition-plan-design.md`, Onda 3). Exit criterion **E8** ("audit estetico completo, zero superfici fuori linguaggio"). **Nessun nuovo linguaggio**: si consolida quello esistente (decisione autore, brainstorm piano 2026-07-03). **Decisione autore (2026-07-03): l'audit copre ENTRAMBI i temi, dark e light** — un'app pubblica che parte col tema di sistema non può avere il light rotto.

**Sequenza**: eseguire DOPO l'Onda 2 — la fascia "Oggi", il builder con anteprima e le righe Home nuove entrano nell'inventario e non vanno lucidate due volte.

## 1. Deliverable centrale: `docs/design/language-checklist.md`

Codifica il linguaggio visivo — oggi sparso nei 16 mockup `docs/design/*.html` e in `BaseStyles.xaml` — in voci **verificabili**, ciascuna con il riferimento allo stile/mockup sorgente:

1. **Chrome finestra**: ogni `Window` usa `Recordum.ChromedWindow` (caption scura, pulsanti custom secondo `ResizeMode`); mai barra titolo nativa.
2. **Famiglia popup**: ContextMenu, dropdown ComboBox, ToolTip, submenu → raggio 8 (6 per i tooltip), ombra morbida su layer dedicato (mai `DropShadowEffect` sul sottoalbero col testo — causa testo soft), hover accento ~14%, niente colonna-icone Aero.
3. **Input**: TextBox/PasswordBox con underline minimal (i 4 borderless voluti sono censiti come eccezioni).
4. **Font**: Palatino/`Font.Default` su ogni superficie, incluse le **sottoclassi** di Window (lo stile implicito WPF non le raggiunge — serve `FontFamily` esplicito sul root); Consolas solo timer/diagnostica.
5. **Empty-state**: icona line + titolo centrati (pattern `EmptyState.xaml`), mai una riga grigia sola; CTA solo dove ha senso.
6. **Scrollbar**: overlay sottile auto-hide con hover/drag; mai colonna riservata.
7. **Niente Aero residuo**: Expander, Slider, CheckBox, DatePicker, GridSplitter già ritemplati — ogni nuova occorrenza deve usare gli stili di `BaseStyles.xaml`.
8. **Palette**: mai nero pieno o bianco pieno fuori dai token; colore habit = identificatore (accent bar, heatmap), mai decorazione.
9. **Icone**: Lucide line, mai emoji nella chrome (contenuti utente esclusi).
10. **Motion**: restrained — transizioni brevi e funzionali, niente bounce.
11. **Ogni voce va verificata in DARK e in LIGHT.** Il toggle tema (e il font switch) devono aggiornare la superficie a caldo.

## 2. Inventario e audit

- Censimento delle superfici XAML (~37 file in `src/Recordum.WPF/Views/**` + `Controls/**` + finestre) raggruppate in **famiglie**: finestre principali (MainWindow, Settings), dialog (Backup/Restore/Export/Builder/Template picker), colonne workspace (Cronologia, Dettaglio, Note, Configura, Diario), popup/menu, controlli riusabili (picker, slider, scala, timer bar), schermata Charts (nuova, da Onda 1a).
- **Tabella superficie × checklist** (in appendice al plan di esecuzione, non qui): ogni cella spuntata nei due temi. I fix escono in **batch per famiglia**, subagent-driven, con commit per famiglia.
- Verifica visiva **unica** con l'autore a fine onda, dark e light (cambio tema a caldo incluso).

## 3. Fix mirati inclusi nell'onda

- **F0-8 — dialog "Crea backup"** (`BackupDialog.xaml` + VM): etichetta sul secondo PasswordBox ("Conferma passphrase"), hint permanente "minimo 12 caratteri", indicatore di forza **localizzato** (`PassphraseStrength` → stringhe resx, via enum grezzo), riga informativa che spiega manuale (passphrase, portabile) vs automatico (chiave di macchina, Impostazioni) — chiude anche la percezione "due backup diversi" segnalata dall'autore.
- **F0-10 — errori restore** (`RestoreDialogViewModel`): mappa `RestoreException.Code` → stringa localizzata (`invalid_passphrase` → "Passphrase errata o file danneggiato", ecc. per tutti i codici emessi da `AesGcmBackupCipher`/`RestoreService`); testo con contrasto leggibile, mai il codice grezzo.
- **F0-12 — banda bianca testata Home in dark** (`verified`, screenshot 2026-07-03): riprodurre sistematicamente (ipotesi: superficie della testata che perde il `DynamicResource` del background in uno stato particolare; l'audit light è il contesto naturale per stanarla), poi fix + voce di checklist anti-regressione.
- **U4 — badge valore centrato** nel cerchio del giorno del calendario.
- **R-heatmapDead** — rimozione di `HeatmapViewModel` costruito/ricaricato ma non più mostrato (field, load, sottoscrizioni, `DataTemplate` morto in `HabitScreenViewModel`/XAML). Pulizia funzionale alle stesse superfici.
- **R-ribbonSlide** — **coda opzionale** (solo a onda altrimenti verde): slide 150-200ms del toggle ☰ sulla colonna ribbon 44px↔0 (entrambe assolute, nessuna `Star` — sicuro); helper `GridLengthAnimation` già in `…/scratchpad/wip-sidebar-anim/`. ⚠️ Non riapplicare all'albero Home (regressione fill-then-scroll nota).

## 4. Test e verifica

- I fix con logica (F0-8 CanRun/strength, F0-10 mappa errori) hanno test VM.
- L'audit è verifica umana guidata dalla tabella; le voci ad alto rischio di regressione (tema a caldo, font a caldo) hanno gli smoke test esistenti come rete.
- Chiusura: verifica visiva autore dark+light su tutte le famiglie → E8 spuntabile.

## 5. Fuori scope (esplicito)

B6b (template custom) e R-builderCustomUnit/CustomScale (feature, non coerenza); ridiscussione del linguaggio; superfici nuove delle onde 1-2 (arrivano già a norma, qui si verificano soltanto); audit di accessibilità completo (→ 1.x, da piano).
