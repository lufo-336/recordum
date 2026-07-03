# Onda 5 — Potenziamento (design)

**Data**: 2026-07-03 · **Stato**: approvato dall'autore (brainstorm 2026-07-03)
**Contesto**: piano v1.0 (`2026-07-03-v1-redefinition-plan-design.md`). Onda **aggiunta su richiesta dell'autore** ("v1.0 più completa"): promuove in v1.0 le idee del parcheggio §9-bis. **Collocazione: dopo l'Onda 3, PRIMA dell'Onda 4** — la beta prova anche queste feature e la `/security-review` copre le superfici nuove (ricerca, import, hotkey globale). Exit criterion: **E19**.
**Dipendenze**: Onda 2 eseguita (fascia "Oggi", moduli per-tipo, `ui_prefs_json` v2.13); Onda 1b per la foto nel quick capture.

## Decisioni dell'autore (2026-07-03)

1. **Craving**: counter collegato + bottone nella fascia Oggi della resistance (non pura composizione).
2. **Quick capture**: mini-finestra col modulo per-tipo riusato (non solo-nota né solo-fatto).
3. (A monte: le 8 idee promosse da parcheggio a scope v1.0.)

## Schema v2.14 (una migrazione additiva)

- **`habit_config.savings_json TEXT NULL`** — config risparmio resistance (`{ "per_day": 6.5, "label": "€" }`); semantica di habit ⇒ **versionata** in `habit_config_history` (colonna mirror + snapshot).
- **Tabella virtuale FTS5** per la ricerca (contenuto esterno su `notes.body_md` + `habit_node.name`, trigger di sync su insert/update/delete). Bump `schema_version` a `2.14` + seed allineato. ⚠️ Verificare nel plan che `SQLitePCLRaw.bundle_e_sqlcipher` includa FTS5 (atteso: sì); fallback: ricerca `LIKE` con stessa UI (nessuna tabella, feature-flag interno).
- Il link craving (`companion_counter_id`) e la modalità routine vivono in **`ui_prefs_json`** (v2.13): niente colonne nuove.

## 5.1 Pacchetto resistance: risparmio + craving

- **Risparmio**: sezione dedicata nel Configura delle resistance — "Cosa risparmi ogni giorno?": valore decimale + etichetta libera (es. "€", "sigarette"; l'etichetta è testo utente, nessuna valuta hardcoded). **Fascia Oggi**: risparmio del **tentativo corrente** (`per_day × giorni del periodo aperto`, frazione di giorno inclusa, aggiornato col tick del cronometro). **Dettaglio**: totale storico (somma durate di tutti i periodi × per_day). Template Stop smoking pre-configurato (valore d'esempio modificabile).
- **Craving**: `ui_prefs_json.companion_counter_id` sulla resistance → la fascia Oggi mostra il bottone **"+ {nome counter}"** ("Ho resistito a una voglia") che registra +1 sul counter collegato senza navigare. Configura → sezione resistance: scegli un counter esistente o "creane uno" (scorciatoia al builder pre-compilato). Il template Stop smoking crea il counter già collegato. **Robustezza**: counter eliminato/archiviato → link ignorato silenziosamente (bottone sparisce); nessuna FK (è una preferenza).

## 5.2 Pacchetto motore-in-UI: vacation mode + routine

- **Vacation mode** (il motore c'è: `IStreakPauseService` Start/End/Delete/List; lo streak-engine già tratta i giorni in pausa come neutri): (a) Configura → sezione Streak: lista pause (da–a, motivo, elimina) + "Aggiungi pausa" (due date + motivo opzionale); (b) menu contestuale riga Home → "Metti in pausa…" (dialog rapido); (c) chip **"in pausa"** sulla riga Home e nella fascia Oggi quando oggi ∈ pausa attiva.
- **Routine ordinata**: toggle "Modalità routine" sul **gruppo** (in `ui_prefs_json` del nodo gruppo; UI nel Configura del gruppo). Con la modalità attiva, la **fascia Oggi del gruppo** (estensione: oggi i gruppi non ne hanno una — è l'unica fascia-gruppo della v1.0) elenca i figli in `SortOrder` con lo stato di oggi: fatti spuntati, **primo non-fatto evidenziato** con la sua azione rapida inline, progresso "3/6". Riordino = drag esistente in Home. I figli restano habit normali (composizione pura).

## 5.3 Pacchetto esplorazione: ricerca + digest

- **Ricerca globale**: icona Lucide `Search` nel ribbon → pannello risultati (stile colonna workspace): match su corpo note (diario incluso — le voci diario sono Note) e nomi habit, con snippet evidenziato; click → apre l'habit sul tab giusto (Note/Diario) . Query FTS5 con ranking; debounce input; niente ricerca nelle completions numeriche (YAGNI).
- **Digest "La tua settimana"**: voce ribbon (icona `CalendarRange`) → vista con navigatore settimane; per ogni habit attiva una riga: completamenti/7, delta streak, totale metrica primaria (con unità), giorno migliore; resistance: giorni puliti/ricadute della settimana; journal: voci scritte. Solo aggregatori/query esistenti (trend, streak, heatmap). Bottone **"Esporta settimana"** → ExportEngine con range della settimana visibile.

## 5.4 Quick capture globale

- **Hotkey di sistema** (default `Ctrl+Shift+R`, configurabile in Impostazioni → Comportamento, con rilevamento conflitti best-effort) → **mini-finestra** chromed flottante topmost: picker abitudine (pinnate prime, filtro testuale) → sotto appare **il modulo per-tipo dell'Onda 2** (Segna fatto / campi valore / nota veloce + foto / Inizio-Ricaduta col cronometro). Esc chiude; dopo la registrazione, conferma breve e chiusura.
- Registrazione hotkey via `RegisterHotKey` Win32 (helper isolato stile `IScreenColorPicker`); funziona con app in esecuzione anche minimizzata; **sinergia** col tray (Onda 4): insieme = cattura sempre disponibile. Nessuna dipendenza dura sul tray.

## 5.5 Import CSV

- **Formato = quello dell'export CSV** (`habit,date,value,metric,retroactive` — simmetria dichiarata: ciò che Recordum esporta, Recordum reimporta). Encoding UTF-8, date ISO.
- Punti d'ingresso: Impostazioni → Dati → "Importa da CSV…" (mappa la colonna `habit` su habit esistenti per nome, con risoluzione manuale per i non trovati: crea counter/action o salta) e menu contestuale habit → "Importa registrazioni…" (tutte le righe su quell'habit, colonna `habit` ignorata).
- **Anteprima obbligatoria** prima di applicare (n righe valide, errori riga-per-riga con motivo, nulla scritto finché non confermi); tutte le righe importate marcate `IsRetroactive = true`; import in transazione (tutto o niente per habit). Limiti: valori→metrica primaria; multi-metrica/serie → 1.x (documentato). Loop Habit Tracker e altri formati → 1.x.

## 6. Ordine di esecuzione (plan separati, scritti a ridosso)

**5.0 migrazione v2.14** → **5.1** resistance pack → **5.2** vacation+routine → **5.3** ricerca+digest → **5.5** import → **5.4** quick capture (per ultimo: riusa più componenti di tutti).

## 7. Test

- Data: migrazione v2.14 (savings mirror in history, FTS + trigger sync, fallback flag); calcolo risparmio (periodo aperto con frazioni, totale storico); import (parser, anteprima, errori riga, transazionalità, mapping nomi); query digest.
- WPF: fascia Oggi resistance (risparmio + bottone craving, link decaduto); pause UI (lista/aggiungi/chip); routine (ordine, primo-non-fatto, progresso); pannello ricerca (risultati, navigazione); digest (righe per-tipo, cambio settimana); quick capture (picker, modulo per-tipo, hotkey registrata/conflitto — test VM-only per il Win32).
- Verifica visiva autore su tutte e otto (E19).

## 8. Fuori scope (esplicito)

Valute/multi-currency reali (etichetta libera basta); ricerca nelle completions e nei valori; digest mensile/annuale; import multi-metrica/serie e formati terzi (Loop → 1.x); quick capture ad app chiusa (richiede il tray dell'Onda 4 o un servizio — la combinazione arriva gratis dopo la 4); statistiche craving dedicate (il counter è un counter: heatmap/trend già suoi).
