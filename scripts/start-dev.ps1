#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "src\AURA.Api\AURA.Api.csproj"
$webDir = Join-Path $repoRoot "src\AURA.Web"

$apiUrl = "http://localhost:5235"
$webUrl = "http://localhost:5173"

Write-Host "Iniciando AURA.Api en $apiUrl ..." -ForegroundColor Cyan
$apiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", $apiProject, "--urls", $apiUrl) `
    -WorkingDirectory $repoRoot `
    -PassThru -WindowStyle Normal

Start-Sleep -Seconds 2
if ($apiProcess.HasExited) {
    throw "AURA.Api finalizo inmediatamente (codigo $($apiProcess.ExitCode)). Revisar la ventana de la API."
}

Write-Host "Iniciando AURA.Web en $webUrl ..." -ForegroundColor Cyan
$webProcess = Start-Process -FilePath "npm.cmd" `
    -ArgumentList @("run", "dev") `
    -WorkingDirectory $webDir `
    -PassThru -WindowStyle Normal

Start-Sleep -Seconds 2
if ($webProcess.HasExited) {
    throw "AURA.Web finalizo inmediatamente (codigo $($webProcess.ExitCode)). Revisar la ventana del frontend."
}

Write-Host ""
Write-Host "AURA en modo desarrollo:" -ForegroundColor Green
Write-Host "  API:      $apiUrl  (health: $apiUrl/health, PID $($apiProcess.Id))"
Write-Host "  Frontend: $webUrl  (PID $($webProcess.Id))"
Write-Host ""
Write-Host "Cada proceso corre en su propia ventana. Cerrar esas ventanas para detenerlos."
