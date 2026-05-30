#!/usr/bin/env node
// Pre-Bash hook: run the full check before any `git commit`.
// Blocks the commit if typecheck, lint, format, or tests fail.
// Fast checks (format, lint, typecheck) catch issues before the expensive test run.

import { readFileSync, existsSync } from 'node:fs';
import { execSync } from 'node:child_process';

const payload = JSON.parse(readFileSync(0, 'utf8') || '{}');
const cmd = payload?.tool_input?.command ?? '';

if (!/\bgit\s+commit\b/.test(cmd)) process.exit(0);

// Detect package manager
const pm = existsSync('pnpm-lock.yaml') ? 'pnpm' : existsSync('yarn.lock') ? 'yarn' : 'npm';
const checkCmd = `${pm} run check`;

console.error(`[guard-commit] Running ${checkCmd} before commit...`);

try {
  execSync(checkCmd, { stdio: 'inherit' });
  process.exit(0);
} catch {
  console.error(
    `\n[guard-commit] BLOCKED — ${checkCmd} failed.\n` +
    `Fix all typecheck, lint, format, and test errors before committing.\n` +
    `Run \`${pm} run check\` to see the full output.`,
  );
  process.exit(2);
}
