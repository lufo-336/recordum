# Rimodernizzazione UI/UX — piano "guscio definitivo" (riferimento: Obsidian)

- **Data**: 2026-06-11 · **Branch**: `fase-4-polish` · **Stato**: design approvato in brainstorming (mockup ribbon in `.superpowers/brainstorm/1545-*/content/ribbon-layout.html`, variante B scelta)
- **Obiettivo dichiarato dall'utente**: dare a Recordum un'interfaccia **definitiva** anche se non tutte le funzioni sono pronte — il guscio finale dell'app, con riferimento estetico/strutturale a **Obsidian** (ribbon a icone + sidebar, estetica scura minimale). Buona parte dell'esistente piace e si conserva.

## Decisioni vincolanti

1. **Solo guscio ora.** Lo spec nav-model "Modello A evoluto" (`2026-05-29-navigation-model-design.md` — albero persistente + colonne-toggle al posto delle tab) resta **parcheggiato** e diventa la fase successiva del piano master, dopo le tre fasi qui sotto. Le tab Abitudine/Dettaglio/Note e i pannelli attuali NON cambiano in questo arco di lavoro.
2. **Riferimento Obsidian** = ribbon verticale a icone + estetica generale (chrome scuro minimale, densità, bordi sottili). NON include: tab multi-documento, command palette (rimandata, eventuale fase futura).
3. **Categorie future visibili ma disabilitate** nella finestra Impostazioni (Sync, Plugin, AI con pannello "In arrivo"). È una **deroga consapevole e approvata dall'utente** al principio "nessuna UI placeholder" di CLAUDE.md §7: l'obiettivo "interfaccia definitiva" prevale.
4. **Title bar custom** (WindowChrome), non nativa.
5. **Ordine: A (Impostazioni) → B (Guscio) → C (Estetica).** Ogni fase è committabile e verificabile a video da sola; nessun cablaggio temporaneo (l'ingranaggio della ribbon di B apre la finestra costruita in A).
6. Icone **Lucide** ovunque (catalogo + `IconKeyToGeometry` esistenti); mai emoji nel chrome. Stringhe `.resx` EN+IT dalla prima riga.

---

## Fase A — Finestra Impostazioni unificata

### Problema
Le impostazioni sono sparse: `SettingsPopup` (solo tema, popup al mouse), `BackupSettingsDialog` (dialog separato), Diagnostica sotto menu Aiuto. Nessun posto unico "da app definitiva".

### Finestra
`SettingsWindow`, modal con owner MainWindow, ~760×540 ridimensionabile, stile Obsidian: colonna categorie a sinistra (icona Lucide + nome, selezione evidenziata), pannello contenuto a destra con titolo categoria.

### Categorie al lancio

| Categoria | Icona (Lucide) | Contenuto | Origine |
|---|---|---|---|
| Aspetto | `palette` | Tema Sistema/Chiaro/Scuro | assorbe `SettingsPopup` → **eliminato** |
| Generale | `sliders-horizontal` | Orario di reset giornaliero (`AppSetting daily_reset_time`) | nuovo: oggi senza UI |
| Backup | `life-buoy` | auto-backup on/off, frequenza giorni, retention, percorso; bottoni Crea ora / Ripristina / Cronologia (command esistenti) | assorbe `BackupSettingsDialog` → **eliminato** |
| Diagnostica | `info` | contenuto dell'attuale dialog Diagnostica | assorbita |
| Sync | `refresh-cw` | **disabilitata** — pannello "In arrivo" + una riga descrittiva | placeholder approvato |
| Plugin | `puzzle` | **disabilitata** — idem | placeholder approvato |
| AI | `sparkles` | **disabilitata** — idem | placeholder approvato |

Testi "In arrivo"/"Coming soon" senza riferimenti interni alle fasi di roadmap.

### Architettura
- `SettingsWindowViewModel`: `ObservableCollection<SettingsCategory>` (Name, IconKey, IsEnabled, content-VM) + `SelectedCategory`. Le categorie disabilitate sono selezionabili-no (non cliccabili).
- I contenuti riusano i VM esistenti dove esistono: tema (`SettingsViewModel`), backup settings (VM del dialog attuale). "Generale" ha un piccolo VM nuovo che legge/scrive `daily_reset_time` via `IAppSettingService`.
- `ISettingsWindowLauncher.ShowAsync(SettingsCategoryId? initial = null)` sul pattern dei launcher esistenti (registrato in `ServiceRegistrar`). Il menu attuale ("Backup → Impostazioni") apre la finestra sulla categoria Backup; la voce tema apre su Aspetto. In Fase B la ribbon userà lo stesso launcher.
- `Popup SettingsPopupHost` in `MainWindow.xaml` viene rimosso in questa fase (sostituito dal launcher).

### Test Fase A
- VM: elenco categorie (7, di cui 3 disabled), categoria iniziale richiesta rispettata, round-trip tema / reset-time / impostazioni backup su fake services.
- Visiva: navigazione categorie, disabled non attivabili, persistenza reale, tema chiaro/scuro.

---

## Fase B — Guscio: title bar custom + ribbon + status bar

### Title bar custom
- `WindowChrome` su MainWindow: `CaptionHeight` per il drag, `ResizeBorderThickness` per gli snap/resize nativi, doppio-clic massimizza, `GlassFrameThickness=0`.
- Layout: sinistra icona app + "Recordum"; centro vuoto; destra min/max/close flat ridisegnati (hover discreto, close con hover rosso convenzionale).
- Brush dal tema (`Brush.Surface`/`Brush.Border`/`Brush.Text`) — segue chiaro/scuro.

### Ribbon (variante B approvata)
`WorkspaceRibbon` (nuovo UserControl), colonna fissa ~44px a sinistra del contenuto, icone Lucide ~20px con tooltip:
- **Workspace** (in alto): Home `house`, Grafici `bar-chart-3`. Stato attivo = sfondo `SurfaceElevated` + barretta accent 2px sul bordo sinistro.
- divisore orizzontale sottile;
- **Azioni**: Backup ora `life-buoy` (→ `BackupCreateCommand`), Esporta libreria `upload` (→ `ExportLibraryCommand`);
- **In basso**: Aiuto `circle-help` (→ `ShowDiagnosticInfoCommand`, in attesa di un vero help), Impostazioni `settings` (→ `ISettingsWindowLauncher`).

### Cosa sparisce
L'intera barra superiore attuale: RadioButton Home/Grafici, bottone Esporta libreria, `Menu` Backup/Aiuto. La Cronologia backup resta raggiungibile da Impostazioni → Backup e da Ctrl+5. KeyBinding Ctrl+1..5 invariati.

### Status bar
`TimerStatusBar` resta in basso: solo restyle (altezza compatta ~26px, bordo superiore 1px, tipografia `FontSize.Small`, brush a tema). Nessun contenuto nuovo.

### Architettura
`MainWindow.xaml` → griglia righe `[title bar / contenuto / status bar]`; contenuto = colonne `[ribbon | workspaces]`. La ribbon si lega ai command **già esistenti** di `MainWindowViewModel` — nessuna logica VM nuova. `WindowStyle=None` + WindowChrome richiede verifica di: massimizzazione senza overflow oltre il work area (già esiste `ClampToWorkArea`), restore, snap laterali, multi-monitor.

### Test Fase B
- Visiva (skill `run`): drag/snap/doppio-clic/massimizza-ripristina della title bar; tooltip e stati attivi ribbon; entrambi i temi; DPI scaling.
- VM: nessun test nuovo (command esistenti già coperti); eventuale smoke sul launcher Impostazioni.

---

## Fase C — Passata estetica

Nessun cambio funzionale. In scope:
- **Home toolbar** (lo screenshot dell'utente la mostra datata): Attive/Archiviate/Cestino come **pill segmentate** (stile sub-toggle del calendario, niente pallini radio); campo ricerca con icona `search` e placeholder; sort come dropdown compatto con label; "+ Nuova" allineata e coerente.
- **Scrollbar** scure sottili a tema (style WPF ScrollBar in BaseStyles).
- **Audit hover/focus/spaziature** sulle view principali (Home, HabitScreen, Charts, Backup history) — uniformare padding e stati interattivi.
- **RadioButton "nudi" fuori dai form**: ovunque facciano da toggle di vista (es. Settimana/Mese/Anno del calendario) valutare la pill segmentata per coerenza col resto del chrome. Nei form di configurazione i RadioButton classici restano.

### Test Fase C
Visiva per ogni blocco; nessuna logica toccata (eventuali snapshot test non previsti).

---

## Fase futura (fuori da questo arco): nav-model "Modello A evoluto"

Lo spec `2026-05-29-navigation-model-design.md` resta valido e diventa la fase 4 del piano master: albero persistente + colonne-toggle (Cronologia/Dettaglio/Note/Configura), deselezione click-a-vuoto, ＋Nuova▾ con gruppo. Si innesterà nel guscio costruito qui (la ribbon e la title bar non cambiano). Eventuale command palette: da valutare dopo il nav-model.

## Ordine di esecuzione e consegna

1. **Fase A** → spec è questo §A; piano di implementazione dedicato; verifica visiva; commit.
2. **Fase B** → idem, dipende da A solo per il launcher Impostazioni.
3. **Fase C** → lista di rifiniture indipendenti, eseguibili anche spezzate.

Ogni fase chiude con suite verde + verifica visiva utente, tracciata in `docs/phase-4-issues.md`.
