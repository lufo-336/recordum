#!/usr/bin/env node
// PreToolUse (Bash/PowerShell): chiede conferma prima di cancellazioni che colpiscono
// i dati utente reali (%LocalAppData%\Recordum, ~/.local/share/Recordum o file .recbak). Non blocca: 'ask'.
const fs = require('fs');
let d = {};
try { d = JSON.parse(fs.readFileSync(0, 'utf8') || '{}'); } catch { process.exit(0); }
const cmd = (d.tool_input && d.tool_input.command) || '';
const low = cmd.toLowerCase();
const isDelete = low.includes('remove-item') || /\brm\s+-\w*[rf]/.test(low);
const touches =
  low.includes('.recbak') ||
  low.includes('appdata/local/recordum') ||
  low.includes('appdata\\local\\recordum') ||
  /localappdata[\\/]+recordum/.test(low) ||
  // Linux: $XDG_DATA_HOME/Recordum, di norma ~/.local/share/Recordum (Passo 0, RV-6).
  /\.local\/share\/recordum/.test(low) ||
  /xdg_data_home[}"']?\/recordum/.test(low);
if (isDelete && touches) {
  process.stdout.write(JSON.stringify({ hookSpecificOutput: {
    hookEventName: 'PreToolUse',
    permissionDecision: 'ask',
    permissionDecisionReason:
      'Questo comando cancella dati utente reali (DB cifrato e/o backup .recbak in %LocalAppData%\\Recordum). Confermi?'
  }}));
}
process.exit(0);
