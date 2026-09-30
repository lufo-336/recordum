# Dogfooding — il manifesto dell'autore

> Le abitudini che l'autore userà davvero, e che cosa serve perché Recordum diventi la sua app di tutti i giorni. L'onda
> F2 esce quando questa tabella è coperta **su Windows e su Linux** (`docs/ROADMAP.md`). Raccolto il 2026-09-30.

## Le abitudini

L'autore segna **"a metà"**: le cose veloci subito, il resto la sera. Userà **Windows e Linux in parti uguali**.

| Abitudine | Tipo | Gesto | Quando | Cosa serve | Onda |
|---|---|---|---|---|---|
| Routine del mattino | gruppo di azioni | spunta dalla Home | mattino, subito | azione rapida sulla riga | F1 |
| Attività generiche | azione o contatore | spunta o +1 dalla Home | subito | azione rapida sulla riga | F1 |
| Smettere di qualcosa | astinenza | Inizio / Ricaduta, orologio | subito | azione rapida, orologio vivo sulla riga | F1 |
| Evacuazioni | contatore (eventualmente con una scala, es. Bristol 1-7) | +1 dalla Home, o un valore | subito | +1 dalla Home; con la scala, il quick-add | F1 / F2 |
| Lettura | contatore (pagine o minuti), o azione | un valore o una spunta | la sera | quick-add del contatore | F2 |
| Umore | stato, scala 1-5 | slider | la sera | fascia Oggi, stato | F2 |
| Allenamento | contatore con metriche (serie, durata, peso), cronometro | quick-add a serie, timer | dopo l'allenamento o la sera | quick-add con metriche, `TimerStatusBar` | F2 |
| Diario del cibo | diario, nota veloce con foto | una nota, una foto | dopo i pasti | composer della nota veloce, import delle foto (D10) | F2 |
| Diario della giornata | diario | testo | la sera | editor del diario, almeno minimo (il controllo Live Preview c'è già) | F2 → F4 |

Non usate dall'autore: **sonno** e **acqua**. Vanno verificate lo stesso prima del pubblico (Onda 2.9), perché il
dogfooding non le eserciterà.

## Che cosa ne segue per F2

- **F1 copre già le cose veloci**: routine, attività generiche, astinenza ed evacuazioni col +1 passano dall'azione
  rapida della riga Home.
- **F2 deve portare**: fascia Oggi e quick-add per stato, contatore con metriche e scala, il cronometro; la nota veloce
  **con le foto** — e con la decisione D10 (2026-09-30): all'import le foto si **ridimensionano e si puliscono dai
  metadati** per impostazione predefinita, e l'utente può scegliere fra tre opzioni (ridimensiona e pulisci, solo pulisci,
  tieni gli originali; la scelta in Impostazioni arriva con F6, o in F2 se costa poco); un editor
  del diario almeno minimo; il percorso "ieri", perché la sera capita di recuperare il giorno prima.
- **Su tutte e due le macchine**: backup automatico e manuale, ripristino da un'altra macchina, su Linux il portachiavi
  (D3), template e builder per creare le abitudini anche lì.

## Due macchine senza sincronizzazione — decisa (D11)

**Decisione dell'autore (2026-09-30): una casa dei dati.** Una macchina tiene i dati veri per tutto il dogfooding;
l'altra serve per sviluppo e prove, con `--data-dir`. Quale delle due: *da scegliere* (conviene quella dove si segna la
sera, che è quando si registra di più).

Il perché: ogni macchina ha il suo database, e il ripristino di un `.recbak` **sostituisce**, non unisce. Se fra un
trasferimento e l'altro si segnasse su tutte e due, le registrazioni di una delle due andrebbero perse. Le strade che
erano sul tavolo:

1. **Una macchina è la casa dei dati** per tutto il dogfooding; l'altra serve per sviluppo e prove, con `--data-dir`.
2. **Ping-pong disciplinato**: a ogni cambio di macchina, backup su quella che si lascia e ripristino su quella che si
   prende. Funziona, ma un passaggio saltato costa dati.
3. **Anticipare una sincronizzazione minima** (per esempio un import da `.recbak` che unisce invece di sostituire,
   usando `updated_at_ms`): è un pezzo della Fase 6 portato avanti.

Se durante il dogfooding la casa unica stona davvero, la sincronizzazione minima è il primo candidato a essere anticipato
— lo dirà l'uso, non il piano.

## Regole dei dati durante il dogfooding

- I dati veri stanno solo nella build di `main`; le sessioni di sviluppo usano `--data-dir` o `RECORDUM_DATA_DIR`
  (e `--seed-demo` rifiuta la cartella dei dati veri).
- Backup automatico acceso dal primo giorno, più un backup manuale prima di ogni build nuova.
- Una migrazione nuova si prova prima su una copia del database vero.
- Quello che si trova usando l'app va nel tracker come `DF-*`.
