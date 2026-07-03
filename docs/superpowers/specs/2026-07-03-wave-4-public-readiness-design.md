# Onda 4 — Preparazione al pubblico (design)

**Data**: 2026-07-03 · **Stato**: approvato dall'autore (brainstorm 2026-07-03)
**Contesto**: piano v1.0 (`2026-07-03-v1-redefinition-plan-design.md`, Onda 4 — ultima). Chiude **F0-6** (lingua), **F0-14** (motore notifiche), onboarding, packaging, hardening, docs, security review, beta. Exit criteria: **E10, E11, E12, E14** (+ i deferiti 0.3-residuo: osservazione auto-backup nel tempo, test passphrase opt-in prima della release).
**Sequenza**: ultima onda, dopo la 3. Prerequisito di rilascio: tutte le onde precedenti `verified-fixed`.

## Decisioni dell'autore (2026-07-03)

1. **Packaging: Velopack** (installer + auto-update da GitHub Releases).
2. **Onboarding: nessun wizard** — empty-state guidati.
3. (Già decise a monte: default **EN** + selettore lingua in Impostazioni; **motore notifiche minimo** toast ad app aperta/tray.)

## 1. Lingua (F0-6)

- AppSetting **`ui.language`** ∈ {`en`, `it`}, default **`en`** (vale per tutti, anche su Windows italiano — decisione F0-6; senza wizard non c'è momento di scelta esplicita al primo avvio).
- **`AppLanguageService`** sul pattern di `ThemeService`/`AppFontService`: legge il setting allo startup e imposta `Strings.Culture` (il designer resx lo espone già) + `Thread.CurrentThread.CurrentUICulture`/`CurrentCulture` di default per formattazioni.
- Selettore in **Impostazioni → Aspetto** (ComboBox English/Italiano). Il cambio **richiede riavvio** (gli `x:Static` XAML si risolvono al load): `IConfirmationDialog` "Riavviare ora?" con riavvio immediato opzionale (`Process.Start` + shutdown).
- **Contenuti seminati** (template ufficiali, demo seeder): **EN**. La localizzazione IT dei payload template slitta a 1.x (documentata; il seeder demo va comunque tradotto in EN in questa onda — oggi è in italiano).

## 2. Onboarding: empty-state guidati (nessun wizard)

- Empty-state della Home (tab Attive) arricchito: CTA primaria **"Create your first habit"** (esistente) + due azioni secondarie: **"Start from a template"** (apre il template picker) e **"Explore with sample data"**.
- **Dati d'esempio rimovibili**: "Explore with sample data" esegue il `DemoDataSeeder` marcando i nodi creati (riuso di `created_by_plugin_id` con id riservato `io.recordum.sample-data` — colonna esistente, nessuna migrazione); in **Impostazioni** compare "Remove sample data" che elimina in blocco (soft-delete + purge) tutto ciò che porta quel marker. Si prova l'app senza sporcarla.
- I 5 tipi si spiegano **dove si scelgono**: nel builder (descrizioni per tipo già presenti/da rifinire in Onda 2) e nel picker template. Nessun coach mark, nessun dialog.

## 3. Motore notifiche minimo (F0-14)

- **`NotificationSchedulerHost`** (pattern `BackupSchedulerHost`: hosted al bootstrap, wait-one-tick): tick al minuto via `IClock`, legge le notifiche configurate (tabella/config esistente dell'editor sezione Notifiche), emette il promemoria per le habit con orario raggiunto e non ancora completate oggi.
- **Canale**: toast nativi Windows per app unpackaged/packaged (Win10 1809+); click sul toast → porta l'app in primo piano sull'abitudine. Se i toast non sono disponibili → fallback banner in-app.
- **Tray opzionale**: setting "Chiudendo la finestra, resta nell'area di notifica" (default **off**): con l'opzione attiva la chiusura della finestra minimizza a tray (icona con menu Apri/Esci) e i promemoria continuano a scattare. Con l'opzione off e l'app chiusa i promemoria **non** scattano — limite del "minimo", dichiarato nella user guide.
- Anti-spam: una notifica per habit per orario configurato al giorno (marker in memoria + persistito leggero, es. AppSetting `notifications.last_fired` o tabella esistente se prevista dallo schema — verificare in fase di piano lo schema notifiche reale).

## 4. Packaging e distribuzione (Velopack)

- **Build di release**: publish profile separato — `dotnet publish src/Recordum.WPF -c Release -r win-x64 --self-contained true` (l'utente non deve installare .NET 10). Il pin `SelfContained=false` in `Directory.Build.props` resta per il ciclo dev (il profilo release lo overrida); attenzione al peso (nativi SkiaSharp/SQLCipher/QuestPDF win-x64 soli, già pinnati).
- **Velopack**: `vpk pack` produce Setup.exe (installazione **per-utente**, niente admin) + pacchetto portable + delta; asset pubblicati su **GitHub Releases**. In-app: `UpdateManager` controlla update all'avvio (silenzioso, scarica, applica al riavvio); toggle Impostazioni "Check for updates automatically" (default on) + comando "Check now".
- **Versioning**: semver, tag `v1.0.0`; **`CHANGELOG.md`** (Keep a Changelog).
- **Firma**: assente in v1.0 → SmartScreen avviserà al primo avvio: nota in README/user guide ("More info → Run anyway"); dopo il rilascio, candidatura a **SignPath OSS** per la firma gratuita (1.x).
- Automazione: script di release riproducibile (PowerShell in `build/`) — publish → vpk → checksum; CI GitHub Actions opzionale, non bloccante per v1.0.

## 5. Hardening macchina pulita

Checklist eseguibile (entra nel plan come task di verifica su VM pulita):
- **Floor OS**: Windows 10 1809+ (toast API + WebView2 Evergreen). Dichiarato in README.
- **WebView2**: detection esistente + banner con link download (verificati); aggiungere il check al primo avvio con messaggio chiaro (l'editor ricco degrada a testo semplice senza runtime — già gestito).
- **Primo avvio offline**: nessun crash (update check e download WebView2 falliscono silenziosi).
- **Percorsi**: DB/chiavi/allegati/backup/log in `%LocalAppData%\Recordum` (già); install per-utente Velopack → niente Program Files read-only.
- **Log**: `%LocalAppData%\Recordum\logs` documentato in user guide ("se qualcosa va storto, allega questo file").

## 6. Docs utente e privacy

- **`docs/USER-GUIDE.md`** (EN, screenshot): installazione (incl. nota SmartScreen), i 5 tipi in breve, creare/registrare, template, backup/restore (passphrase!), export, promemoria e limiti, dove sono i miei dati, FAQ.
- **Dichiarazione privacy** (sezione della guide + README): dati solo sul dispositivo; DB cifrato (SQLCipher); foto allegate in chiaro nella cartella dati (scelta documentata, Onda 1b); backup `.recbak` cifrato (Argon2id + AES-256-GCM); **zero telemetria, zero rete** salvo update check (disattivabile) e download WebView2.
- Release notes v1.0.

## 7. Chiusura del cerchio

- **`/security-review`** sull'intero delta dalla baseline 2026-07-01 (aree calde: motore notifiche, updater Velopack, importer immagini 1b, parser payload template).
- **Beta**: 1-2 persone esterne su macchine pulite con guida di test = flusso completo (installa → onboarding → crea da builder E da template → registra per giorni → foto → export → backup/restore → update da release fittizia).
- **Release checklist**: suite verdi; onde 0-3 `verified-fixed`; deferiti 0.3 chiusi (auto-backup osservato nel tempo, passphrase opt-in testata con backup fresco); smoke installer su VM pulita; tag + asset + note.

## 8. Test

- `AppLanguageService` (default en, persistenza, culture applicata); empty-state (3 CTA, visibilità per-tab); sample-data marker (seed marcato + rimozione in blocco); `NotificationSchedulerHost` (scatta all'orario, non duplica, rispetta completate-oggi, fallback); updater: mock del feed (nessun test di rete reale).
- Verifiche manuali da checklist §5 + beta §7.

## 9. Fuori scope (esplicito)

Firma del codice (1.x via SignPath); notifiche ad app completamente chiusa (servizio/scheduled task → 1.x); localizzazione IT dei payload template (1.x); CI/CD completa (script riproducibile basta per v1.0); winget/store (1.x); wizard di onboarding (deciso: no).
