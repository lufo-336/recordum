#!/usr/bin/env node
// PreToolUse (Edit/Write/MultiEdit): blocca la modifica a mano dei file EF generati
// (*.Designer.cs, *ModelSnapshot.cs). Vanno rigenerati con dotnet ef.
const fs = require('fs');
let d = {};
try { d = JSON.parse(fs.readFileSync(0, 'utf8') || '{}'); } catch { process.exit(0); }
const ti = d.tool_input || {};
const fp = ti.file_path || ti.filePath || '';
const low = fp.replace(/\\/g, '/').toLowerCase();
if (low.endsWith('.designer.cs') || low.endsWith('modelsnapshot.cs')) {
  process.stdout.write(JSON.stringify({ hookSpecificOutput: {
    hookEventName: 'PreToolUse',
    permissionDecision: 'deny',
    permissionDecisionReason:
      `'${fp}' e' generato da EF Core. Non modificarlo a mano: rigeneralo con 'dotnet ef migrations add/remove'.`
  }}));
}
process.exit(0);
