#!/usr/bin/env node
// PreToolUse (Bash/PowerShell): blocca git commit/push se il branch e' main/master.
// Fail-open: se non determina il branch, lascia procedere.
const fs = require('fs');
const cp = require('child_process');
let d = {};
try { d = JSON.parse(fs.readFileSync(0, 'utf8') || '{}'); } catch { process.exit(0); }
const cmd = (d.tool_input && d.tool_input.command) || '';
if (!cmd.includes('git commit') && !cmd.includes('git push')) process.exit(0);
let branch = '';
try { branch = cp.execSync('git rev-parse --abbrev-ref HEAD', { encoding: 'utf8' }).trim(); }
catch { process.exit(0); }
if (branch === 'main' || branch === 'master') {
  process.stdout.write(JSON.stringify({ hookSpecificOutput: {
    hookEventName: 'PreToolUse',
    permissionDecision: 'deny',
    permissionDecisionReason:
      `Sei sul branch '${branch}'. Crea/passa a un branch feature prima di commit/push (CLAUDE.md: rama prima).`
  }}));
}
process.exit(0);
