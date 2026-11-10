# Checklist a schermo dell'autore

> Tutto ciò che solo una persona davanti allo schermo può verificare, in un file solo (prima era sparso nella
> ROADMAP). Le voci sono quelle scritte dalle onde, **non riscritte**: l'ordine delle sezioni dice la priorità.
> Si spunta qui; un difetto trovato va nel tracker (`docs/phase-4-issues.md`).
>
> **Finché la sezione 1 non è fatta, la voce Recordum del menu apre la cartella dati vera: non avviarla.**
> Le sezioni 3–5 riguardano schermate che le onde "Giornata" e 3b cambieranno: si guardano dopo quelle onde, sulla
> versione che si userà.

## 1. Prima dei dati veri — F2d-1, su una COPIA della cartella dati (`--data-dir`)

  - [ ] (Ruling 6) **primo avvio su una COPIA della cartella dati vera** (`--data-dir`, mai l'originale): le foto esistenti
    entrano nel DB, `attachments/` non ha più file, le voci vecchie mostrano le loro foto; **cronometrare il primo avvio**;
    backup e ripristino della copia; `ls -lR` della cartella non mostra nessuna immagine;
  - [ ] nota veloce dal diario del Cibo e da un diario normale;
  - [ ] `#` con suggerimenti (mostrati come `#nome`) e «Crea «#…»»; scegliere un suggerimento con un **vero clic del MOUSE**
    lo inserisce (nei test c'è solo un tap sintetico);
  - [ ] la riga Tag (provare un nome con spazi: rifiutato);
  - [ ] una foto dal telefono esce dritta e ridimensionata; un HEIC è rifiutato con un messaggio che nomina il file; un
    panorama molto grande o una foto da 200 MP dà il messaggio «troppi pixel»;
  - [ ] una voce di sole foto, poi modificarne solo i tag;
  - [ ] modifica con una foto tolta, poi Annulla;
  - [ ] le tre scelte D10 in «Dati e backup»;
  - [ ] l'ora delle voci è l'ora locale;
  - [ ] nessuna immagine leggibile nella cartella dati;

## 2. Prima dei dati veri — F2c su GNOME (portachiavi, backup, ripristino)

- **Checklist a schermo dell'autore** (GNOME per tutte; Windows per F1; la lista D7 è nel piano F1): F1 (finestra, tab,
  trascinamento, barra di sistema); F2a (i quattro tipi dalla colonna "Oggi" e dal quick-add, "ieri" vicino al reset, timer
  dopo riavvio, separatore decimale, chiaro e scuro, rinomina e cancellazione); F2b (creare da tutti e otto i template e dal
  builder, configurare ogni sezione, finestra stretta, **W2V-16** e **W2V-18**); **F2c su GNOME**: (1) primo avvio con
  "Ricorda su questo dispositivo"; (2) riavvio senza passphrase; (3) Seahorse mostra le due voci "Recordum …" con
  l'etichetta giusta; (4) "Dimentica su questo dispositivo" e riavvio con la passphrase; (5) backup automatico acceso con la sua
  passphrase, e spento (con l'avviso nella pagina) se la si cancella in Seahorse; "Sfoglia…" apre il selettore di
  cartella del sistema e "Salva" dice l'esito; con Seahorse bloccato e il prompt annullato la pagina dice "portachiavi non
  raggiungibile" e il backup automatico resta acceso; nello storico "Apri cartella" apre il file manager ed "Elimina" toglie la riga; (6) "Backup ora" e "Ripristina…" dalle
  Impostazioni; (7) un `.recbak` della VM Windows ripristinato su Linux; (8) `ls -l` della cartella dati: i tre file
  del database sono 0600.

## 3. Onda 3a — selettore dei modelli e creatore (GNOME, in italiano e in inglese dove detto)

  - [ ] il selettore: "Da zero" e "Gruppo vuoto"; la ricerca in italiano ("sonno", "umore", "diario", con e senza accenti) e in
    inglese; frecce e Invio fra le schede; un doppio clic su "Usa questo modello" crea **una** abitudine;
  - [ ] **"I tuoi modelli"**: Rinomina ed Elimina dal ⋮ col mouse e con la tastiera — **Invio su Rinomina o Elimina agisce sul
    menu e non esegue mai "Usa"** (UX3A-4, in headless non si prova);
  - [ ] il creatore da **ciascuno degli otto modelli** con "Personalizza…": nome, campi, serie, scale e unità in italiano,
    l'anteprima a destra, "Crea abitudine"; poi "Personalizza…" e Annulla: nessuna abitudine né modello;
  - [ ] il creatore da zero: le quattro tessere (spente con la ragione), unità e scala nuove, Avanzate, "Salva come modello"
    con "Salvato ✓", la finestra stretta con l'anteprima sotto;
  - [ ] Configura di uno stato: le etichette della scala come elenco di campi; cambiare scala: le etichette
    nuove nella lingua di adesso, le stesse in Configura e in testa alla schermata (UX3A-2);
  - [ ] cambiare lingua dopo aver creato: l'abitudine resta nella lingua di quando è nata; chiaro e scuro.

## 4. F2d-2 — editor del diario della sera (GNOME; gesto Diario)

  - [ ] diario della sera con un titolo, un elenco (Invio che continua, Invio su voce vuota che chiude), una citazione,
    Ctrl+B e Ctrl+I con e senza selezione;
  - [ ] `#` con la tastiera (frecce, Invio che sceglie senza andare a capo) e con un **vero clic del mouse**;
  - [ ] "Inserisci immagine…": la miniatura appare nel testo fuori dalla riga del cursore; scrivere a lungo: la colonna segue
    il cursore;
  - [ ] Salva, poi rilettura dalla Cronologia: chip dei tag e "[foto]";
  - [ ] Modifica di una voce, Annulla (torna la bozza di prima); Modifica con cambi, poi un'altra voce o un altro giorno: chiede;
  - [ ] chiudere la finestra a metà frase e riaprire: la bozza torna; `kill -9` a metà frase (anche durante una Modifica) e
    riaprire: torna anche la modifica;
  - [ ] ieri dal calendario: le voci di ieri rese con le immagini; una voce nuova scritta lì va su ieri;
  - [ ] chiaro e scuro;
  - [ ] **export** (F2d2-8: su Avalonia arriva con F6): creare la voce con un'immagine sull'app Avalonia della VM Windows ed
    esportare da WPF sulla stessa cartella dati — Markdown aperto in Obsidian con le immagini, HTML con l'immagine al suo
    posto; oppure rimandare questi due punti a F6;

## 5. Telefono — dalla sonda del 2026-11-10 (`docs/design/2026-11-10-android-probe.md`)

  - [x] il database cifrato si apre sul Galaxy S21; la Home si disegna;
  - [x] l'editor del diario con la correzione automatica: **non regge** (il diario del telefono usa un campo di testo);
  - [ ] da rifare a ogni onda del telefono: tempo dall'icona alla Home, tastiera, fotocamera, impronta.
