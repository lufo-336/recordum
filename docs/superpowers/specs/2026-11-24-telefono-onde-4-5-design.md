# Telefono — passi 4 e 5: la testa Android e il telefono che registra tutto

> Spec dei passi 4 e 5 della ROADMAP. Conversazione con l'autore del 2026-11-24 (branch `telefono-onda4`, da `unisci`).
> Mockup approvati: `docs/design/2026-11-24-telefono-onde-4-5/accesso-e-finestre.html` e `fogli.html`, sopra quelli del
> 2026-11-10 (`docs/design/2026-11-10-telefono-mockups/cornice.html`, `oggi-striscia.html`). Sonde: Android
> (`docs/design/2026-11-10-android-probe.md`), Drive col selettore di sistema (`docs/design/2026-11-22-saf-drive-probe.md`),
> e quelle del §13, da fare prima del piano. Letta da un revisore esterno il 2026-11-24: le sue domande sono risolte
> qui. Le scelte segnate *mia scelta* non le ha fatte l'autore: si possono correggere.

## 1. Perché

L'autore userà Recordum "praticamente per tutto" dal telefono (Samsung Galaxy S21 5G, Android 15, arm64): il telefono è
la casa dei dati e il posto dove si registra; il computer è lo studio (grafici, diario lungo). Il dogfooding vero parte
quando il telefono copre il manifesto di `docs/DOGFOODING.md`. La sonda del 2026-11-10 ha provato che dominio, dati
cifrati, ViewModel e viste passano su Android senza modifiche; il lavoro è la cornice da telefono, i servizi di
piattaforma e il tempo d'avvio.

## 2. Obiettivi, non obiettivi, anti-obiettivi

**Onda 4 — testa Android.** `Recordum.UI` diventa una libreria con due teste (`Recordum.Desktop`, `Recordum.Android`);
avvio fuori dal thread dell'interfaccia dietro una schermata d'attesa; la cornice approvata (barra in basso, pagine,
fogli dal basso); portachiavi di Android con l'impronta e le regole d'accesso. **Esce quando** l'APK, installato sul
telefono dell'autore, apre la Home dei dati veri (arrivati con un ripristino del `.recbak` del computer) e il tempo
dall'icona alla Home è misurato e scritto (§7.4).

Nell'onda 4 c'è **tutta la cornice**, perché ogni launcher deve avere la sua implementazione Android (un servizio
mancante compila e muore all'avvio): la barra, la Home compatta, la pagina dell'abitudine a sezioni (col comando in
basso che apre le viste di registrazione di oggi), il selettore dei modelli e il creatore come pagine, le Impostazioni
come elenco con le pagine che esistono già (Aspetto, Dati e backup con "Backup ora" e "Ripristina…") più Sicurezza, il
foglio di conferma, l'attesa, lo sblocco e l'errore. All'onda 5 restano i fogli per tipo dalla Home, il diario col
`TextBox`, fotocamera, notifica e backup automatico nella cartella del selettore. *Mia scelta*.

**Onda 5 — registrare tutto dal telefono.** I cinque tipi dai fogli dal basso, il diario in un campo di testo con la
barra, la fotocamera, il timer con la notifica, il backup automatico in una cartella scelta col selettore di sistema.
**Esce quando** ogni riga del manifesto di `DOGFOODING.md` si fa dal telefono (verificato dall'autore con la sezione
"Telefono" di `docs/CHECKLIST.md`). Da lì parte il dogfooding vero.

**Prima dell'onda 4: la WPF si spegne** (decisione dell'autore del 2026-11-24, §12).

**Non obiettivi.** La parte telefono di "Unisci" (passo 6, piano a sé; qui si lascia solo la strada aperta, §10.6).
PDF (QuestPDF), grafici, Statistiche e builder dei grafici sul telefono. Promemoria. "Cerca" unico (passo 7). APK
firmato per il pubblico e pubblicazione su uno store (passo 8). La pagina Sicurezza sul computer (arriva con 3b, §8.6).

**Anti-obiettivi** (fallimento anche se "funziona").
- Un aggiornamento dell'APK che chiede di disinstallare: sul telefono la disinstallazione cancella la casa dei dati (§11.2).
- Dati che escono dal telefono senza consenso: niente backup di Android verso Google, niente rete (§11.1).
- Un'impronta, un PIN o un'impronta aggiunta da altri che aprono il diario (§8).
- "L'app non risponde" (ANR) all'avvio, o un avvio che cresce di nuovo senza che nessuno lo misuri.
- Una terza implementazione di un'astrazione scritta per una shell morta (la WPF).

## 3. Vincoli

- `Recordum.Presentation` resta `net10.0` (mai `-windows`, mai `-android`): `NoWpfReferenceTests` e il suo gemello
  per Android (§6.1) lo proteggono.
- Ogni astrazione di host nuova: un'implementazione per testa (Desktop, Android) e un test della registrazione DI per
  ciascuna. Con la WPF spenta le teste sono due.
- Stringhe in `Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` (a mano, via shell). Parole del glossario.
- Versioni esatte in `Directory.Packages.props`. Nessun pacchetto Android nuovo se un'API di sistema basta (§8.2, §10.5).
- Le regole di CLAUDE.md §5–§6 valgono identiche sul telefono: `DayBoundary`, `IClock`, `CipherPragmas`, file in
  chiaro solo in `TempDirectory`, cartella dati privata.
- Il telefono dell'autore contiene dati veri dalla fine dell'onda 4: da lì ogni build installata passa da un backup.

## 4. Approcci per la cornice

1. **Le pagine di Avalonia 12 — scelta dall'autore.** Avalonia 12.1.2 porta `TabbedPage` (con `TabPlacement`),
   `NavigationPage` (pila con `PushAsync`/`PopAsync`, tasto indietro di sistema, bordi sicuri) e `ContentPage`. La barra in
   basso è un `TabbedPage` con quattro voci, ogni voce una `NavigationPage`; le viste di oggi entrano in `ContentPage`;
   ogni launcher del telefono è un `PushAsync`. Di nostro solo il contenitore dei fogli dal basso. Rischio: sono
   controlli nuovi; la sonda del §13 li prova sul telefono prima del piano.
2. **Una cornice nostra** (`ContentControl` con una pila di ViewModel, barra e indietro scritti da noi): controllo
   totale, ma riscrive indietro, animazioni e bordi sicuri. **Ripiego** se la sonda boccia la 1: i ViewModel non
   cambiano, cambia solo l'ospite.
3. *Scartata*: la finestra del computer rimpicciolita. Su Android le finestre non esistono (sonda, punto 4) e va contro
   la cornice approvata.

## 5. Decisioni dell'autore (2026-11-24)

| # | Decisione |
|---|---|
| T1 | L'onda 4 parte da `unisci` (non ancora in `main`); si ribasa su `main` dopo il merge. |
| T2 | Il passo 3 (sezioni 1–2 della checklist, dogfooding sul computer) va **in parallelo**: lo fa l'autore mentre si lavora al telefono. |
| T3 | **La WPF si spegne ora**, prima dell'onda 4 (si perdono export PDF e grafici finché non arrivano su Avalonia). |
| T4 | Sblocco con l'impronta, passphrase come ripiego (cornice del 2026-11-10). Le **regole d'accesso** in Impostazioni › Sicurezza come nello schermo 2a: Apertura in tre modi (impronta, predefinita · senza chiedere · passphrase ogni volta), "Blocca di nuovo" (predefinito 5 minuti), "Nascondi nelle app recenti" (acceso), "Ripassa la passphrase". |
| T5 | "Ripassa la passphrase" esiste ma **parte spento**. |
| T6 | Le finestre del computer: pagina con ← per ciò che chiede spazio, foglio dal basso per una sola risposta, selettori di file di sistema, niente "Apri cartella" (tabella del primo mockup). |
| T7 | Registrare: dove il computer apre la scheda, il telefono apre un foglio; "Annulla" per 5 s dopo un tocco; notifica fissa del timer senza "Ferma". |
| T8 | **Lo scatto finisce nella Galleria** (non solo dentro Recordum). |
| T9 | Cornice: le pagine di Avalonia 12 (approccio 1). |

Già decise il 2026-11-10 e non riaperte: barra in basso (Oggi, Cerca, Statistiche "In arrivo", Impostazioni); l'abitudine
come pagina a sezioni col comando in basso; Impostazioni come elenco di categorie; riquadri compatti, iniziali dei giorni,
Compatta predefinita sul telefono (scelta per dispositivo, fuori dall'unione); il diario del telefono in un `TextBox` con
la barra, l'anteprima viva solo al computer; PDF e grafici pesanti al computer.

## 6. Struttura

### 6.1 Progetti

| Progetto | TFM | Contiene |
|---|---|---|
| `Recordum.UI` | `net10.0`, **libreria** | tutte le viste, i controlli, i temi, i font; la cornice del telefono (§9) e la sequenza d'avvio condivisa (§7); le implementazioni dei launcher che non toccano API di piattaforma (pagine, fogli). Nome dell'assembly invariato: gli `avares://Recordum.UI/...` restano validi. |
| `Recordum.Desktop` | `net10.0`, eseguibile | `Program`, il lifetime a finestre, `X11PlatformOptions`, i launcher a finestra, DPAPI/libsecret. `install-linux.sh` e il `.desktop` puntano qui. |
| `Recordum.Android` | `net10.0-android`, APK | activity, `AvaloniaAndroidApplication`, manifest; i servizi di Android: Keystore e impronta, selettori di sistema, fotocamera, notifiche, lifetime. Nient'altro. |

- Le finestre (`MainWindow`, `SettingsWindow`, …) possono restare nella libreria: compilano su Android (sonda) e
  spostarle costa senza dare niente. *Mia scelta*; il costruttore le sposta in Desktop solo se servisse a qualcosa.
- `Recordum.UI.Tests` referenzia la libreria (e Desktop per i test di registrazione DI della testa desktop). La testa
  Android non ha una suite headless: ciò che si può provare sta nella libreria; i servizi Android si provano sul telefono
  con le voci della checklist e, dove un pezzo è logica pura (scelta del modo d'apertura, calcolo del "blocca di nuovo"),
  quella logica sta in Presentation o nella libreria, con i suoi test.
- Guardia nuova, gemella di `NoWpfReferenceTests`: Presentation, Data, Core e la libreria UI non hanno
  `TargetPlatformAttribute` (nessun `-android` che scivola dentro).

### 6.2 Trappole di build già pagate (sonda, punto 8)

- Gli using impliciti di Android si scontrano con Avalonia: `<Using Remove="Android.App;Android.Widget;…"/>` nella testa.
- Un `-r`/`RuntimeIdentifier` da riga di comando arriva ai progetti condivisi: `GlobalPropertiesToRemove="RuntimeIdentifier;SelfContained"`
  su ogni `ProjectReference` della testa Android, e il RID come proprietà del progetto.
- L'APK di debug pesa il doppio (44 MiB contro 20,8): per il telefono dell'autore si installa la Release.
- La sonda aveva spento analizzatori e `TreatWarningsAsErrors`: la testa vera no (eccezioni puntuali `NoWarn`, motivate).

## 7. Avvio

### 7.1 La sequenza condivisa

Una sola sequenza nella libreria, usata dalle due teste, con l'ordine di CLAUDE.md §6: opzioni → `DataDirectoryLock` →
`ReconcileOrphans` → `DetectState` → sblocco → contenitore → `CoreInitializer` → cultura, tema, righe della Home →
Home → **dopo** la Home: scheduler del backup, giro dell'unione, attivazione dei plugin che la Home non usa. La sequenza
parla con la testa attraverso un piccolo ospite (mostra l'attesa con una riga di stato, chiede lo sblocco, mostra
l'errore fatale, mostra la Home); su Desktop sono le finestre di oggi, su Android il contenuto della vista unica.

Ogni passo lento gira **fuori dal thread dell'interfaccia** (oggi `App.StartAsync` lo fa sul thread UI: sul desktop non
si vede, su Android dopo 10 s è ANR). La schermata d'attesa (schermo 1a) appare subito dopo lo splash di sistema e dice
cosa sta succedendo.

Sul telefono non ci sono argomenti: la cartella dati è quella privata dell'app (`Context.FilesDir/Recordum`), passata
esplicitamente alla sequenza; `--data-dir`, `--seed-demo` e simili restano strumenti del desktop.

### 7.1b Processo e activity

Su Android il processo vive più a lungo dell'activity (rotazione, tema, ritorno dal secondo piano, l'activity ricreata
dal sistema). Il lock della cartella dati, la chiave sbloccata e il contenitore dei servizi appartengono al **processo**
(all'`Application` di Android) e si costruiscono una volta sola; l'activity, quando nasce o rinasce, si limita a
riattaccare la vista: mai una seconda inizializzazione, mai un secondo lock. "Blocca di nuovo" (§8.4) e il riallineamento
della notifica (§10.4) si agganciano agli eventi dell'activity (pausa, ripresa), non all'avvio. *Mia scelta*.

### 7.2 Obiettivo

**Sotto i 3 s dall'icona alla Home**, al secondo avvio a freddo, mediana di 5, **senza il tempo del dito** sul sensore
(misurato a parte). *Mia scelta*, coerente col criterio di rilascio "avvio sotto i 3 s anche sul telefono". Oggi ≈ 6 s.

### 7.3 La strada, misurando dopo ogni passo

1. **Strumenti prima di tutto**: l'avvio scrive nel log (e in logcat) il tempo di ogni passo e "Home pronta" in ms
   dall'avvio del processo. Senza questo non si fa il passo 2.
2. Avvio fuori dal thread UI con la schermata d'attesa (toglie l'ANR, non il tempo).
3. Sblocco dal Keystore (salta Argon2id: ≈ 1 s). Con l'impronta, mentre il dito è sul sensore il modello EF si prepara
   in parallelo (non richiede la chiave).
4. Capire i 3,1 s di `InitializeAsync` coi tempi del passo 1 prima di cambiare qualcosa; la candidata è il modello EF
   costruito a freddo → **modello compilato**, generato dalla build (EF 10 lo sa fare) e non a mano, così una
   migrazione nuova non lo lascia vecchio; una guardia lo verifica.
5. Home prima del resto (§7.1).
6. Solo se si è ancora sopra i 3 s: AOT con profilo, o ReadyToRun. Da misurare anche nel peso dell'APK.

### 7.4 Come si misura

`adb shell am force-stop` + avvio dall'icona (o `am start`), 5 volte, mediana del "Home pronta" del log; a parte la durata
del riquadro dell'impronta. Le misure (data, build, numeri) si scrivono nel piano e alla fine in questa spec (§16). La
stessa misura entra nella checklist del telefono: si ripete a ogni onda.

## 8. Accesso

### 8.1 Primo avvio e i tre modi

Primo avvio: si crea la passphrase come oggi (Argon2id), poi si sceglie il modo d'apertura (predefinito: con l'impronta).
I tre modi (schermo 2a):

| Modo | Cosa c'è nel telefono | All'apertura |
|---|---|---|
| **Con l'impronta** (predefinito) | la chiave del database cifrata con una chiave del Keystore che chiede l'impronta (solo biometria forte; **non** il PIN del telefono) e si invalida se si aggiunge un'impronta | riquadro di sistema dell'impronta (1b); "Usa la passphrase" come ripiego |
| **Senza chiedere** | la chiave del database cifrata con una chiave del Keystore senza richiesta d'autenticazione | si apre; protegge il blocco schermo del telefono; i dati restano cifrati sul disco |
| **Passphrase ogni volta** | niente | la passphrase (≈ 1 s di Argon2id) |

Il modo non è un'impostazione nel database (serve prima di aprirlo): **è lo stato del portachiavi stesso**. Cambiarlo da
Sicurezza riscrive la voce e può chiedere una volta l'impronta o la passphrase.

**I dati del computer arrivano così** (è l'uscita dell'onda 4): primo avvio sul telefono con una passphrase del telefono
→ Home vuota → Impostazioni › Dati e backup › "Ripristina…" → selettore di Android → il `.recbak` del computer e la sua
passphrase di backup. Il ripristino di oggi ricifra il database ripristinato con la chiave del dispositivo che ripristina
(`RestoreService`, `rekey`): la voce del portachiavi (che conserva la chiave derivata, non la passphrase) resta valida e
non va riscritta. Il telefono e il computer possono avere passphrase diverse.

### 8.2 Il portachiavi di Android

Un `ISecretStore` sul Keystore di Android (il contratto c'è già, sincrono, "mai sul thread dell'interfaccia": un
riquadro dell'impronta è un'attesa come il prompt di libsecret). Solo API di sistema: `KeyGenParameterSpec`, AES-GCM,
`android.hardware.biometrics.BiometricPrompt` con `CryptoObject` — **niente AndroidX** (*mia scelta*, regola §3). Il
testo cifrato sta in un file nella cartella dati. `SecretKind.DbKey` (la chiave derivata del database) segue il modo
d'apertura; `SecretKind.AutoBackup` (la **passphrase** dei backup automatici, la stessa che servirà per ripristinarli
altrove) usa sempre la chiave senza richiesta: un backup automatico non può chiedere il dito.

Il tetto di 10 s di `StoredKeyUnlock` non vale mentre il riquadro dell'impronta è aperto: il tempo è dell'utente.

### 8.3 Quando l'impronta non basta (schermo 1c)

Annullata, troppi tentativi, sensore non disponibile, oppure **un'impronta nuova aggiunta al telefono** (la chiave del
Keystore si invalida da sola; la voce si cancella). In tutti i casi la pagina della passphrase, con la riga gialla che dice
**perché**, e la casella "Torna a sbloccare con l'impronta" spuntata: dopo la passphrase giusta la voce si riscrive.

### 8.4 Mentre la usi

- **Blocca di nuovo** (subito · 1 · 5 · 30 minuti · solo quando l'app si chiude; predefinito 5): tornando all'app dopo
  quel tempo in secondo piano, uno strato copre tutto e chiede impronta o passphrase secondo il modo. Con "Senza chiedere"
  la riga non c'è. Il blocco copre lo schermo: il database resta aperto, i timer e il backup continuano. *Limite noto*: la
  chiave resta in memoria; si alza solo se un giorno serve chiudere davvero il database in secondo piano.
- **Nascondi nelle app recenti** (acceso): l'anteprima nel selettore delle app è coperta; gli screenshot restano
  possibili (l'autore li usa per i difetti).
- **Ripassa la passphrase** (mai · ogni settimana · 2 settimane · mese; **predefinito mai**, T5): dopo lo sblocco, una
  volta per periodo, lo schermo 1d; "Non ora" lo salta; una passphrase sbagliata lo dice e propone "Cambia passphrase".
- **Cambia passphrase**: riga presente e spenta ("passo 7"), come nel mockup.

Le tre impostazioni sono chiavi di `app_settings` **del dispositivo**: classificate in `SyncCatalog` come locali, non
viaggiano con l'unione. Non viaggiano nemmeno con un **ripristino**: dopo un ripristino le chiavi locali sono quelle del
dispositivo che ripristina (anche nessuna, e allora valgono le predefinite: 5 minuti, recenti coperte, ripasso spento).
La stessa regola vale per le righe della Home, che sono per dispositivo: un `.recbak` del computer non porta la "Larga"
sul telefono. *Mia scelta*; oggi il ripristino sostituisce tutto `app_settings`.

### 8.5 Il backup di Android

`android:allowBackup="false"` e regole d'estrazione che escludono tutto: il backup automatico di Android verso Google è
un'uscita di dati senza consenso, e comunque inutile (la chiave del Keystore non lo segue).

### 8.6 La pagina Sicurezza sul computer

Il ViewModel è uno e sta in Presentation; la testa dice quali righe sa fare. Sul computer la pagina arriva con 3b
(Impostazioni a categorie) e prende anche la riga "Ricorda su questo dispositivo" oggi in Dati e backup. *Mia scelta*:
l'onda 4 non tocca le Impostazioni del computer.

## 9. La cornice

### 9.1 Barra e pagine

`TabbedPage` in basso: **Oggi** (la Home), **Cerca** ("In arrivo" fino al passo 7, *mia scelta*: il filtro della Home
resta nella lente), **Statistiche** ("In arrivo"), **Impostazioni** (elenco di categorie). Ogni voce è una
`NavigationPage`. Il timer che corre resta sopra la barra in ogni schermo (cornice, schermo 1).

### 9.2 Indietro

Il gesto indietro chiude prima il foglio, poi la pagina; dalla radice di una voce torna su Oggi; da Oggi manda l'app in
secondo piano (non la chiude).

### 9.3 Le finestre (T6)

| Sul computer | Sul telefono |
|---|---|
| Selettore dei modelli | pagina (schermo 3a), dal + della Home |
| Creatore | pagina (3b): anteprima della riga in alto, "Crea abitudine" in basso |
| Scheda dell'abitudine | pagina a sezioni col comando in basso (cornice, schermo 3) |
| Impostazioni | voce della barra; ogni categoria una pagina |
| Backup ora · Ripristina | pagine dentro Dati e backup (3d) |
| Conferma | foglio dal basso (3c), due pulsanti, il distruttivo sopra, non rosso |
| Salva backup · Apri file · Scegli cartella | selettore di sistema (SAF / selettore di foto) |
| Apri cartella | non c'è: la riga dello storico dice dove sta il file |
| Sblocco · Errore all'avvio | pagina a tutto schermo; l'errore con "Copia i dettagli" |

I contratti di oggi passano **percorsi**; un documento scelto col selettore di Android non è un percorso.
L'implementazione Android lavora su una copia in `TempDirectory`: la prende dal documento prima (apertura, ripristino,
foto) o la consegna al documento dopo la scrittura (salvataggio). Dove il contratto non dice quando la scrittura è
finita (`IBackupSaveFileDialog`), il contratto cambia; la forma la decide il piano. La copia temporanea si cancella sempre.

### 9.4 Misure da telefono

Riquadri compatti, bersagli almeno 40 px (*mia scelta*, da confermare guardando i PNG), nomi non schiacciati nella
striscia del gruppo (sonda, punto 7). Le viste comuni si provano in headless a 360 px di larghezza, chiaro e scuro, con
un'asserzione strutturale per ogni PNG.

### 9.5 Ciclo di vita delle pagine

Una pagina tolta dalla pila rilascia le sue sottoscrizioni (bus, eventi delle righe): sul telefono le pagine si aprono e
chiudono di continuo. Chiude il resto di **GIO-11** (le sottoscrizioni della Home e delle righe sostituite).

## 10. Registrare (onda 5)

### 10.1 La regola (T7, `fogli.html`)

Dove il computer apre la scheda per chiedere un valore, il telefono apre un **foglio dal basso** e si resta sulla Home. Il
foglio è lo stesso che apre il comando in basso della pagina dell'abitudine, e riusa le viste di oggi (oggi della scheda,
quick-add misurato e a serie). Il giorno su cui si scrive è sempre scritto nel foglio.

| Tipo | Sulla riga | Il foglio |
|---|---|---|
| Azione | ✓ registra | — |
| Contatore semplice | +1 | — |
| Contatore con un valore | apre il foglio | il valore con la tastiera numerica, la nota, "per un altro giorno" |
| Contatore a serie | apre il foglio | le serie di oggi, i campi della prossima pieni con l'ultima, il cronometro se corre; resta aperto fra le serie |
| Sessione | ▶ avvia, ■ ferma e registra | — |
| Astinenza | Inizia se non è in corso | in corso: "Segna una ricaduta" (adesso / prima…, nota) |
| Diario | — | gesto Diario: pagina per scrivere; gesto Nota veloce: foglio con le foto |
| Stato | apre il foglio | approvato il 2026-11-10 |

Dopo un tocco che registra (spunta, +1, ■, Inizia) compare "Annulla" per 5 s. Annulla toglie **esattamente ciò che il
tocco ha creato**, passando dalle stesse strade di una cancellazione dalla Cronologia (quindi dai trigger dell'unione,
che ne lasciano la lapide): la registrazione della spunta o del +1, il periodo appena iniziato. Dopo ■ toglie la
sessione registrata **e rimette il timer in corso dall'orario d'inizio di prima**: chi ferma per sbaglio rivuole il
cronometro. *Mia scelta*.

### 10.2 Il diario sul telefono

Un `TextBox` a tutto schermo in serif (la correzione automatica nell'editor del computer aggiunge parole invece di
sostituirle: sonda, punto 6). Stessa sintassi (Markdown, `#tag`, immagini) e stessa bozza salvata a ogni modifica. Barra
sopra la tastiera: titolo, grassetto, corsivo, elenco, `#tag`, fotocamera, immagine. I suggerimenti dei `#tag` come sul
computer, a misura di dito.

### 10.3 Fotocamera e foto (T8)

"Scatta" apre la fotocamera di sistema e le chiede di scrivere **nella Galleria** (cartella `Pictures/Recordum`, tramite
`MediaStore`): l'originale resta lì, dell'utente. Recordum legge lo scatto e lo importa con la regola D10 (ridimensiona,
togli i metadati, secondo la scelta in Diario e foto). Senza il permesso `CAMERA` nel manifest (con la fotocamera di
sistema non serve). "Dalla galleria" usa il selettore di foto di sistema, senza permessi. Se lo scatto si annulla non
succede niente.

### 10.4 Timer in secondo piano

Una **notifica fissa** finché esiste un timer in `active_timer`, col cronometro disegnato da Android dall'orario d'inizio
(nessun servizio in primo piano: niente da calcolare in secondo piano). Un tocco apre l'abitudine (dopo lo sblocco, se
serve). Niente "Ferma" (T7). La notifica segue `active_timer`: si mette all'avvio di un timer, si toglie alla fine, e
all'apertura dell'app si riallinea a ciò che c'è nel database (un timer sopravvissuto a un'uccisione dell'app riprende,
una notifica orfana sparisce). Il permesso per le notifiche si chiede al primo timer, non al primo avvio; negato, il
timer funziona lo stesso.

### 10.5 Backup automatico dal telefono

Il backup automatico scrive in una cartella scelta col selettore di sistema (Drive, per l'autore), come nello schermo 3d.
Lo stesso accesso a una cartella del selettore serve all'`ISyncFileStore` del passo 6: si scrive una volta, nella testa
Android, pensato per tutti e due (la sonda di Drive: scritture locali veloci, caricamento non garantito senza rete, letture
che possono essere vecchie). Il backup manuale ("Backup ora") consegna il file con il selettore di salvataggio.

### 10.6 La strada per "Unisci"

Nessun lavoro dell'unione nelle onde 4–5, ma: la pagina Sincronizzazione sul telefono c'è e dice "In arrivo"; lo
scheduler dell'unione non parte sulla testa Android finché non c'è un `ISyncFileStore` per il selettore di sistema; nulla
nelle onde 4–5 presume che l'archivio dell'unione sia un percorso.

## 11. Servizi di piattaforma

### 11.1 Tabella

Ogni astrazione di host di `AvaloniaShellServices` ha la sua implementazione per testa. Le Null di oggi restano Null
dove la funzione non c'è (export, colore dallo schermo, editor esterno).

| Astrazione | Desktop | Android |
|---|---|---|
| `IApplicationLifetime` | lifetime classico (oggi) | lifetime a vista unica (oggi: `InvalidCastException`, sonda punto 3) |
| `ISecretStore` | libsecret / DPAPI (oggi) | Keystore e impronta (§8.2) |
| launcher di pagine e fogli | finestre (oggi) | `PushAsync` / foglio (§9.3), nella libreria |
| `IConfirmationDialog` | finestra (oggi) | foglio |
| file e cartelle | dialog di sistema (oggi) | selettore di Android su copia in `TempDirectory` (§9.3) |
| `IFolderOpener` | file manager (oggi) | nessuno (la riga non c'è) |
| `IClipboard` | Null (oggi) | appunti di Android, se serve a "Copia i dettagli" |
| righe della Home | Larga predefinita | Compatta predefinita |
| `IThemeService` | segue il sistema | segue il sistema (modo scuro di Android) |

Le funzioni nuove che vivono solo sul telefono (notifica del timer, fotocamera, blocca di nuovo, app recenti) sono
codice della testa Android che ascolta il dominio, non astrazioni di Presentation con una Null sul desktop, salvo dove
un ViewModel le deve chiamare (la fotocamera dalla barra del diario: astrazione con Desktop che non mostra il pulsante).

### 11.2 Nessuna rete

Il manifest non chiede `INTERNET`. Avalonia o una libreria che lo richiedesse va scoperta prima del merge (guardia sul
manifest unito).

## 12. La WPF si spegne (T3)

Prima di ogni altro lavoro dell'onda 4 (primi task del piano, stesso branch):
- via `src/Recordum.WPF`, `tests/Recordum.WPF.Tests`, il loro posto nella soluzione e nella CI (il job Windows resta per le
  suite portabili e i test `WindowsOnly` di DPAPI);
- `Recordum.Plugins.ChartEngine` torna `net10.0` tenendo i builder (dipendono solo dai contratti); ciò che dipende da
  WPF se ne va; i suoi test seguono. Il renderer per Avalonia è del passo 7;
- `Recordum.Plugins.ExportEngine` è già `net10.0` e **resta intero**, coi suoi test: si perde solo la finestra
  d'export della WPF, cioè la strada per arrivarci, finché l'export non arriva su Avalonia (passo 7);
- `Recordum.Desktop` continua a uscire per **Windows e Linux** nella v1.0 (CLAUDE.md §3); il pacchetto per Windows è
  del passo 8, come oggi;
- CLAUDE.md, ARCHITECTURE.md, la skill `run` e i documenti che nominano la WPF come shell viva; le regole di CLAUDE.md
  che valgono solo per la WPF si tolgono, quelle portabili restano;
- il tracker: le voci che riguardano solo viste WPF si archiviano (regola del 2026-11-10, §5 della revisione).

## 13. Sonde, prima del piano

App usa e getta fuori dal repo (`~/dev/_spike-telefono`), come la sonda di Drive; risultati in
`docs/design/2026-11-2x-telefono-sonde.md`. Sul Galaxy S21 dell'autore, Release:
1. **Pagine di Avalonia 12**: `TabbedPage` in basso con quattro voci, `NavigationPage` con push/pop, gesto indietro di
   sistema (anche dalla radice), bordi sicuri, un foglio dal basso sopra la tastiera numerica (`AdjustResize`), una
   pagina con un `TextBox` lungo e la tastiera. Se fallisce: approccio 2 (§4).
2. **Keystore con l'impronta**: chiave con biometria forte, cifra e decifra, riquadro di sistema da un thread del pool,
   annullamento, invalidazione dopo l'aggiunta di un'impronta (a mano, dalle impostazioni del telefono), chiave senza
   autenticazione per l'altro modo.
3. **Notifica del timer**: cronometro dall'orario d'inizio, permesso chiesto al momento, l'app tolta dalle recenti e
   uccisa (`am kill`, poi "forza arresto"): cosa resta della notifica su One UI; il tocco che riapre su un'abitudine.
4. **Fotocamera nella Galleria**: scatto con uscita su `MediaStore`, senza permesso `CAMERA`; lo scatto appare nella
   Galleria; Recordum legge il flusso; i metadati che arrivano (per la regola D10).
5. **Anteprima nelle recenti** coperta e screenshot ancora possibili.

Le sonde **precedono il piano**: il piano dell'onda 4 si scrive con i loro risultati (approccio 1 o 2 per la cornice). Se
il telefono non è disponibile quando il piano va scritto, il piano assume l'approccio 1 e il suo primo task della
cornice ripete la sonda 1 dentro il repo come **cancello**: se fallisce, il task successivo è il ripiego dell'approccio
2. *Mia scelta*.

## 14. Test

- Viste comuni (Home compatta, pagina dell'abitudine a sezioni, fogli, Sicurezza, selettore, creatore, attesa, sblocco)
  in headless a 360 px, chiaro e scuro; asserzioni strutturali; i PNG li guarda l'autore.
- Logica d'accesso (quale schermata d'apertura, quando bloccare di nuovo, quando ripassare) in Presentation, coi suoi
  test e il "parametro muto" (CLAUDE.md §7).
- Registrazione DI per ciascuna testa; una guardia che nessuna astrazione di host resta senza implementazione Android.
- Sul telefono, voci nuove nella sezione 5 della checklist: avvio misurato, impronta (anche dopo un'impronta aggiunta),
  blocca di nuovo, recenti, ripristino dal `.recbak` del computer, aggiornamento dell'APK senza perdere dati; all'onda 5
  il manifesto riga per riga, fotocamera, notifica dopo un'uccisione.
- CI: un job che costruisce l'APK Release (senza firma di rilascio) a ogni push, così la testa non marcisce.

## 15. Installazione sul telefono e firma

- Un aggiornamento Android richiede **la stessa firma**: una chiave diversa (la chiave di debug di un'altra macchina)
  fa fallire l'installazione, e la scorciatoia "disinstalla e reinstalla" cancella la casa dei dati. Serve una **chiave
  di firma stabile per il dogfooding**, fuori dal repo, con una copia di sicurezza, usata da ogni build che va sul
  telefono dell'autore. Uno script installa con `adb install -r` (mantiene i dati), rifiuta di disinstallare e ricorda
  il "Backup ora" prima di installare una build nuova.
- L'identificativo del pacchetto è **`io.github.lufo336.recordum`** (autore, 2026-11-24), definitivo dal primo giorno di
  dati veri: cambiarlo crea un'altra app con un'altra cartella dati.
- La chiave di firma del dogfooding: un keystore generato una volta su Fedora, fuori dal repo (sotto
  `~/.local/share/recordum-signing/`), la sua password nel portachiavi di GNOME, una copia di sicurezza dove l'autore
  tiene le sue; le build da altre macchine la usano copiandola, mai con la chiave di debug locale. È la trappola 24 di
  Quaestio (`it.eduits.wsaquiz`): un APK firmato con la chiave di debug di un'altra macchina non si installa sopra, e
  l'unica strada è disinstallare perdendo i dati. *Mia scelta* il posto; la copia di sicurezza la decide l'autore.

## 16. Misure (si compila durante l'esecuzione)

| Data | Build | Avvio (mediana di 5) | Riquadro impronta | APK | Note |
|---|---|---|---|---|---|
| 2026-11-10 | sonda | ≈ 6 s | — | 20,8 MiB | sul thread UI, passphrase, senza modello compilato |

## 17. Rischi

- Le pagine di Avalonia 12 sono nuove: difetti su Android (bordi sicuri, indietro, tastiera). Sonda 1, ripiego §4.2.
- Samsung One UI è aggressivo coi processi in secondo piano: la notifica potrebbe sparire dopo un "forza arresto". Il
  timer non si perde comunque (`active_timer`).
- Il tempo d'avvio potrebbe non scendere sotto i 3 s senza AOT; l'AOT aumenta il peso dell'APK.
- La tastiera sopra i fogli e i campi (ridimensionamento della vista) è un punto debole classico.

## 18. Lasciato al costruttore

La forma dell'ospite della sequenza d'avvio; la forma del contratto di salvataggio che sostituisce `IBackupSaveFileDialog`;
`minSdk` (almeno 30: le API d'autenticazione del §8.2 lo chiedono; l'anteprima coperta nelle recenti è da 33 e sotto si
ripiega su niente); come il foglio dal basso si apre e si chiude (animazione contenuta, CLAUDE.md §11); dove vive il
file dei segreti di Android nella cartella dati; il nome dei canali di notifica.
