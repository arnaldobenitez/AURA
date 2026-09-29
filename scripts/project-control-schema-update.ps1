#requires -Version 5.1
[CmdletBinding()]
param(
    [switch] $Force,
    [switch] $SchemaOnly,
    [switch] $BackupVerified,
    [string] $TargetServer
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "PersistenceCommon.ps1")

$apiProject = Join-Path $repoRoot "src\AURA.Api\AURA.Api.csproj"

if (-not $BackupVerified) {
    throw "Antes de aplicar cambios, cree un backup de la base AURA y verifique el backup (RESTORE VERIFYONLY); luego ejecute con -BackupVerified. El script no crea ni verifica el backup por sí mismo."
}

if ($SchemaOnly) {
    if ($Force) {
        throw "-Force no se admite en modo SchemaOnly. El destino debe coincidir exactamente con AURA en -TargetServer."
    }
    $connectionString = $env:AURA_SCHEMA_MIGRATION_CONNSTR
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw "SchemaOnly requiere la variable temporal AURA_SCHEMA_MIGRATION_CONNSTR; no se usa User Secrets ni appsettings.json."
    }
    if ([string]::IsNullOrWhiteSpace($TargetServer)) {
        throw "SchemaOnly requiere -TargetServer para validar el destino antes de conectar."
    }

    $connectionInfo = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionString)
    $actualServer = $connectionInfo.DataSource -replace '^(?i:tcp):', ''
    if ($actualServer -ine $TargetServer -or $connectionInfo.InitialCatalog -ine "AURA") {
        throw "La conexión no coincide con el servidor/base AURA declarados; se aborta sin conectar."
    }
} else {
    $connectionString = Get-AuraDevConnectionString -RepoRoot $repoRoot

    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        Write-Host "No se encontro una connection string de desarrollo." -ForegroundColor Red
        Write-Host "Definí la variable de entorno AURA_DESIGNTIME_CONNSTR, o cargala en User Secrets de AURA.Api:" -ForegroundColor Red
        Write-Host "  dotnet user-secrets set ""ConnectionStrings:DefaultConnection"" ""<CONNECTION_STRING_DE_DESARROLLO>"" --project src/AURA.Api" -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-AuraDatabaseName -ConnectionString $connectionString)) {
    Write-Host "ADVERTENCIA: la connection string configurada no apunta a la base 'AURA'." -ForegroundColor Yellow
    if (-not $Force) {
        Write-Host "Abortando para evitar aplicar el esquema de control de proyectos sobre una base incorrecta. Volvé a ejecutar con -Force si es intencional." -ForegroundColor Yellow
        exit 1
    }
}

if ($SchemaOnly) {
    Write-Host "Aplicando sólo cambios aditivos de esquema de ProjectControl sobre 'AURA' en el servidor validado..." -ForegroundColor Cyan
} else {
    Write-Host "Aplicando esquema aditivo y datos iniciales de Control de Proyectos IA sobre 'AURA'..." -ForegroundColor Cyan
    Write-Host "(También inserta/actualiza datos iniciales; no borra ni recrea nada.)" -ForegroundColor Cyan
}

$env:ConnectionStrings__DefaultConnection = $connectionString
$migrationArgument = if ($SchemaOnly) { "--apply-project-control-schema-only" } else { "--apply-project-control-schema" }
$output = dotnet run --project $apiProject -- $migrationArgument 2>&1

$sanitized = Protect-AuraSecrets ($output | Out-String)
Write-Host $sanitized

if ($LASTEXITCODE -ne 0) {
    Write-Host "FALLO al aplicar el esquema de Control de Proyectos IA (codigo $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

if ($SchemaOnly) {
    Write-Host "Esquema de Control de Proyectos IA aplicado y verificado sin semillas de datos." -ForegroundColor Green
} else {
    Write-Host "Esquema y datos iniciales de Control de Proyectos IA aplicados correctamente." -ForegroundColor Green
}
exit 0
