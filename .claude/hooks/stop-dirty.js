#!/usr/bin/env node
// Stop: se il working tree git e' sporco, lo segnala (promemoria, non blocca).
const cp = require('child_process');
let out = '';
try { out = cp.execSync('git status --porcelain', { encoding: 'utf8' }).trim(); }
catch { process.exit(0); }
if (out) {
  const n = out.split('\n').length;
  process.stdout.write(JSON.stringify({
    systemMessage: `Promemoria: ${n} file non committati nel working tree.`
  }));
}
process.exit(0);
