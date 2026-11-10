# Sonda Android — 2026-11-10

**Domanda**: la v1.0 può comprendere un APK (decisione dell'autore del 2026-11-10)? Quanto del codice passa così com'è, e
dove sta il lavoro? **Risposta breve**: sì. Dominio, dati cifrati, ViewModel e viste passano senza modifiche; il lavoro è
la cornice da telefono, i servizi di piattaforma e il tempo d'avvio.

**Come**: branch locale `sonda-android` (non pushato, da buttare), `spikes/android/`. Un'app `net10.0-android` che compila
**i sorgenti di `Recordum.UI` così come sono** (stesso nome di assembly, così gli `avares://` restano validi), con un punto
d'ingresso e un `App.axaml.cs` suoi. Avvio vero: opzioni, lock, sblocco, contenitore, migrazioni, dati d'esempio, Home.
**Dispositivo**: Samsung Galaxy S21 5G (SM-G991B), Android 15, arm64, 8 core. Build Release, trimming dell'SDK, senza AOT.

## Misure

| Cosa | Risultato |
|---|---|
| Cifratura | SQLite 3.54.0 con SQLite3MC 2.6.1 caricato; file su disco cifrato (intestazione non leggibile) |
| Sblocco con passphrase (Argon2id 64 MiB × 4, p=2) | 936 ms alla creazione, 1074 ms allo sblocco |
| Contenitore dei servizi | 41 ms |
| Inizializzazione del database | 4,0 s al primo avvio (tutte le migrazioni); **3,1 s agli avvii dopo**, senza niente da migrare |
| Home: ViewModel caricato / a schermo | 913 ms / 687 ms |
| **Dall'avvio alla Home** (secondo avvio) | **≈ 6 s**, più l'avvio del runtime |
| `DataDirectoryLock` | funziona |
| APK | 20,8 MiB (Release, senza AOT); 44 MiB in Debug |
| Toolchain | workload `android` 36.1.69, SDK `android-36`, JDK 25: compila |

## Cosa passa così com'è

- `Recordum.Core`, `.Data`, `.Presentation`, `.Plugins`: nessuna modifica. EF Core e le migrazioni reggono il trimming
  dell'SDK.
- Tutte le viste di `Recordum.UI` compilano per Android: l'unico file con API da desktop è `Program.cs`.
- La Home vera a larghezza di telefono: tema, Inter, righe tinte, strisce, log rapido, orologio dell'astinenza che scorre.
  La schermata del gruppo e dell'abitudine si disegna (testa, numeri, colonne, Panoramica). Schermate in
  `docs/design/2026-11-10-android-probe/`.
- L'editor del diario (AvaloniaEdit) apre la tastiera e riceve il testo.

## Cosa ha trovato

1. **L'avvio sul thread dell'interfaccia blocca: ANR.** `AppComposition.InitializeAsync` e il seed girano per secondi
   sul thread UI; sul desktop non si vede perché la finestra non c'è ancora, Android dopo 10 s dichiara l'app bloccata.
   Sul telefono l'avvio va fuori dal thread UI, dietro una schermata che dice cosa succede.
2. **Il tempo d'avvio è il rischio vero.** 3 s di inizializzazione del database a ogni avvio (modello EF costruito a
   freddo, senza AOT) più 1 s di Argon2id. Il criterio "avvio sotto i 3 s" sul telefono chiede: modello EF compilato,
   AOT con profilo, sblocco dal portachiavi di Android (salta Argon2id), Home mostrata prima del resto. Da misurare
   di nuovo dopo ognuno.
3. **Il lifetime.** `AvaloniaShellServices` fa un cast secco a `IControlledApplicationLifetime`:
   `InvalidCastException` su Android (il commento lì accanto dice che tornerebbe null: non è così).
4. **Tutto ciò che è una finestra va rifatto come vista.** Nove servizi aprono finestre: selettore dei modelli,
   creatore, Impostazioni, backup, conferma, tre dialog di file, apri cartella. Su Android c'è una vista sola. Sono già
   astrazioni con due implementazioni: serve la terza.
5. **Il portachiavi.** `OperatingSystem.IsLinux()` è falso su Android: oggi esce `NullSecretStore`. Serve un
   `ISecretStore` sull'Android Keystore, con l'impronta.
6. **La tastiera nell'editor.** Coi tasti il testo entra, ma i suggerimenti della tastiera Samsung sono diversi da quelli
   di un `TextBox` (dopo "ciao": "iao / io / iso" contro "ciao / ciaoooo / ciaone"): l'editor non passa alla tastiera il
   testo intorno al cursore. Correzione automatica, suggerimenti e scrittura a scorrimento vanno provati a mano
   dall'autore; se non reggono, sul telefono il diario usa un `TextBox` con la stessa sintassi.
7. **Dimensioni da desktop.** Nel pannello del gruppo la striscia schiaccia i nomi ("Di…", "Le…"); i comandi delle
   colonne e i bersagli sono pensati per il mouse.
8. **Build.** Gli using impliciti di Android si scontrano con Avalonia (`Application`, `Button`); un `-r` da riga di
   comando arriva ai progetti condivisi (serve togliere `RuntimeIdentifier` dalle `ProjectReference`); un APK di debug
   pesa il doppio.

## Non provato

PDF (QuestPDF) e grafici su Android — proposta: restano sul computer; fotocamera e selettore di foto; notifiche e timer in
background; Keystore e impronta; selettori di file e cartelle (SAF); firma dell'APK; CI.

## Che struttura ne segue

- `Recordum.UI` diventa una **libreria** di viste, controlli e temi (`net10.0`); due teste sottili:
  `Recordum.Desktop` (finestre, `Program`, i launcher di oggi) e `Recordum.Android` (activity, cornice a vista unica,
  servizi di Android). Il trucco della sonda (gli stessi sorgenti compilati due volte) prova che le viste sono portabili,
  non è la struttura da tenere.
- I launcher a finestra diventano navigazione: sul telefono ogni "finestra" è una pagina o un foglio dal basso.
- L'avvio diventa una sequenza unica condivisa, con i passi lenti fuori dal thread UI.

## Sincronizzazione (dalla conversazione del 2026-11-10)

L'autore usa Google Drive con **DriveSync** sul telefono (come per Obsidian): una cartella locale tenuta allineata. Il
database vivo **non** va in quella cartella (un SQLite sincronizzato a metà si rovina, e su Android Drive non è una
cartella vera). Disegno: ogni dispositivo tiene il database in locale e scrive nella cartella di sincronizzazione **il
proprio** file cifrato; legge quello dell'altro e **unisce**. Metà esiste già (backup automatico con cartella a scelta);
manca l'"unisci", con le lapidi per le cancellazioni. Spec a sé.
