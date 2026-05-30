# Pre-Bash/PowerShell hook: when the agent tries to `git commit`, run the full check first.
# Stdin is JSON describing the tool call.

$ErrorActionPreference = 'Stop'
$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }

try {
    $payload = $raw | ConvertFrom-Json
} catch {
    exit 0  # malformed — don't block
}

$cmd = $payload.tool_input.command
if (-not $cmd -or $cmd -notmatch '\bgit\s+commit\b') {
    exit 0
}

Write-Host "[guard-commit] git commit detected — running dotnet format/build/test..."

try {
    dotnet format --verify-no-changes --severity warn
    if ($LASTEXITCODE -ne 0) { throw "dotnet format failed" }

    dotnet build --nologo --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

    dotnet test --nologo --no-build -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }

    exit 0
} catch {
    Write-Error "[guard-commit] $_ — commit blocked. Fix and retry."
    exit 2
}
