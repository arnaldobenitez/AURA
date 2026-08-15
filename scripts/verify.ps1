#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "AURA.sln"
$webDir = Join-Path $repoRoot "src\AURA.Web"

function Invoke-Step {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [scriptblock] $Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan

    & $Action

    if ($LASTEXITCODE -ne 0) {
        Write-Host "FALLO: $Name (codigo $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "OK: $Name" -ForegroundColor Green
}

Invoke-Step "dotnet restore" { dotnet restore $solution }
Invoke-Step "dotnet build" { dotnet build $solution --no-restore }
Invoke-Step "dotnet test" { dotnet test $solution --no-build }

Invoke-Step "npm ci" {
    Push-Location $webDir
    try { npm ci } finally { Pop-Location }
}
Invoke-Step "npm run typecheck" {
    Push-Location $webDir
    try { npm run typecheck } finally { Pop-Location }
}
Invoke-Step "npm run lint" {
    Push-Location $webDir
    try { npm run lint } finally { Pop-Location }
}
Invoke-Step "npm run build" {
    Push-Location $webDir
    try { npm run build } finally { Pop-Location }
}

Write-Host ""
Write-Host "Verificacion completa: todos los pasos finalizaron correctamente." -ForegroundColor Green
exit 0
