# Recordum — cos'è

> *Record your existence.*
>
> Questo documento dice **perché** esiste Recordum e **per chi**. È la fonte per ogni domanda di prodotto. Il *come* sta in
> `CLAUDE.md` e in `docs/ARCHITECTURE.md`, il *dove siamo* in `docs/ROADMAP.md`. Prende il posto dei `.docx` di riferimento
> (ProductSpec v2 e schemi), che non sono nel repository e descrivono ancora lo schema 2.2.
>
> Descrizione confermata dall'autore il 2026-09-30.

## La promessa

Recordum è un registro della propria vita per chi vuole *capirla*, non gamificarla. Funziona offline e cifrato, senza
account: i dati restano sul computer. Non riduce tutto a "fatto / non fatto": cinque tipi di abitudine si compongono in
gruppi, così chi traccia salute, allenamento, scrittura o una dipendenza trova la forma giusta invece di una casella da
spuntare. Si usa in trenta secondi, e la profondità c'è per chi la cerca. Un template è solo una composizione salvata:
quello che fa un template, l'utente può farlo da solo.

## Per chi

- **Chi tiene d'occhio la salute** — sintomi, farmaci, sonno, umore: vuole uno storico di cui fidarsi e un PDF da portare
  al medico.
- **Chi si allena o scrive** — serie, durate, parole, obiettivi settimanali: vuole vedere i progressi.
- **Chi smette di fare qualcosa** — fumo, alcol, un'abitudine che fa male: vuole l'orologio che scorre e un posto dove
  segnare una ricaduta senza giudizio.
- **Chi tiene un diario** — com'è andata la giornata, cosa ha mangiato, con le foto.
- **Chi arriva da Obsidian e dalla privacy** — lavora con la tastiera, vuole i dati in locale, non accetta un cloud
  obbligatorio.

Il primo utente è l'autore: Recordum deve diventare la sua app di tutti i giorni prima di diventare quella di altri.

## La giornata di chi lo usa

- **Ogni giorno**, una-tre volte: apre l'app, segna in pochi secondi dalla Home (una spunta, un +1, uno sguardo
  all'orologio dell'astinenza) o dalla fascia *Oggi* (una durata, una quantità, uno stato), scrive una nota o il diario.
  Segna spesso **ieri**: sul computer la vita si registra dopo averla vissuta, quindi il giorno prima deve costare quanto
  oggi.
- **Ogni settimana**: guarda indietro — heatmap, streak, obiettivi, andamento. Senza gamification, la ricompensa è vedere
  la propria costanza.
- **Ogni tanto**: crea o ritocca un'abitudine (tante volte nella prima settimana, poi di rado), esporta un PDF o un CSV,
  controlla i backup.
- **Quasi mai**: ripristina un backup — ed è il momento in cui l'app non può sbagliare.

## I tipi, e la composizione

- **Azione** — si fa o non si fa, con un programma, uno streak, un obiettivo.
- **Contatore** — una quantità: volte, minuti, millilitri, serie; anche a sessioni con segmenti (lavoro, pausa).
- **Astinenza** — un periodo che dura finché non arriva una ricaduta: l'orologio corre.
- **Diario** — testo e foto, come diario della giornata o come nota veloce.
- **Stato** — un valore su una scala: umore da 1 a 5, dolore, energia.

I **gruppi** raccolgono abitudini e possono avere un obiettivo proprio. Le esigenze complesse si risolvono componendo
abitudini semplici, mai gonfiando la singola abitudine di opzioni. Ogni composizione dell'utente si può salvare come
template, e ogni template ufficiale si può ricostruire a mano nel builder.

## Cosa Recordum non è

- **Non è un servizio**: niente account, niente server nostro. I dati stanno sul dispositivo, cifrati. La
  sincronizzazione arriverà attraverso un cloud dell'utente (Dropbox, Drive, OneDrive, iCloud).
- **Non è un gioco**: niente badge, punti, classifiche, frasi motivazionali, animazioni che premiano.
- **Non è una lista di cose da fare**, e non riduce un'abitudine a una casella.
- **Non è un dispositivo medico**: registra, non diagnostica.
- **Nella v1.0 non c'è** intelligenza artificiale, non c'è l'app per il telefono, non c'è la versione web.

## Come si presenta

Serio ma caldo. Prima la tipografia e lo spazio; un serif (Source Serif 4) per il diario e per il marchio. Il colore
identifica un'abitudine — la barra a sinistra della riga, la heatmap — e non decora. Icone a linea (Lucide), mai emoji
nell'interfaccia (l'utente nei suoi testi scrive ciò che vuole). Tema chiaro e scuro curati allo stesso modo. Movimento
contenuto. La profondità è nascosta finché non serve, mai tolta.

## La v1.0

È l'app che uno sconosciuto scarica da GitHub su Windows o Linux e usa ogni giorno senza di noi:

- registra in pochi secondi con i cinque tipi, oggi e nei giorni passati;
- mostra lo storico: heatmap, streak, obiettivi, grafici del Dettaglio;
- tiene un diario con le foto (ridimensionate, senza la posizione nascosta nei metadati);
- fa backup cifrati da sola e a richiesta, e ripristina anche su un altro computer;
- esporta un PDF da portare al medico, un CSV per i fogli di calcolo (e HTML, Markdown, JSON);
- compone qualunque abitudine nel builder, con un'anteprima fedele, e offre template pronti;
- parla inglese e italiano, ha tema chiaro e scuro, impostazioni complete.

Si rilascia quando uno sconosciuto la installa e la usa senza aiuto su una macchina pulita, quando l'autore l'ha usata
ogni giorno per almeno quattro settimane, e quando la revisione di sicurezza non lascia problemi gravi aperti. I criteri
dettagliati stanno in `docs/ROADMAP.md`.

## Dopo la v1.0

Senza date, prima la v1.0: l'app per il telefono dallo stesso codice di interfaccia (Avalonia), con il backup `.recbak`
come ponte fra i dispositivi; la sincronizzazione attraverso il cloud dell'utente; plugin e marketplace; la versione web;
statistiche e correlazioni, anche con l'AI, solo con consenso esplicito e con l'AI spenta al primo avvio.
