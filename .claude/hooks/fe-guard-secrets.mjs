#!/usr/bin/env node
// Pre-Edit/Write/MultiEdit hook: scan new content for common secret patterns.
// Block the write if any pattern matches.
// React note: also blocks VITE_/NEXT_PUBLIC_ env vars that look like real secrets
// (env vars prefixed this way are embedded in the public JS bundle).

import { readFileSync } from 'node:fs';

const payload = JSON.parse(readFileSync(0, 'utf8') || '{}');
const input = payload?.tool_input ?? {};
const content = [input.content, input.new_string, ...(input.edits ?? []).map((e) => e.new_string)]
  .filter(Boolean)
  .join('\n');

const PATTERNS = [
  { name: 'AWS access key', re: /AKIA[0-9A-Z]{16}/ },
  { name: 'AWS secret key', re: /aws_secret_access_key\s*=\s*['"]?[A-Za-z0-9/+=]{40}/i },
  { name: 'GitHub PAT', re: /ghp_[A-Za-z0-9]{36}/ },
  { name: 'GitHub fine-grained PAT', re: /github_pat_[A-Za-z0-9_]{82}/ },
  { name: 'Slack token', re: /xox[abprs]-[A-Za-z0-9-]{10,}/ },
  { name: 'Stripe live key', re: /sk_live_[A-Za-z0-9]{24,}/ },
  { name: 'Private key block', re: /-----BEGIN (RSA |EC |OPENSSH |)PRIVATE KEY-----/ },
  { name: 'Generic API key assignment', re: /(api[_-]?key|secret|password)\s*=\s*['"][A-Za-z0-9_\-]{20,}['"]/i },
  { name: 'Public env var with secret value (VITE_/NEXT_PUBLIC_)', re: /(VITE_|NEXT_PUBLIC_)[A-Z_]*(?:SECRET|KEY|TOKEN|PASSWORD)\s*=\s*['"][A-Za-z0-9_\-]{20,}['"]/i },
];

const hits = PATTERNS.filter((p) => p.re.test(content));
if (hits.length > 0) {
  console.error(
    `[guard-secrets] BLOCKED — looks like a secret in the write:\n  - ${hits.map((h) => h.name).join('\n  - ')}\n\n` +
    `Move it to .env (gitignored) or a secret manager.\n` +
    `React/Vite warning: VITE_*/NEXT_PUBLIC_* env vars are bundled into your public JS — never put real secrets there.`,
  );
  process.exit(2);
}
process.exit(0);
