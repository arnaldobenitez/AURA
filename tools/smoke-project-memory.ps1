[CmdletBinding()]
param()

$repoRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $repoRoot "tools\aura.ps1"
$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("aura-project-memory-smoke-" + [guid]::NewGuid().ToString("N"))
$projectRoot = Join-Path $smokeRoot "project"
$indexPath = Join-Path $smokeRoot "projects\index.json"
$payloadPath = Join-Path $smokeRoot "close-payload.json"

function Invoke-Aura {
    param([string[]]$CommandArguments)
    & $launcher @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "AURA command failed with exit code ${LASTEXITCODE}: $($CommandArguments -join ' ')"
    }
}

try {
    New-Item -ItemType Directory -Path $projectRoot -Force | Out-Null

    Invoke-Aura @("project", "init", $projectRoot, "--name", "AURA Smoke", "--json")
    Invoke-Aura @("project", "validate", $projectRoot, "--json")
    Invoke-Aura @("agent", "bootstrap", $projectRoot, "--json")

    $started = (& $launcher project session start --path $projectRoot --objective "Project Memory smoke test" --role "CODER" --json | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0 -or -not $started.success) { throw "session start failed" }
    $sessionId = $started.sessionId

    Invoke-Aura @("project", "session", "status", "--path", $projectRoot, "--json")

    Set-Content -LiteralPath $payloadPath -Value '{ "result": "Smoke test completed", "pending": "None", "nextRecommendedStep": "Continue from current state" }' -Encoding utf8
    Invoke-Aura @("project", "session", "close", "--path", $projectRoot, "--session", $sessionId, "--input", $payloadPath, "--json")

    Invoke-Aura @("manager", "inspect", $projectRoot, "--json")
    Invoke-Aura @("manager", "remember", $projectRoot, "--alias", "smoke", "--index-path", $indexPath, "--json")
    Invoke-Aura @("manager", "list-known", "--index-path", $indexPath, "--json")
    Invoke-Aura @("manager", "history", $projectRoot, "--take", "5", "--json")
    Invoke-Aura @("manager", "handoff", $projectRoot, "--json")
    Invoke-Aura @("manager", "forget", "smoke", "--index-path", $indexPath, "--json")

    Write-Host "AURA Project Memory smoke test passed."
    exit 0
}
finally {
    if (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force
    }
}
