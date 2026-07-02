# Recordum/.claude

> Vedi [`../AGENTS.md`](../AGENTS.md) per le regole globali del progetto.

## Ambito

Questa directory contiene la configurazione dell'ambiente Claude Code per il progetto Recordum: guardrail hook, permessi, e skill personalizzati.

## Struttura

| Path | Contenuto |
|---|---|
| `settings.json` | Configurazione permessi e hook lifecycle |
| `hooks/guard-main.js` | Blocca commit/push diretti su branch `main` |
| `hooks/guard-generated.js` | Blocca edit dei file EF generati (`*.Designer.cs`, `*ModelSnapshot.cs`) |
| `hooks/guard-userdata.js` | Chiede conferma prima di cancellare dati utente (`%LocalAppData%\Recordum`, `.recbak`) |
| `hooks/stop-dirty.js` | Promemoria working tree sporco a fine sessione |
| `skills/run/SKILL.md` | Skill per build e lancio di Recordum.WPF |

## Regole operative

### Hook system

I guardrail hook sono eseguiti automaticamente da Claude Code ad ogni tool use e garantiscono:

1. **`guard-main.js`** (PreToolUse: Bash|PowerShell) — Impedisce commit/push su main.
2. **`guard-userdata.js`** (PreToolUse: Bash|PowerShell) — Conferma prima di cancellare dati utente.
3. **`guard-generated.js`** (PreToolUse: Edit|Write|MultiEdit) — Impedisce edit manuale di file EF generati.
4. **`stop-dirty.js`** (Stop) — Avvisa se il working tree è sporco a fine sessione.

### Gestione hook

- Gli hook si gestiscono con il comando `/hooks`.
- L'hook `guard-generated.js` matcha **qualsiasi** `*.Designer.cs`, incluso `Strings.Designer.cs` (RESX, non EF). Per editare quel file servono strumenti shell.
- Il plugin **`superpowers`** (pin v6.0.3) è necessario per il flusso `docs/superpowers/*` e va installato per-macchina.
- Le impostazioni machine-local (plugin, memorie, `settings.local.json`) NON viaggiano via git. Vedi `docs/dev-environment-sync.md` per la checklist di allineamento.

### Modifiche consentite

- `settings.json` — modificabile per aggiornare permessi o hook.
- `skills/*/SKILL.md` — modificabile per aggiornare skill.
- `hooks/*.js` — modificabile per aggiornare guardrail.
- Non eliminare hook senza valida ragione.

## Verifica tipica

1. Verificare che gli hook siano presenti e funzionanti con un commit test su branch non-main
2. `node .claude/hooks/guard-main.js` — deve uscire con 0 solo se non si è su main
3. Se il plugin `superpowers` non è installato, il flusso `docs/superpowers/*` non funziona

## Indice AGENTS.md

- Vedi `../AGENTS.md`
