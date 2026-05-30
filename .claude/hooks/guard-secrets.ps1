# Pre-Edit/Write/MultiEdit hook: scan new content for common secret patterns.

$ErrorActionPreference = 'Stop'
$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$parts = @()
if ($payload.tool_input.content)    { $parts += $payload.tool_input.content }
if ($payload.tool_input.new_string) { $parts += $payload.tool_input.new_string }
if ($payload.tool_input.edits) {
    foreach ($e in $payload.tool_input.edits) { if ($e.new_string) { $parts += $e.new_string } }
}
$content = $parts -join "`n"
if (-not $content) { exit 0 }

$patterns = @(
    @{ Name = 'AWS access key'; Regex = 'AKIA[0-9A-Z]{16}' }
    @{ Name = 'GitHub PAT'; Regex = 'ghp_[A-Za-z0-9]{36}' }
    @{ Name = 'GitHub fine-grained PAT'; Regex = 'github_pat_[A-Za-z0-9_]{82}' }
    @{ Name = 'Azure storage key'; Regex = 'AccountKey=[A-Za-z0-9+/=]{60,}' }
    @{ Name = 'Connection string with password'; Regex = '(?i)Password\s*=\s*[^;"'']{8,}' }
    @{ Name = 'Stripe live key'; Regex = 'sk_live_[A-Za-z0-9]{24,}' }
    @{ Name = 'Private key block'; Regex = '-----BEGIN (RSA |EC |OPENSSH |)PRIVATE KEY-----' }
    @{ Name = 'Generic API key'; Regex = '(?i)(api[_-]?key|secret|password)\s*=\s*"[A-Za-z0-9_\-]{20,}"' }
)

$hits = @()
foreach ($p in $patterns) { if ($content -match $p.Regex) { $hits += $p.Name } }

if ($hits.Count -gt 0) {
    Write-Error "[guard-secrets] BLOCKED — possible secret in write:`n  - $($hits -join "`n  - ")`n  Move to User Secrets / Key Vault / env var."
    exit 2
}
exit 0
