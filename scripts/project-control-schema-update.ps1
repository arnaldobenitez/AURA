#requires -Version 5.1
[CmdletBinding()]
param(
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "PersistenceCommon.ps1")

$apiProject = Join-Path $repoRoot "src\AURA.Api\AURA.Api.csproj"

$connectionString = Get-AuraDevConnectionString -RepoRoot $repoRoot

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    Write-Host "No se encontro una connection string de desarrollo." -ForegroundColor Red
    Write-Host "Definí la variable de entorno AURA_DESIGNTIME_CONNSTR, o cargala en User Secrets de AURA.Api:" -ForegroundColor Red
    Write-Host "  dotnet user-secrets set ""ConnectionStrings:DefaultConnection"" ""<CONNECTION_STRING_DE_DESARROLLO>"" --project src/AURA.Api" -ForegroundColor Red
    exit 1
}

if (-not (Test-AuraDatabaseName -ConnectionString $connectionString)) {
    Write-Host "ADVERTENCIA: la connection string configurada no apunta a la base 'AURA'." -ForegroundColor Yellow
    if (-not $Force) {
        Write-Host "Abortando para evitar aplicar el esquema de control de proyectos sobre una base incorrecta. Volvé a ejecutar con -Force si es intencional." -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Aplicando esquema aditivo y datos iniciales de Control de Proyectos IA sobre 'AURA'..." -ForegroundColor Cyan
Write-Host "(Sólo agrega columnas/índices nuevos e inserta/actualiza los 6 proyectos del tenant 1; no borra ni recrea nada.)" -ForegroundColor Cyan

$env:ConnectionStrings__DefaultConnection = $connectionString
$output = dotnet run --project $apiProject -- --apply-project-control-schema 2>&1

$sanitized = Protect-AuraSecrets ($output | Out-String)
Write-Host $sanitized

if ($LASTEXITCODE -ne 0) {
    Write-Host "FALLO al aplicar el esquema de Control de Proyectos IA (codigo $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Esquema y datos iniciales de Control de Proyectos IA aplicados correctamente." -ForegroundColor Green
exit 0
