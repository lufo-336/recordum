# Recordum — Plugin System Design Spec

> **Status:** Draft for review · **Date:** 2026-05-13 (brainstorming) → 2026-05-14 (spec) · **Author:** ITS Web Solutions Architect · **Reviewer:** —
>
> Documento di design del sistema di estendibilità di Recordum. Output del brainstorming via `superpowers:brainstorming` del 2026-05-13. Tutte le decisioni macro sono approvate; resta da scrivere il *plan* di implementazione via `superpowers:writing-plans` solo dopo che questo spec è stato rivisto e accettato.
>
> **Scope di precedenza:** questo spec descrive un *layer aggiuntivo* sopra l'architettura di Fase 3 già fissata in `CLAUDE.md`. In caso di conflitto con il `CLAUDE.md` di progetto o con `Recordum_DatabaseSchema_v2_2.docx`, **vincono questi ultimi**, salvo l'addendum esplicito proposto in §2.4 (4 colonne `created_by_plugin_id` additive per provenance) che andrà confermato come `v2.3` o equivalente prima del freeze.

---

## 0. Sintesi (TL;DR)

Recordum espone un sistema di plugin in due fasi:

- **Fase 3 — Foundation API.** Si committano contratti C# (`Recordum.Plugins.Contracts`), un manifest schema, types TypeScript, e un *in-process host* fidato. Le feature first-party (Chart Engine, export, dashboard widget, importer) sono i primi consumer dell'API e validano il design *prima* che qualunque codice di terze parti tocchi il sistema.
- **Fase 7 — External Runtime.** Si aggiunge un secondo `IPluginHost` basato su `WebView2` isolato, manifest parser + signing, marketplace UI, sandbox, sideloading. Il codice first-party non viene toccato: continua a girare sullo stesso contratto.

Il modello segue Obsidian per **filosofia distributiva** (registry pubblico, badge non-gating, OSS opzionale) e VS Code per **rigore del contratto** (API tipizzata, extension points dichiarati, permessi manifest-based).

**Sandbox runtime:** JS/TS in una `CoreWebView2Environment` isolata per plugin, custom scheme `recordum-plugin://`, CSP costruita dal manifest, IPC JSON-RPC su `postMessage`, no `window.fetch` globale.

**Modello privacy:** sandbox + permessi dichiarati nel manifest + runtime prompts per azioni sensibili. Il `PermissionBroker` filtra automaticamente i record secondo `ai_data_sharing` con cascata a **tre soglie** (none / local_only / api_allowed) in base ai permessi `ai:local` e `network` del plugin chiamante (§4.6). Nessuna scorciatoia tipo "tutti i plugin vedono tutto, l'utente legge il warning".

**Scope dei plugin:** 6 categorie di capability — visualizzazioni, processori dati, scale/metriche custom, automazioni, sync provider, estensione UI generica stile Obsidian.

**Provenance dei dati:** 4 colonne `created_by_plugin_id` additive sullo schema su entità top-level (HabitNode, HabitTemplate, ExportTemplate, Chart) per uninstall hygiene e attribuzione. Richiede addendum v2.3 allo schema.

**Cross-cutting sigillato qui:** crittografia at-rest del DB via SQLCipher (chiave DPAPI baseline + upgrade opzionale a passphrase) — vedi §11. Tocca `Recordum.Data`, non solo plugin.

---

## 1. Decisioni consolidate

Le 7 decisioni macro approvate durante il brainstorming. Costituiscono le invarianti del design: ogni dettaglio successivo deve essere coerente con esse.

| # | Decisione | Conseguenza |
|---|---|---|
| 1 | Scope plugin = tutte e 6 le categorie | API deve essere abbastanza generale per coprirle, non scoping a "solo viewer" o "solo provider". |
| 2 | Recordum core resta OSS | Niente API "premium" lato core; niente paywall su capability essenziali. |
| 3 | Distribuzione = registry aperto stile Obsidian, non gating | Niente review umana di routine; badge non-gating; sideloading permesso behind developer mode. |
| 4 | Privacy = sandbox + manifest permessi + runtime prompts | Implementazione tecnica richiesta. Non basta un warning testuale all'install. |
| 5 | OSS dei plugin opzionale, con badge | Closed-source ammessi ma marcati. Open Source / Verified / Community Pick sono badge informativi. |
| 6 | Runtime = JS/TS in WebView2 | WASM scartato (UI plugin richiederebbero comunque WebView). C# scripting scartato (.NET non ha sandbox in-process credibile). |
| 7 | Timing = API scheletro in Fase 3 + Chart Engine come consumer pilota; runtime esterno in Fase 7 | Fase 3 non spedisce un runtime di plugin di terze parti — spedisce il contratto e la sua prima implementazione first-party. |
| 8 | Provenance dei dati = colonna `created_by_plugin_id` solo su entità top-level (HabitNode, HabitTemplate, ExportTemplate, Chart) | Tocca lo schema: 4 colonne additive nella `InitialSchema` migration (vedi §2.4). Child events (Completion, Note, Goal) ereditano dal parent. |
| 9 | AI sharing filter a tre soglie con permesso `ai:local` esplicito | Onora granularmente `ai_data_sharing ∈ {none, local_only, api_allowed}`. Plugin senza permessi AI/network → vede tutto. Plugin con `ai:local` → vede `local_only`+`api_allowed`. Plugin con `network` → vede solo `api_allowed`. |
| 10 | Crittografia at-rest = SQLCipher con chiave DPAPI baseline + upgrade opzionale a passphrase | Baseline transparente sempre on; passphrase opt-in da Settings → Privacy. Decisione **cross-cutting** che eccede lo scope plugin — vedi §11. |
| 11 | Backup completo include `.rmpk` referenziati per id+version + permission state + plugin storage scope | Restore non rompe l'esperienza utente. I binari `.rmpk` referenziati, non re-imballati nel backup (peso). |
| 12 | Supply chain: `package-lock.json` obbligatorio nel `.rmpk` + SBOM derivato dal CI + check su advisory list | Mitigazione contro deps npm compromesse. CI rifiuta submission con deps in `revoked-deps.json` del registry. |
| 13 | Performance budget: warning UI a partire da N=8 plugin attivi concurrent, hard cap N=16 | Limite esplicito invece di "lentezza generica". Oltre soft cap → toast informativo. Oltre hard cap → disable forzato dei meno recenti. |

---

## 2. Architettura

### 2.1 Strati

```
┌───────────────────────────────────────────────────────────────┐
│                          Plugin                                │
│  JS/TS in WebView2 isolata · pacchetto .rmpk (zip)             │
│  ─ manifest.json · main.js · locales/ · assets/                │
└────────────┬──────────────────────────────────────────────────┘
             │  IPC JSON-RPC over postMessage
             │  custom scheme recordum-plugin://
┌────────────▼──────────────────────────────────────────────────┐
│                       Plugin Host                              │
│  Nuovo progetto Recordum.Plugins                               │
│  ─ PluginManager (lifecycle: install→enable→run→update→…)      │
│  ─ ManifestParser + ManifestValidator (JSON Schema)            │
│  ─ PermissionBroker (manifest + runtime prompts + filtro AI)   │
│  ─ ApiRouter (recordum.habits.* → IHabitService)               │
│  ─ ExtensionPointRegistry (dashboard.widget, chart.renderer…)  │
│  ─ IPluginHost: { InProcess | WebView2 } (DI swap)             │
│  ─ EventBus (habit.created, completion.recorded…)              │
└────────────┬──────────────────────────────────────────────────┘
             │  IHabitService, ICompletionService, INoteService…
┌────────────▼──────────────────────────────────────────────────┐
│                   Recordum.Core + Recordum.Data                │
│  Invariati. Plugin layer è additivo.                           │
└───────────────────────────────────────────────────────────────┘
```

### 2.2 Progetti aggiunti alla solution

| Progetto | Responsabilità | Referenzia |
|---|---|---|
| **Recordum.Plugins.Contracts** | Interfacce, DTO, enum di permessi, extension point IDs, schema manifest C#. Nessuna logica. | `Recordum.Core` |
| **Recordum.Plugins** | Implementazione host: PluginManager, PermissionBroker, ApiRouter, ExtensionPointRegistry, EventBus, InProcessPluginHost. WebView2PluginHost arriva in Fase 7. | `Recordum.Core`, `Recordum.Plugins.Contracts` |
| **tools/plugin-api-ts/** | Pacchetto TypeScript `@recordum/plugin-api`. Solo type defs in Fase 3, generate dai contratti C#. Pubblicazione su npm in Fase 7. | — |

`Recordum.WPF` referenzia `Recordum.Plugins` solo per registrare extension points UI e per la futura settings panel di gestione plugin (Fase 7). In Fase 3 la dipendenza esiste ma non c'è UI dedicata.

### 2.3 Contratto di comunicazione

Single source of truth è C# (interfacce in `Recordum.Plugins.Contracts`). Un build step genera:

- `manifest.schema.json` canonico (committato).
- `@recordum/plugin-api/index.d.ts` (committato in `tools/plugin-api-ts/`).

Il pacchetto npm in Fase 3 esiste solo come cartella nel repo, **non pubblicato**. La pubblicazione su npm avviene in Fase 7.

### 2.4 Schema delta per provenance

L'opzione presa al §1.8 introduce **4 colonne additive** sullo schema v2.2. Vanno incluse nella `InitialSchema` migration di Milestone 3.1 — ancora editabile prima del primo `dotnet ef migrations add`. Conforme alla regola CLAUDE.md §12.2 ("aggiunte di colonna in Fase 3 devono passare per la migrazione iniziale se ancora editabile").

| Tabella | Colonna | Tipo | Note |
|---|---|---|---|
| `habit_node` | `created_by_plugin_id` | `TEXT NULL` | Reverse-DNS plugin id che ha creato il nodo. NULL = creato dall'utente o da Recordum core. |
| `habit_template` | `created_by_plugin_id` | `TEXT NULL` | Idem. |
| `export_template` | `created_by_plugin_id` | `TEXT NULL` | Idem. |
| `chart` | `created_by_plugin_id` | `TEXT NULL` | Idem. |

**FK soft** (no constraint a livello DB): il plugin id è una stringa libera; se il plugin viene disinstallato la colonna resta valorizzata come "traccia storica". Niente cascade.

**Indici:** non necessari di default — uso atteso è scan filtrato su singolo `plugin_id`, query rara. Aggiungere `INDEX habit_node_created_by_plugin_id` solo se profilato necessario.

**Child events (Completion, Note, Goal):** non hanno `created_by_plugin_id`. Provenance derivata via JOIN al parent HabitNode.

**Aggiornamento allo schema doc:** richiederà un addendum `Recordum_DatabaseSchema_v2_3.docx` (o equivalente) prima del freeze finale dello schema. Da tracciare come task.

---

## 3. Struttura del package plugin

### 3.1 Estensione e formato

**Estensione file:** `.rmpk` (Recordum Package). Zip standard. Verificare al momento dell'implementazione se la convenzione finale è `.rmpk` o `.rpkg` — la decisione del brainstorming è `.rmpk` ma potrebbe essere refuso.

Il formato può ospitare anche **template di habit** e **temi** in futuro, distinti dal campo `"type"` nel manifest (`"plugin" | "template" | "theme"`). Spec attuale copre solo `"plugin"`.

### 3.2 Contenuto canonico

```
mio-plugin.rmpk (zip)
├── manifest.json            (obbligatorio)
├── main.js                  (entry point, ES2020)
├── icon.svg                 (opzionale, 256x256 raccomandato)
├── README.md                (opzionale)
├── LICENSE                  (opzionale ma necessario per badge OSS)
├── locales/
│   ├── en.json
│   └── it.json
└── assets/
    └── …
```

### 3.3 Manifest

```jsonc
{
  "schema_version": "1.0",
  "id": "io.recordum.chart-engine",        // reverse-DNS, unico nel registry
  "name": "Chart Engine",
  "version": "1.2.3",                      // semver
  "type": "plugin",                        // plugin | template | theme
  "publisher": {
    "id": "recordum-official",
    "display_name": "Recordum",
    "url": "https://github.com/recordum"
  },
  "min_recordum_version": "1.0.0",
  "description_key": "plugin.chart_engine.description",  // chiave in locales/
  "icon": "icon.svg",
  "entry": "main.js",
  "permissions": {
    "data": ["read:habit-nodes", "read:completions", "read:goals"],
    "network": [],                         // host whitelist
    "notifications": false,
    "filesystem": "none"                   // none | plugin-storage | attachments-read | user-pick
  },
  "extension_points": ["chart.renderer", "habit.detail.tab"],
  "source_url": "https://github.com/recordum/chart-engine",  // serve per badge OSS
  "supported_locales": ["en", "it"]
}
```

Validazione: `manifest.schema.json` (JSON Schema 2020-12). Validazione fallita = install rifiutato.

### 3.4 Lifecycle

| Stato | Trigger | Effetto |
|---|---|---|
| **Install** | User installa da marketplace o sideload | Estratto in `%LocalAppData%/Recordum/plugins/<id>/`. Stato iniziale: **disabled**. Permessi mostrati ma non ancora attivi. |
| **Enable** | User clicca "Enable" e accetta i permessi | PluginManager carica nel host. Permessi attivi. EventBus subscription attive. |
| **Runtime** | Plugin attivo durante la sessione | Risponde a chiamate API e ad eventi. Sandbox enforced. |
| **Update** | Nuova versione disponibile | Permission diff: se i permessi sono uguali → silent auto-update. Se nuovi permessi → prompt manuale di re-consent. **Storage migration**: prima dell'enable della nuova versione, host chiama `plugin.onUpgrade(prevVersion, newVersion)` (se il plugin esporta l'hook) per permettere la migrazione dello schema interno di `recordum.storage`. Timeout 10s, fallimento → rollback alla versione precedente con dialog di errore. |
| **Disable** | User clicca "Disable" | Sandbox terminata. Storage e dati creati restano. |
| **Uninstall** | User clicca "Uninstall" | File del plugin rimossi. **Dati con `created_by_plugin_id = <plugin>`** (HabitNode, HabitTemplate, ExportTemplate, Chart) → dialog "Plugin X ha creato N habits, M templates, K charts. Cosa vuoi fare? **Keep** (rimangono come dati utente, `created_by_plugin_id` resta come traccia) / **Convert to user-owned** (azzera `created_by_plugin_id`) / **Delete** (soft-delete su HabitNode, hard-delete altrove)". Child events ereditano dalla scelta sul parent. Storage del plugin (`recordum.storage`) viene chiesto se rimuovere separatamente. |

### 3.5 Supply chain

Ogni `.rmpk` deve includere:

- **`package-lock.json`** (o `pnpm-lock.yaml` / `yarn.lock`) **obbligatorio** alla submission. Manifest manca lockfile → submission rifiutata dal CI.
- **`sbom.json`** generato dal CI al merge (CycloneDX format). Lista deterministica delle deps risolte. Committato nel registry sotto `plugins/<id>/<version>/sbom.json`.
- **Bundle stesso (`main.js`)** deve essere il prodotto della build dal lockfile — il CI rebuilda e verifica byte-match (modulo whitespace/sourcemaps).

**Advisory check:**

- Registry mantiene `tools/revoked-deps.json` con `(package_name, version_range, severity, advisory_url)`.
- CI ad ogni submission: matcha `sbom.json` contro `revoked-deps.json`. Match → submission rifiutata, autore notificato con link advisory.
- Ad ogni PR su `revoked-deps.json` (nuova advisory): CI rivaluta tutti i plugin pubblicati; chi matcha viene segnato `quarantined` (non installabile finché autore non aggiorna).

**Limiti onesti:**

- Native deps (binari pre-compilati in npm) non sono ammessi nei plugin Fase 7 — `main.js` deve essere puro JS/TS dopo bundle. CI rifiuta package con `*.node` o `*.dll` nel bundle.
- L'analisi statica non rileva supply chain backdoor sofisticati (es. obfuscation post-bundle). Lo dichiariamo nei docs senza pretendere di risolverlo.

---

## 4. Modello permessi

### 4.1 Categorie

| Categoria | Scopo | Granularità |
|---|---|---|
| **data** | Accesso a entità di dominio | `read|write:<entity>` dove entity ∈ `habit-nodes, completions, notes, goals, attachments, tags, templates, settings:public` |
| **network** | Chiamate HTTP esterne | Host whitelist (FQDN). `localhost`, IP privati (10/8, 172.16/12, 192.168/16, 169.254/16, ::1, fc00::/7) **sempre bloccati** anche se whitelistati (anti-SSRF). `["*"]` ammesso ma scatena warning rosso install. |
| **notifications** | Mostrare notifiche local | Boolean |
| **filesystem** | Accesso filesystem oltre lo storage del plugin | `none` (default) · `plugin-storage` (implicito, 10MB cap) · `attachments-read` · `user-pick` (solo via picker user-initiated) |
| **ai** | Processing AI locale (NER, sentiment, classificazione, modelli locali) | `ai:local`. Dichiarazione esplicita di intent — il plugin afferma "userò questi dati per inferenza, anche se solo on-device". Bypass del filtro `ai_data_sharing` a soglia `local_only` (vedi §4.6). |
| **ui/capability** | Capability minori | `commands:register` (implicito), `notifications:show`, `clipboard:read|write` |

### 4.2 Settings esclusi dall'accesso plugin

Anche con `read:settings:public`, mai esposti:

- Tutte le chiavi `ai_*` (`ai_mode`, `ai_default_sharing`)
- Tutte le chiavi `app_lock_*` (`app_lock_enabled`, `app_lock_type`)
- Credenziali di sync cloud (quando esisteranno in Fase 5+)

Lista mantenuta in `Recordum.Plugins.Contracts.SettingsAllowlist`. Test: nessuna chiave sensibile esce dal `PermissionBroker`.

### 4.3 Tre livelli di urgenza

| Livello | Permessi | UX |
|---|---|---|
| **Impliciti** | `recordum.storage` (10MB), `commands:register`, `i18n`, `log` | Nessun prompt. Sempre disponibili. |
| **Install-time declared** | `data:*`, `network:*`, `notifications`, `filesystem:plugin-storage`/`attachments-read` | Dialog all'install con lista permessi. All-or-nothing — non si possono accettare selettivamente. |
| **Runtime prompts** | Prima chiamata `network.fetch` a host whitelistato; lettura nota marcata "sensitive"; scritture batch > N (N=50 default); `filesystem:user-pick` (per definizione richiede picker) | Modale durante l'uso. User può **Allow once** / **Allow always** / **Deny**. |

### 4.4 UX install dialog

Stile inversivo: cosa il plugin **può** fare e cosa **non può** fare.

```
┌─────────────────────────────────────────────────────────┐
│  ✓ Open Source                                          │
│                                                          │
│  Install "Chart Engine" by Recordum?                    │
│                                                          │
│  This plugin can:                                       │
│   • Read your habits and completions                    │
│   • Show charts in habit detail screens                 │
│                                                          │
│  This plugin cannot:                                    │
│   • Modify your data                                    │
│   • Access the internet                                 │
│   • See notes you marked as sensitive                   │
│   • See data you have not opted into AI sharing         │
│                                                          │
│              [ Cancel ]    [ Install ]                  │
└─────────────────────────────────────────────────────────┘
```

Il "non può" è generato dal complemento dei permessi richiesti vs. la lista completa. Aiuta l'utente a interiorizzare il sandbox.

### 4.5 Permission diff su update

Confronto deep su `permissions` tra versione installata e versione nuova.

- Sottoinsieme o uguale → silent auto-update.
- Aggiunta di un permesso (anche un solo host in network) → prompt manuale di re-consent, evidenziando solo le diff.
- Rimozione di permessi → silent (riduzione di scope è sempre sicura).

### 4.6 Filtro `ai_data_sharing` a tre soglie

L'utente decide per ogni HabitNode (e HabitConfig) un livello in `ai_data_sharing ∈ {none, local_only, api_allowed}`. Il `PermissionBroker` onora questa decisione strutturalmente, filtrando **prima** della response API in base ai permessi del plugin chiamante:

| Permessi del plugin | Vede record con `ai_data_sharing ∈` |
|---|---|
| né `ai:local` né `network` | `{none, local_only, api_allowed}` (tutto — il plugin non rappresenta un rischio AI/esfiltrazione) |
| solo `ai:local` | `{local_only, api_allowed}` |
| solo `network` | `{api_allowed}` |
| `ai:local` **e** `network` | `{api_allowed}` (vince la soglia più stringente) |

**Trust model.** La dichiarazione `ai:local` è un trust statement: il plugin afferma di fare AI locale e accetta la soglia più stretta. Un plugin che mente (fa AI senza dichiararla) non ha incentivo (non guadagnerebbe accesso a più dati), ma se scoperto è soggetto a revocation §7.7.

**Edge case.** Plugin con `network` chiede una completion il cui habit ha `ai_data_sharing = 'none'`: risposta `null`/empty, **non** un errore. Niente information leak nemmeno tramite "esiste ma non te lo do".

**HabitConfig ai_data_sharing.** Quando lo schema lo prevede sulla config oltre che sul node, il filtro applica l'`AND` logico (più restrittivo dei due vince) per il singolo record di completion valutato.

### 4.7 Accesso alla provenance dei dati

Plugin con `read:habit-nodes` vedono il campo `created_by_plugin_id` come parte del DTO ritornato. Caso d'uso: un dashboard widget che mostra "powered by Plugin X" o un cleanup tool che lista dati per plugin.

**Scrittura.** Il plugin **non** può settare `created_by_plugin_id` su record che crea — il broker lo popola automaticamente con l'id del plugin chiamante. Niente spoofing.

**Plugin che crea per conto di un altro plugin** (es. importer che porta dati da un plugin esterno): non supportato; resta `null` o l'id del plugin chiamante.

---

## 5. API surface

### 5.1 Forma generale

Global `recordum` interamente async (Promise-based). Moduli:

| Modulo | Esempi |
|---|---|
| `recordum.habits` | `list()`, `get(id)`, `getTree()`, `create(input)`, `update(id, patch)`, `archive(id)`, `delete(id)`, `restore(id)` |
| `recordum.completions` | `record(habitId, input)`, `list(habitId, range)`, `update(id, patch)`, `delete(id)` |
| `recordum.notes` | `list(habitId)`, `create(habitId, markdown)`, `update(id, markdown)`, `delete(id)` |
| `recordum.goals` | `list(habitId)`, `create(habitId, input)`, `close(id)` |
| `recordum.attachments` | `getMetadata(id)`, `readBlob(id)` (richiede `attachments-read`) |
| `recordum.tags` | `list()`, `assign(habitId, tag)`, `unassign(habitId, tag)` |
| `recordum.templates` | `listHabitTemplates()`, `applyTemplate(templateId, parentId)` |
| `recordum.settings` | `get(key)`, `subscribe(key, callback)` — solo whitelist (§4.2) |
| `recordum.ui` | `registerExtension(point, definition)`, `showToast(msg)`, `openCommandPalette()`, `pushView(component)` |
| `recordum.events` | `on(eventName, handler)`, `off(handler)` |
| `recordum.storage` | `get(key)`, `set(key, value)`, `delete(key)`, `clear()` — scope per plugin, 10MB cap |
| `recordum.i18n` | `t(key, params?)`, `getCurrentLocale()` |
| `recordum.network` | `fetch(url, options)` — unico canale rete, validato contro manifest |
| `recordum.log` | `debug(msg)`, `info(msg)`, `warn(msg)`, `error(msg)` — finiscono in audit log |
| `recordum.lifecycle` | Esporti opzionali: `onActivate(ctx)`, `onDeactivate()`, `onUpgrade(prevVersion, newVersion)`. L'host chiama `onUpgrade` prima del primo enable della nuova versione (vedi §3.4). Se assente: nessuna migrazione automatica, storage preservato as-is. |

### 5.2 Extension points canonici

| ID | Scopo | Disponibile da |
|---|---|---|
| `dashboard.widget` | Widget custom sulla home | Fase 3 (Chart Engine come consumer) |
| `chart.renderer` | Rendering chart custom in habit detail / charts tab | Fase 3 (Chart Engine come consumer) |
| `habit.detail.tab` | Tab aggiuntivo nella habit detail screen | Fase 3 |
| `completion.input` | UI custom per completion recording (es. selector per scale custom) | Fase 3 |
| `settings.panel` | Pannello custom in Settings | Fase 3 |
| `command-palette` | Comandi nella palette globale | Fase 3 |
| `export.format` | Nuovo formato di export | Milestone 3.10 |
| `sync.provider` | Provider sync cloud | Fase 5 |
| `data.importer` | Import da app esterne | Fase 3 |

### 5.3 Eventi

| Evento | Permesso necessario per sottoscrivere |
|---|---|
| `habit.created`, `habit.updated`, `habit.deleted` | `read:habit-nodes` |
| `completion.recorded`, `completion.updated`, `completion.deleted` | `read:completions` |
| `note.created`, `note.updated` | `read:notes` |
| `streak.milestone` | `read:habit-nodes` |
| `goal.completed` | `read:goals` |
| `app.theme.changed`, `app.locale.changed` | (nessuno — impliciti) |

Filtro `ai_data_sharing` si applica anche agli eventi.

### 5.4 Errori

Tre famiglie:

| Famiglia | Esempio | Aspettativa |
|---|---|---|
| `PermissionDenied` | Plugin chiama `habits.update` senza `write:habit-nodes` | Bug del plugin. Logged, plugin riceve eccezione tipizzata, **non** crasha host. |
| `ValidationError` | Input non valido (es. completion con metric inesistente) | Gestito dal plugin. Eccezione tipizzata. |
| `HostUnavailable` | Host in shutdown, DB lock | Plugin retry o degrade. |

Errori non gestiti dal plugin → loggati, plugin marcato "unhealthy" se ripetuti, eventuale disable automatico dopo soglia.

### 5.5 Rete

Unico canale è `recordum.network.fetch(url, options)`. Implementazione lato host:

1. Parse URL.
2. Verifica scheme = `https` (eccezione: `http` ammesso solo se host è esplicitamente whitelistato e su `Settings → Advanced → Allow plain HTTP plugins`).
3. Verifica host ∈ manifest `permissions.network`.
4. Verifica host ∉ blacklist (localhost, IP privati).
5. Esegui via HttpClient con timeout di default (30s).
6. Limita response body (10MB hard cap default, configurabile per plugin).
7. Audit log.

`window.fetch` globale **disabilitato via CSP** (`connect-src` mancante per `https:` generico). `XMLHttpRequest` lo stesso. Niente bypass.

---

## 6. Sandbox e IPC

### 6.1 Isolamento WebView2

Una `CoreWebView2Environment` separata per plugin attivo:

- **User data folder** isolato: `%LocalAppData%/Recordum/plugin-sandboxes/<plugin-id>/`.
- **Profilo storage** separato (no cookie sharing).
- **GPU process** separato per plugin (Edge WebView2 default — niente da forzare).

Memoria attesa: 50-150 MB per plugin attivo. Documentato come limite di scalabilità (sopra ~10 plugin attivi simultaneamente l'app sente il peso).

### 6.2 Custom scheme

Plugin servito su `recordum-plugin://<plugin-id>/<asset-path>`. Implementazione via `AddWebResourceRequestedFilter` + handler:

- Validazione path-traversal: refuse `..`, refuse path assoluti, refuse symlink.
- Solo file dentro la cartella estratta del plugin sono leggibili.
- CSP iniettata via response header, non solo via `<meta>` (più forte).

Non si usa `file://` perché origin è troppo permissivo e gli asset locali avrebbero accesso pieno al filesystem se il sandbox WebView2 ha bug.

### 6.3 CSP costruita dal manifest

Per ogni plugin, l'host costruisce CSP dinamica:

```
default-src 'self' recordum-plugin://<id>;
script-src 'self' recordum-plugin://<id>;
style-src 'self' recordum-plugin://<id> 'unsafe-inline';   // tollerato per Toast UI / Milkdown
img-src 'self' recordum-plugin://<id> data:;
connect-src <hosts dal manifest>;                          // vuoto se network=[]
frame-src 'none';
object-src 'none';
base-uri 'none';
form-action 'none';
```

**No `'unsafe-eval'`**. Plugin che hanno bisogno di `eval` (es. template engine dinamici) devono rifattorizzare.

### 6.4 IPC

JSON-RPC-like su `postMessage`. Tre tipi di messaggio:

```typescript
// Plugin → Host (call)
{ kind: "request", id: "uuid", method: "habits.list", params: {...} }

// Host → Plugin (response)
{ kind: "response", id: "uuid", result: {...} }
{ kind: "response", id: "uuid", error: { code: "PermissionDenied", message: "..." } }

// Host → Plugin (event)
{ kind: "event", name: "completion.recorded", payload: {...} }
```

### 6.5 Flow di una chiamata

```
Plugin (JS)                Host (C#)
─────────────              ─────────
recordum.habits.list()
  │
  ├─ postMessage(request) ─►
                              │
                              ├─ Parse + valida shape (JSON Schema)
                              ├─ Identifica plugin caller (via WebView identity)
                              ├─ PermissionBroker.check("read:habit-nodes")
                              ├─ Valida params (tipi, range)
                              ├─ Chiama IHabitService.GetAllAsync()
                              ├─ Filtra ai_data_sharing
                              ├─ Audit log (level=debug)
                              ├─ Serializza response
                              ▼
  ◄─ postMessage(response) ─
  │
Promise resolved
```

### 6.6 Limiti runtime

| Risorsa | Limite | Comportamento al supero |
|---|---|---|
| Storage plugin | 10 MB | `QuotaExceeded` error |
| IPC rate | 1000 chiamate/secondo | Throttling con warning in log |
| Memory soft (per plugin) | 200 MB | Warning all'utente, plugin auto-disabled se persiste >30s |
| Long-running script | 5s sync | Watchdog interrompe, plugin marcato unhealthy |
| Disco package | 50 MB | Install rifiutato |
| Response fetch body | 10 MB | Truncate + error |
| Plugin attivi concurrent (soft cap) | 8 | Toast informativo "Many plugins active, app may slow down". Reversibile. |
| Plugin attivi concurrent (hard cap) | 16 | Dialog modale: utente deve disabilitare almeno 1 plugin per attivarne un altro. I 16 più recentemente abilitati restano attivi; gli altri restano installed-but-disabled. |

I cap concurrent partono dall'ipotesi memoria 50-150MB per plugin × 8 = ~1GB peak. Su macchine low-end (4GB RAM) il soft cap potrebbe già essere troppo alto; valutare cap dinamico in Fase 7 in base a `GC.GetTotalMemory()` come metrica.

### 6.7 DevTools

`Settings → Advanced → Enable plugin DevTools` (off di default). Apre Edge DevTools sulla WebView2 del plugin. Utile per debug, mai richiesto in uso normale.

### 6.8 Limitazioni dichiarate

Da documentare onestamente nei docs pubblici (`docs/plugin-api.md`):

- **Side channels**: timing attacks, cache attacks su Spectre/Meltdown non sono mitigati a livello WebView2. Sandbox è defense-in-depth, non garanzia formale.
- **Phishing in-app**: un plugin malevolo può imitare UI Recordum dentro il suo widget. Mitigazione: chrome di sistema visibile, badge "Plugin: <id>" sempre su widget esterni.
- **CVE WebView2**: dipendenza diretta dal patching di Edge runtime. Recordum non può patchare WebView2; documenta che gli utenti devono mantenere Windows aggiornato.
- **Bug nel custom scheme handler**: bug in path validation può bypassare il sandbox. Trattare come security-critical. Test esaustivi richiesti.

---

## 7. Marketplace e distribuzione

### 7.1 Hosting

Modello Obsidian:

```
recordum-plugins (git repo pubblico, owned by Recordum)
├── plugins/
│   ├── io.recordum.chart-engine/
│   │   └── manifest.json          (sha256, signature, release_url)
│   └── …
├── publishers/
│   ├── recordum-official/
│   │   └── identity.json          (claim GitHub identity)
│   └── …
├── revoked.json                    (lista plugin/versioni revocate)
└── community-picks.json            (selezione editoriale)
```

I `.rmpk` veri vivono nelle **GitHub Releases del repo del publisher**. Recordum non ospita binari. Zero hosting cost, zero responsabilità di storage.

### 7.2 Submission via PR + CI

Publisher apre PR sul repo `recordum-plugins`. CI esegue:

1. **Identity verification** (prima volta): claim verificato via file `.well-known` nel repo source o nel profilo GitHub del publisher.
2. **sha256** del `.rmpk` matcha quello dichiarato.
3. **Signature ed25519** valida contro la chiave pubblica del publisher.
4. **Manifest schema** valido.
5. **`source_url`** raggiungibile e contiene `LICENSE` con licenza OSI-approved → badge OSS automatico.
6. **`min_recordum_version`** parsabile e ≤ ultima release Recordum.

PR auto-merged se tutte le check passano. **Niente review umana di routine**.

### 7.3 Badge system

| Badge | Criterio | Gating |
|---|---|---|
| ✓ **Open Source** | `source_url` raggiungibile + `LICENSE` con licenza OSI | Automatico, non-gating |
| ✓ **Verified Publisher** | Review umana rara (publisher di scala / istituzionali). Revocabile. | Non-gating |
| ★ **Community Pick** | Selezione editoriale ruotata mensilmente | Non-gating |
| (nessuno) | Default per closed-source senza track record | — |

**Non-gating** significa che un plugin senza badge è installabile esattamente come gli altri. Il badge è segnaletica, non firewall.

### 7.4 UX marketplace in-app

Lista con search, sort (popolare / recente / alfabetico), categories (Charts, Health, Productivity, Sync, Themes, Utilities).

Segnali per voce:

- Badge (icona + tooltip)
- Autore (link a `publisher.url`)
- Install count (da telemetria opzionale, vedi §7.6)
- Rating media (idem)
- Ultima release (data + version)
- Screenshot (massimo 5, dal `manifest.json` o dal `README.md`)
- Link a `source_url` e issue tracker

### 7.5 Sideloading

`Settings → Advanced → Developer mode` (off di default). Quando attivo:

- Drag-drop di `.rmpk` non firmati con **warning rosso** "This plugin is not from the official registry. Only proceed if you trust the source."
- Plugin sideloaded marcato chiaramente nel plugin list con badge "Sideloaded".
- Stesso sandbox e permessi del plugin marketplace.
- Auto-update disabilitato (sideloaded resta alla versione installata finché user non riimporta).

### 7.6 Telemetria (opt-in)

**Unica componente server-side del progetto.** Endpoint Recordum-owned (~$5/mese in hosting). Riceve:

- `install_count` per plugin (incrementato all'enable).
- `rating` (1-5 stelle) dall'utente.
- UUID device anonimo (random alla prima opt-in, salvato in `AppSetting.telemetry_device_uuid`).

**Nessun** fingerprint, **nessun** IP loggato oltre rate-limit transient, **nessuna** correlazione cross-plugin.

Opt-in alla prima apertura del marketplace, **non** al primo avvio dell'app. Retention: rolling 365 giorni. Privacy policy esplicita e linkata.

**Per release pubblica EU serve GDPR-compliance documentata** — vedi §9 (Production-readiness prerequisites).

### 7.7 Revocation

Plugin compromesso o malevolo:

1. PR su `revoked.json` con `plugin_id`, `version_range` (es. `<=1.2.3`), `reason`, `date`.
2. App checka `revoked.json` al refresh registry (default ogni 24h all'apertura, override manuale).
3. Plugin revocato → disable automatico + dialog all'utente: "This plugin has been removed from the registry due to: <reason>. You can restore it from Advanced settings if you understand the risk."
4. **Finestra di esposizione offline accettata**: se l'utente non apre l'app per 30 giorni dopo una revoca, il plugin resta attivo fino al refresh. Non c'è push.

### 7.8 Versioning

`min_recordum_version` filtra release incompatibili dal lato app. **Niente force-update di Recordum.** Se un plugin richiede una versione più nuova, l'app lo nasconde dalla install list e mostra "Requires Recordum X.Y.Z" sui plugin già installati.

### 7.9 Backup e restore

Il backup nativo di Recordum (feature di base offline-first, da specificare in milestone Fase 3 dedicata) deve includere lo stato plugin per non rompere l'esperienza al restore. Contenuto plugin-related nel backup:

| Cosa | Come |
|---|---|
| Lista plugin installati | Array `{plugin_id, version, enabled, sideloaded}` — referenza, non binario |
| Permission state | Per ogni plugin: `{permissions_granted_at, runtime_decisions[]}` (le scelte allow-once/always degli prompts) |
| `recordum.storage` di ogni plugin | Blob JSON per scope, fino a 10MB ciascuno |
| `created_by_plugin_id` sui dati | Già nello schema, esportato col DB |

**Non incluso nel backup:**

- I file `.rmpk` veri (peso). Il restore tenterà di reinstallarli dal registry usando `plugin_id+version`.
- DevTools state, cache WebView2 isolata.

**Restore flow:**

1. Restore DB + settings + storage plugin.
2. Per ogni plugin nella lista: cerca nel registry. Se trovato e compatibile → reinstall + apply permission state. Se non trovato (revocato, sparito) → notifica utente "Plugin X non disponibile: dati creati restano, plugin non funzionerà".
3. Plugin sideloaded: chiede all'utente di reimportare manualmente il `.rmpk`.

**Backup encrypted?** Se la decisione cross-cutting su crittografia at-rest (§11) abilita SQLCipher, il backup viene encryptato con la stessa derivazione di chiave. Restore richiede la passphrase se l'utente l'aveva impostata.

---

## 8. Cosa si scrive in Fase 3

### 8.1 Sì in Fase 3

- **Nuovo progetto `Recordum.Plugins.Contracts`** con:
  - `Contracts/` (IHabitService, ICompletionService, INoteService, IGoalService, IExtensionPoint, IPluginContext, IPermissionBroker, …)
  - `Permissions/` (PermissionId enum, PermissionRequest, PermissionDecision)
  - `ExtensionPoints/` (ExtensionPointId enum, registration record)
  - `Events/` (EventName enum, EventPayload base classes)
  - `Manifest/` (PluginManifest record, ManifestValidator interface)
- **Nuovo progetto `Recordum.Plugins`** con:
  - `Hosting/InProcessPluginHost.cs` (un solo `IPluginHost`)
  - `PluginManager.cs` (load/enable/disable lifecycle — semplificato per consumer interni)
  - `ApiRouter.cs` (routing via reflection o source-gen)
  - `ExtensionPointRegistry.cs`
  - `EventBus.cs`
  - `PermissionBroker.cs` con implementazione `AlwaysAllow` di default (consumer interni fidati)
- **Pacchetto TypeScript** in `tools/plugin-api-ts/`:
  - Solo type defs in Fase 3, generate dai contratti C#.
  - **Non pubblicato su npm** — vive nel repo come riferimento.
- **`manifest.schema.json`** canonico committato in `tools/plugin-api-ts/manifest.schema.json`.
- **`docs/plugin-api.md`** — overview e tutorial intro (anche se nessun runtime esterno gira).
- **Chart Engine come "core plugin" pilota** implementato in Milestone 3.9 come consumer dell'API plugin. È la prova che il contratto funziona. Altri first-party (export 3.10, heatmap 3.7, dashboard widget 3.3, importer Fase 7) adottano il pattern mano mano ma non sono blocking per 3.1.
- **Schema additivo per provenance**: 4 colonne `created_by_plugin_id` nella `InitialSchema` migration (§2.4). Aggiornamento documento schema a v2.3 (o addendum) prima del freeze.
- **Permission enum completa** in `Recordum.Plugins.Contracts.Permissions` (include `ai:local` anche se non enforced in Fase 3 — la enum è il contratto, l'enforcement arriva con `PermissionBroker.Manifest` in Fase 7).
- **`IPluginContext.PluginId` valorizzato** sugli `InProcessPluginHost` (con id sintetici tipo `core.chart-engine`) — il `SaveChangesInterceptor` legge questa proprietà per popolare `created_by_plugin_id` automaticamente.

### 8.2 No in Fase 3

- `WebView2PluginHost` (Fase 7)
- Manifest parser runtime con validazione completa (Fase 7 — in Fase 3 i first-party non hanno manifest, sono compilati)
- Settings UI per plugin management (Fase 7)
- Marketplace UI (Fase 7)
- Signing (Fase 7)
- Sideloading (Fase 7)
- Registry sync con `recordum-plugins` (Fase 7)
- Custom scheme handler (Fase 7)
- CSP costruzione dinamica (Fase 7)
- Permission enforcement reale (`PermissionBroker` resta `AlwaysAllow` per consumer interni)
- Telemetria (Fase 7)

### 8.3 Costo stimato Fase 3

**~3-4 giorni di lavoro architetturale aggiuntivo** sparsi su:

- **Milestone 3.1** (~2 giorni): scheletro `Recordum.Plugins.Contracts` + `Recordum.Plugins` + `InProcessPluginHost` + `PermissionBroker.AlwaysAllow` + DI registration + `manifest.schema.json` + `@recordum/plugin-api/index.d.ts` skeleton + `docs/plugin-api.md` skeleton.
- **Milestone 3.9** (~1-2 giorni extra rispetto a un Chart Engine non-pluginificato): Chart Engine implementato come consumer dell'API plugin, registrato via `IExtensionPoint` con `chart.renderer`. Validazione del contratto.

### 8.4 Path Fase 3 → Fase 7

Quando si arriva a Fase 7:

1. Aggiungere `WebView2PluginHost : IPluginHost` come secondo host.
2. Sostituire `PermissionBroker.AlwaysAllow` con `PermissionBroker.Manifest` (validazione reale contro `PluginManifest`).
3. Implementare custom scheme handler `recordum-plugin://`.
4. Implementare CSP builder dinamico.
5. Implementare marketplace UI + registry sync.
6. Implementare sideloading + developer mode.
7. Pubblicare `@recordum/plugin-api` su npm.
8. Pubblicare repo `recordum-plugins` come registry.

**Il codice first-party non viene toccato.** Continua a girare sull'`InProcessPluginHost` o, opzionalmente, viene migrato all'host esterno per dogfooding.

---

## 9. Production-readiness prerequisites

Punti emersi a fine sessione di brainstorming, **fuori dallo scope del sistema plugin** ma blocking per il rilascio pubblico (vedi [memoria `recordum-public-release`]). Documentati qui per non perderli; ognuno richiederà il suo spec o ticket.

| # | Tema | Note |
|---|---|---|
| 1 | **Telemetria GDPR-compliant** | Endpoint anonimizzato, opt-in al primo accesso al marketplace, no fingerprint, retention 365gg, privacy policy esplicita. Necessario per §7.6. |
| 2 | **GDPR data subject rights** | Endpoint REST per export e delete del proprio `device_uuid` dalla telemetria (right to access, right to erasure). Documentato in privacy policy. Implementabile in pochi giorni una volta deciso lo stack server della telemetria. |
| 3 | **Code signing dell'app** | Certificato EV (~$300-500/anno) per evitare Windows SmartScreen "publisher unknown" al primo install. Da pianificare prima della release v1.0. |
| 4 | **Privacy policy + Terms of Service** | Anche se l'app è offline-first, marketplace e telemetria opzionale richiedono trattamento legale. Da scrivere prima della release v1.0. |
| 5 | **Accessibilità WPF** | WCAG AA: screen reader (UI Automation), keyboard nav completo, contrast ratio. CLAUDE.md la cita in Fase 7 ma andrebbe esplicitata come milestone dedicata. |
| 6 | **Namespace dominio brand** | Verificare disponibilità `recordum.io` o equivalente prima di cementare `io.recordum.*` come ID namespace ufficiale. **Questa è l'unica decisione che potrebbe toccare lo spec plugin se cambiata** — finché resta placeholder, lo spec è valido. Search/replace meccanico al momento della decisione. |
| 7 | **Crittografia at-rest** | Decisione cross-cutting sigillata in §11. Non plugin-specific ma sigillata in questo design perché impatta il backup §7.9 e il modello di trust complessivo. |

---

## 10. Open questions & follow-up

Domande non risolte che vanno chiuse prima del `writing-plans` di implementazione (Fase 3):

1. **Estensione `.rmpk` vs `.rpkg`** — confermare al momento dell'implementazione del manifest schema. Default attuale: `.rmpk`.
2. **`reset_time` cascade nei plugin** — un plugin che mostra "oggi" deve ovviamente usare `IResetTimeResolver`. Confermare che l'API esporta `recordum.habits.getResetTime(habitId): Promise<TimeOnly>` come helper, evitando che ogni plugin reimplementi la cascade.
3. **`HabitConfigHistory` versioning ai plugin** — un plugin che valuta completion storiche deve sapere se la config era cambiata. Esponiamo `recordum.habits.getConfigAt(habitId, timestamp)` o lasciamo il plugin a inferire? Decisione consigliata: esporre `getConfigAt`.
4. **Notifiche locali** — Recordum stesso non ha ancora deciso il backend di notifiche (Windows Toast vs custom). Specificare in milestone 3.4 il provider e l'interfaccia che i plugin con `notifications:show` useranno.
5. **Eventi attachment** — assenti dalla lista §5.3. Aggiungere `attachment.added`/`attachment.removed` se l'use case (es. plugin di backup foto) lo giustifica. Decisione: aggiungere solo se uno dei first-party lo richiede prima di Fase 7.
6. **Estensibilità `node_type`/habit type** — un plugin **non** può aggiungere un sesto tipo di habit. La filosofia compositiva di CLAUDE.md §5.3 vince: i tipi sono fissati e i casi nuovi si compongono. Da esplicitare nei docs API per evitare richieste future.
7. **Localizzazione listing marketplace** — categorie del marketplace (Charts, Health, Productivity…) attualmente hardcoded in inglese. Strategia: il listing usa la `description_key` del manifest risolta contro `locales/<lang>.json` del plugin per il testo descrittivo; le categorie restano enum tradotte dal client. Da implementare quando si scrive il marketplace UI (Fase 7).
8. **`manifest.schema_version` deprecation policy** — quando esce schema v2.0 (breaking), v1.x resta supportato per N major release di Recordum (proposta: N=2). Plugin con schema_version non più supportato → install rifiutato, plugin esistenti restano disabled con notifica. Da formalizzare.
9. **Streaming API per attachment grandi** — `readBlob(id)` carica in memoria; foto >50MB rompono il limite IPC. Da introdurre `readBlobStream(id)` con `ReadableStream` nel pacchetto TS API quando arriva il primo use case (es. plugin di backup foto). Non blocking.
10. **Moderazione review marketplace** — chi gestisce rating spam? Proposta: rate limit per UUID device + report button (PR sul registry con motivazione). Niente moderazione centralizzata. Da scrivere policy quando il marketplace va live.

---

## 11. Decisioni cross-cutting sigillate in questo design

Decisioni emerse durante il brainstorming plugin ma che eccedono lo scope plugin. Sigillate qui per evitare un secondo spec; ciascuna richiede comunque ticket/milestone propri nell'implementazione.

### 11.1 Crittografia at-rest del DB SQLite

**Decisione (§1.10).** SQLCipher con due livelli:

- **Baseline (sempre on):** chiave master derivata via DPAPI (`ProtectedData.Protect`, scope = `CurrentUser`). Niente passphrase utente. Transparente. Protegge da accesso casuale al filesystem (es. file leakato in backup non-cifrato, USB stick, second account sulla stessa macchina senza credenziali).
- **Upgrade opzionale:** Settings → Privacy → "Encrypt my data with a passphrase". Toggle on → prompt passphrase (con conferma + recovery hint opzionale memorizzato in chiaro). La chiave master viene ri-derivata da passphrase via Argon2id. Passphrase prompted all'avvio dell'app (separato da `app_lock_enabled` che protegge l'UI). **No recovery server-side**: passphrase dimenticata = dati persi. UI lo dice esplicitamente.

**Migrazione DPAPI → passphrase.** All'opt-in, l'app: (1) decifra DB con chiave DPAPI, (2) ri-cifra con chiave passphrase. Operazione one-shot, ~secondi per DB <100MB.

**Implementazione.** Pacchetto NuGet `Microsoft.Data.Sqlite` + binding SQLCipher (es. `SQLitePCLRaw.bundle_e_sqlcipher`). Connection string include `Password=...`. EF Core transparente.

**Perf budget.** SQLCipher aggiunge ~10-15% read overhead, di più su write. Da misurare prima del freeze su un dataset realistico (50 habit × 2 anni di completion) contro il target home-screen <500ms. Se la misura fallisce il budget, fallback: disattivare SQLCipher come **default** (rimuovere baseline DPAPI) e tenere solo l'opt-in passphrase. Decisione di fallback documentata nel plan di implementazione.

**Scope.** Tocca `Recordum.Data` (connection string + initialization) e `Recordum.WPF` (settings UI + key derivation flow). **Non** tocca `Recordum.Core` né `Recordum.Plugins`. Milestone dedicata da inserire in Fase 3 (probabilmente 3.2 — Core Data Layer è il punto giusto).

**Backup.** Se l'utente ha upgrade passphrase, anche il backup (vedi §7.9) è encrypted con la stessa derivazione. Restore richiede passphrase.

**Trade-off accettati.** (1) Perf hit costante. (2) DPAPI lega il DB al Windows account corrente — copiare il DB su altra macchina senza la chiave DPAPI lo rende illeggibile a meno di upgrade passphrase. (3) DPAPI non porta in Fase 4 web / Fase 6 mobile: lì serve scelta alternativa (es. on iOS keychain, on Android Keystore). Decisione per quelle fasi è **out-of-scope qui**.

---

## 12. Riferimenti

- Brainstorming session: 2026-05-13 (vedi memoria `recordum-plugin-system-design`)
- `CLAUDE.md` §3-4 (stack e architettura), §13 (production-grade)
- `Recordum_DatabaseSchema_v2_2.docx` — schema autoritativo, **non toccato** da questo spec
- Modello distributivo: Obsidian Community Plugins
- Modello contrattuale API: VS Code Extension API
- Microsoft.Web.WebView2 documentation (custom scheme, environment isolation)

---

## 13. Storia revisioni

| Data | Autore | Note |
|---|---|---|
| 2026-05-13 | Brainstorming | 7 decisioni macro approvate; sezione 1-7 design |
| 2026-05-14 | Spec draft v0.1 | Prima stesura completa per review |
| 2026-05-14 | Spec draft v0.2 | Integrate decisioni 8-13 a seguito di review preliminare: provenance (1D), AI filter tri-tier (2A), encryption DPAPI+passphrase (7C), backup inclusion, supply chain, storage migration hook, performance budget concreto, GDPR data subject rights. Schema delta in §2.4 (richiede addendum v2.3). |
