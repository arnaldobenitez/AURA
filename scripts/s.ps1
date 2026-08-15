#requires -Version 5.1
[CmdletBinding()]
param(
    [int[]] $Ports = @(5235, 5173, 4173)
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "src\AURA.Api\AURA.Api.csproj"
$webDir = Join-Path $repoRoot "src\AURA.Web"
$apiUrl = "http://localhost:5235"
$webUrl = "http://localhost:5173"

Write-Host "Cerrando puertos en uso: $($Ports -join ', ')..." -ForegroundColor Cyan
foreach ($port in $Ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) {
        continue
    }

    $processIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $processIds) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "  Puerto $port -> deteniendo $($process.ProcessName) (PID $processId)" -ForegroundColor Yellow
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host ""
Write-Host "Recompilando AURA.Api..." -ForegroundColor Cyan
dotnet build $apiProject
if ($LASTEXITCODE -ne 0) {
    Write-Host "FALLO: la compilacion de AURA.Api no finalizo correctamente (codigo $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

if (-not (Test-Path (Join-Path $webDir "node_modules"))) {
    Write-Host ""
    Write-Host "Instalando dependencias de AURA.Web (primera vez)..." -ForegroundColor Cyan
    Push-Location $webDir
    try { npm install } finally { Pop-Location }
}

Write-Host ""
Write-Host "Iniciando AURA.Web (frontend) en $webUrl ..." -ForegroundColor Cyan
Start-Process -FilePath "npm.cmd" -ArgumentList @("run", "dev") -WorkingDirectory $webDir -WindowStyle Normal

Write-Host ""
Write-Host "Pantalla: $webUrl   (la API en $apiUrl sólo expone JSON, no tiene interfaz)" -ForegroundColor Green
Write-Host "Iniciando AURA.Api en $apiUrl ..." -ForegroundColor Green
dotnet run --project $apiProject --no-build --urls $apiUrl
