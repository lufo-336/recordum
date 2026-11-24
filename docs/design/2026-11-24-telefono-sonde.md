# Sonde del telefono per le onde 4–5 (2026-11-24)

**Domanda**: le cinque sonde del §13 della spec `docs/superpowers/specs/2026-11-24-telefono-onde-4-5-design.md`, prima
di eseguire il piano dell'onda 4: le pagine di Avalonia 12 reggono la cornice? Quale cifratura per il portachiavi?
Cosa resta della notifica del timer dopo un'uccisione? Lo scatto finisce nella Galleria? L'anteprima nelle recenti si
copre?

**Come**: app usa e getta `~/dev/_spike-telefono` (`it.recordum.sondatelefono`, un'app diversa da Recordum), Avalonia
12.1.2 + `Avalonia.Android` 12.1.2, `minSdk` 30, `targetSdk` 36 (predefinito del workload 36.1), Release, sul Galaxy
S21 dell'autore (Android 15, One UI, navigazione a tre tasti). Le sonde si lanciano con un intent
(`am start … --ei probe N`), non toccando coordinate (vedi trappole). Le azioni col dito le ha fatte l'autore.

## 1. Pagine di Avalonia 12 — l'approccio 1 regge, con tre regole

| Prova | Esito |
|---|---|
| `TabbedPage` in basso con quattro voci, ogni voce una `NavigationPage` | ✅ |
| `PushAsync`, ← nell'intestazione, indietro di sistema che toglie **una** pagina (pila di 3 → 2) | ✅ |
| Bordi sicuri in verticale e in orizzontale (barra di stato, tacca, barra dei tasti) | ✅ le pagine li applicano da sole |
| Rotazione e tema chiaro/scuro di sistema con una pagina spinta | ✅ nessuna ricreazione (`ConfigurationChanges`), pila intatta, tema seguito dal vivo |
| Indietro con un foglio aperto sopra una pagina spinta | ✅ chiude solo il foglio (il nostro gestore di `TopLevel.BackRequested` arriva per primo, `Handled` falso) |
| Indietro alla radice di Oggi, non gestito | ⚠️ `AvaloniaActivity` **chiude l'activity** (`OnDestroy finishing=True`) |
| L'activity ricreata con lo stesso processo | ❌ la stessa `MainView` riattaccata **non disegna le pagine** (le 4 pagine ci sono, misure giuste, schermo vuoto; un controllo semplice accanto si disegna) |
| L'activity ricreata con una cornice **nuova** | ✅ si disegna; `App.OnFrameworkInitializationCompleted` resta una volta per processo |
| Foglio con un campo numerico e la tastiera | ⚠️ la tastiera **copre** il foglio: con `targetSdk` ≥ 35 Android impone l'edge-to-edge e `AdjustResize` non ridimensiona più la vista (resta 360 × 800) |
| Lo stesso foglio con un margine dal basso = altezza vista − `InputPane.OccludedRect.Y` | ✅ il foglio sta esattamente sopra la tastiera |
| Pagina con un `TextBox` lungo e la tastiera | ⚠️ copre il cursore; ✅ con lo stesso margine sulla cornice la pagina si accorcia e scrivendo scorre al cursore |
| Tastiera numerica | ✅ `TextInputOptions.ContentType = Number` |

Misure: 1080 × 2400 px a scala 3 → **360 × 800** DIP; bordi sicuri 26,7 sopra e 48 sotto in verticale.

**Regole per il piano**
1. **Activity nuova → cornice nuova.** Il processo tiene lock, chiave, contenitore e ViewModel; un'activity nata dopo la
   prima riceve un `PhoneShell` nuovo costruito dal contenitore (la pila delle pagine riparte da Oggi). Riattaccare la
   vecchia lascia lo schermo vuoto.
2. **Radice di Oggi → `MoveTaskToBack(true)`** in `MainActivity` (come già nel piano): senza, l'activity si chiude e alla
   riapertura vale la regola 1.
3. **Tastiera a mano.** `SheetHost` e la cornice si spostano da soli sopra la tastiera con `TopLevel.InputPane`
   (`StateChanged`, `OccludedRect`); il foglio aggiunge anche il bordo sicuro di sotto (sta fuori dalle pagine, che lo
   applicano da sole).

**Stranezza**: il primo `SafeAreaChanged` arriva in pixel fisici (`0,80,0,144`), i successivi in DIP (`0,26.7,0,48`).
Leggere `TopLevel.InsetsManager.SafeAreaPadding` al momento, non fidarsi del primo evento.

## 2. Portachiavi — RSA-OAEP, e un quarto modo: il blocco schermo

Il telefono dell'autore si sblocca **solo col PIN**: nessuna impronta né volto registrati, e il Keystore rifiuta di
creare una chiave "solo biometria forte" (`At least one biometric must be enrolled…`;
`BiometricManager.CanAuthenticate(STRONG) = ErrorNoneEnrolled`). **Decisione dell'autore** (2026-11-24): l'apertura
la sceglie l'utente fra tutti i modi, compreso il blocco schermo del telefono. Per la prova l'autore ha registrato due
impronte temporanee.

| Prova | Esito |
|---|---|
| Chiave RSA 2048 "solo impronta" (`AUTH_BIOMETRIC_STRONG`, invalidata da un'impronta nuova) | ✅ creata in 151 ms (con un'impronta registrata) |
| La pubblica RSA cifra **senza dito** (OAEP SHA-256, MGF1 SHA-1, copia "libera" della pubblica) | ✅ 256 byte |
| La privata decifra dietro `BiometricPrompt` + `CryptoObject`, chiamata da un thread del pool, riquadro portato sul thread UI | ✅ (tipo 2, impronta) |
| AES-GCM "solo impronta": cifrare senza dito | ❌ come atteso: anche la scrittura chiede il dito |
| AES-GCM con il dito, cifra e decifra | ✅ |
| Riquadro annullato col tasto indietro | ✅ `OnAuthenticationError(UserCanceled)` |
| Pulsante "Usa la passphrase" del riquadro | ✅ arriva al suo ascoltatore, distinto dall'annullamento |
| **Seconda impronta aggiunta** → RSA e AES "solo impronta" | ✅ `Cipher.Init` fallisce subito, senza riquadro: `Key permanently invalidated` |
| … ma la **pubblica RSA cifra ancora** | ⚠️ un file scritto così non si aprirebbe più |
| Chiave RSA "blocco schermo" (`AUTH_BIOMETRIC_STRONG \| AUTH_DEVICE_CREDENTIAL`, non invalidata da impronte nuove) | ✅ creata senza impronte (166 ms); decifra col PIN (tipo 1) e, dopo, con l'impronta (tipo 2); regge l'impronta aggiunta |
| Chiave senza autenticazione (AES-GCM) | ✅ andata e ritorno in 9 ms |

**Regole per il piano (Task 10)**
- **RSA-OAEP** (opzione a): attivare l'impronta o il blocco schermo al primo avvio non chiede il dito; decifrare sì.
- **Prima di scrivere con la pubblica**, provare `Init` della privata: se è invalidata, rigenerare la coppia.
- Nel modo blocco schermo Android vieta il pulsante negativo nel riquadro (`DEVICE_CREDENTIAL`): "Usa la passphrase"
  sta nella nostra pagina, dopo un annullamento.
- `USE_BIOMETRIC` nel manifest (permesso normale, nessuna richiesta all'utente).
- In Release l'eccezione è arrivata come `Java.Lang.Exception` col messaggio giusto: la sonda non nominava
  `KeyPermanentlyInvalidatedException`, quindi il tipo è stato tolto dal trimming. Il codice vero lo nomina nel `catch`;
  da riprovare sul telefono.
- "Con l'impronta" si offre solo se `CanAuthenticate(BIOMETRIC_STRONG)` è `Success`; "Col blocco schermo" solo se il
  telefono ha un blocco sicuro (`KeyguardManager.IsDeviceSecure`).

## 3. Notifica del timer

| Prova | Esito |
|---|---|
| Permesso `POST_NOTIFICATIONS` chiesto al momento | ✅ il riquadro di sistema compare quando serve; concesso |
| Cronometro dall'orario d'inizio (`SetUsesChronometer`, `SetWhen` 10 min fa) | ✅ l'autore ha visto ≈ 10 minuti che corrono |
| App in secondo piano uccisa (`am kill`) | ✅ la notifica resta |
| App tolta dalle recenti (One UI uccide il processo) | ✅ la notifica resta |
| "Forza arresto" (`am force-stop`, la stessa chiamata del pulsante nelle Impostazioni) | ❌ Android la toglie |
| Aggiornamento dell'APK (`adb install -r`) | ❌ Android la toglie |
| Tocco sulla notifica dopo la morte del processo | ✅ riapre: `OnCreate` con lo stato salvato **senza** l'abitudine nell'intent, poi `OnNewIntent` con l'abitudine |

**Regole**: il riallineamento all'apertura dell'app (spec §10.4) copre "forza arresto" e aggiornamento. L'abitudine
della notifica si legge sia in `OnCreate` sia in `OnNewIntent` (`LaunchMode = SingleTop`).

## 4. Scatto nella Galleria

| Prova | Esito |
|---|---|
| `ACTION_IMAGE_CAPTURE` con `EXTRA_OUTPUT` su una riga di `MediaStore` in `Pictures/Recordum`, **senza** permesso `CAMERA` | ✅ la fotocamera Samsung scrive; la foto è nella Galleria, album "Recordum" (visto dall'autore) |
| La stessa riga creata con `IS_PENDING = 1` | ❌ la fotocamera non riesce a scriverci: risultato `Canceled`, file assente |
| Lettura del flusso dopo lo scatto | ✅ JPEG 4032 × 3024, 2,6 MB |
| Metadati | Make=samsung, Model=SM-G991B, DateTime, **Orientation=6** (ruotata di 90°), GPS assente |
| Scatto annullato | la riga vuota resta finché Recordum non la cancella (cancellata: nella Galleria solo la foto vera) |

**Regole (onda 5)**: riga senza `IS_PENDING`; l'importazione D10 applica l'orientamento ai pixel **prima** di togliere
i metadati; l'URI della riga si salva fuori dalla memoria (il processo può morire mentre la fotocamera è aperta) e
all'avvio si cancellano le righe nostre senza file.

## 5. Anteprima nelle recenti

`Activity.SetRecentsScreenshotEnabled(false)` (API 33): l'anteprima della sonda nelle recenti è coperta, lo screenshot
dentro l'app riesce (verificato dall'autore). ✅

## Trappole pagate qui

- **Build incrementale dopo una modifica del solo manifest**: l'APK Release muore all'avvio
  (`UnsatisfiedLinkError … AvaloniaAndroidApplication_1.n_onCreate`). Una build pulita (`-t:Rebuild`) lo sistema:
  `install-phone.sh` fa sempre una build pulita (≈ 2 min).
- **Tocchi a coordinate con `adb input tap`**: una notifica comparsa in alto si prende il tocco (si è aperta un'altra
  app). Sul telefono dell'autore si comanda con gli intent, non con le coordinate.
- `(IPrivateKey)keyStore.GetKey(...)` lancia `InvalidCastException` in .NET per Android: passare la chiave così com'è
  (`IKey`) a `Cipher.Init`.
- Un nome fisso per l'activity (`[Activity(Name = "…")]`) dà un componente stabile per `am start`: serve alle misure
  d'avvio del §7.4.
- APK Release della sonda: 16,5 MiB.

## Stato del telefono dopo le sonde

La sonda resta installata (non è Recordum). Tema scuro rimesso con `cmd uimode night yes` (prima delle prove era scuro;
se l'autore usava la programmazione automatica va riaccesa dalle Impostazioni). Rotazione automatica rimessa. Restano
all'autore: le due impronte temporanee (da togliere se non servono) e la foto di prova nell'album Recordum.
