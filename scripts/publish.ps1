#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet("auto", "patch", "minor", "major")]
    [string] $Bump = "auto",

    [string[]] $Notes = @(),

    [string] $Channel
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appversionPath = Join-Path $repoRoot "appversion.json"
$verifyScript = Join-Path $PSScriptRoot "verify.ps1"
$apiProject = Join-Path $repoRoot "src\AURA.Api\AURA.Api.csproj"
$webDistDir = Join-Path $repoRoot "src\AURA.Web\dist"
$versionesDir = Join-Path $repoRoot "Versiones"

# Patrones de archivos/carpetas que jamas deben llegar a un paquete distribuible.
$forbiddenPatterns = @(
    "appsettings.Development.json",
    "appsettings.Local.json",
    "appsettings.*.Local.json",
    ".env",
    ".env.*",
    "key-*.xml",
    ".dp-keys",
    "*.pfx",
    "*.pem",
    "tmp_*.*",
    "*.user"
)

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

function Remove-ForbiddenFiles {
    param(
        [Parameter(Mandatory)] [string] $Path
    )

    $removed = @()
    foreach ($pattern in $forbiddenPatterns) {
        $hits = Get-ChildItem -Path $Path -Filter $pattern -Recurse -Force -ErrorAction SilentlyContinue
        foreach ($hit in $hits) {
            $removed += $hit.FullName
            Remove-Item -LiteralPath $hit.FullName -Recurse -Force
        }
    }
    return $removed
}

function Find-ForbiddenFiles {
    param(
        [Parameter(Mandatory)] [string] $Path
    )

    $found = @()
    foreach ($pattern in $forbiddenPatterns) {
        $hits = Get-ChildItem -Path $Path -Filter $pattern -Recurse -Force -ErrorAction SilentlyContinue
        foreach ($hit in $hits) {
            $found += $hit.FullName
        }
    }
    return $found
}

# --- 1. Preflight ---------------------------------------------------------

Write-Host "AURA - Publicacion de build distribuible" -ForegroundColor Cyan

Push-Location $repoRoot
try {
    git rev-parse --is-inside-work-tree *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Este directorio no es un repositorio git. Ejecute 'git init' antes de publicar."
    }

    $pendingChanges = git status --porcelain
    if ($pendingChanges) {
        throw "Hay cambios sin confirmar en el arbol de trabajo. Commitee o descarte los cambios antes de publicar:`n$pendingChanges"
    }
} finally {
    Pop-Location
}

if (-not (Test-Path $appversionPath)) {
    throw "No se encontro appversion.json en $appversionPath."
}

$appversion = Get-Content -LiteralPath $appversionPath -Raw | ConvertFrom-Json

# --- 2. Resolver commit y tipo de bump -------------------------------------

$commitSha = (git -C $repoRoot rev-parse HEAD).Trim()
$rootCommit = (git -C $repoRoot rev-list --max-parents=0 HEAD).Trim()
$isBootstrap = [string]::IsNullOrWhiteSpace($appversion.lastPublishedCommit)
$diffAnchor = if ($isBootstrap) { $rootCommit } else { $appversion.lastPublishedCommit }

if ($Bump -ne "auto") {
    $bumpType = $Bump
} elseif ($isBootstrap) {
    $bumpType = "none"
} else {
    git -C $repoRoot cat-file -e "$($appversion.lastPublishedCommit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "lastPublishedCommit ($($appversion.lastPublishedCommit)) ya no existe en el historial. Vuelva a correr con -Bump explicito (patch|minor|major)."
    }
    $bumpType = $null # se resuelve mas abajo con la heuristica de diff
}

$filesChanged = 0
$totalLines = 0
$diffOutput = git -C $repoRoot diff --numstat "$diffAnchor..HEAD"
foreach ($line in $diffOutput) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split "`t"
    $filesChanged++
    $added = if ($parts[0] -eq "-") { 0 } else { [int]$parts[0] }
    $removed = if ($parts[1] -eq "-") { 0 } else { [int]$parts[1] }
    $totalLines += $added + $removed
}

if ($Bump -eq "auto" -and -not $isBootstrap) {
    $lastPublishedAt = [datetime]$appversion.lastPublishedAt
    $daysSince = ((Get-Date) - $lastPublishedAt).TotalDays
    if ($daysSince -le 14 -and $filesChanged -le 4 -and $totalLines -le 120) {
        $bumpType = "patch"
    } else {
        $bumpType = "minor"
    }
}

# --- 3. Calcular nueva version ---------------------------------------------

$versionParts = $appversion.version -split "\."
$major = [int]$versionParts[0]
$minor = [int]$versionParts[1]
$patch = [int]$versionParts[2]

switch ($bumpType) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
    "none"  { }
}

$newVersion = "{0}.{1}.{2:D2}" -f $major, $minor, $patch
$effectiveChannel = if ($Channel) { $Channel } else { $appversion.channel }

Write-Host ""
Write-Host "Version actual: $($appversion.version)  ->  Version nueva: $newVersion  (bump: $bumpType)" -ForegroundColor Cyan
Write-Host "Canal: $effectiveChannel" -ForegroundColor Cyan
Write-Host "Diff vs $diffAnchor : $filesChanged archivo(s), $totalLines linea(s)" -ForegroundColor Cyan

# --- 4. Gate de calidad (verify.ps1 como proceso hijo) ----------------------

Invoke-Step "Verificacion (verify.ps1)" {
    powershell -NoProfile -ExecutionPolicy Bypass -File $verifyScript
}

# --- 5. Publish del backend --------------------------------------------------

$publishTempDir = Join-Path ([System.IO.Path]::GetTempPath()) "aura-publish-api-$(Get-Date -Format yyyyMMddHHmmss)"

Invoke-Step "dotnet publish (AURA.Api)" {
    dotnet publish $apiProject -c Release -o $publishTempDir --nologo
}

$removedFromTemp = Remove-ForbiddenFiles -Path $publishTempDir
foreach ($file in $removedFromTemp) {
    Write-Host "  Eliminado del publish temporal: $file" -ForegroundColor Yellow
}

# --- 6. Frontend (dist/ ya generado por verify.ps1) --------------------------

if (-not (Test-Path $webDistDir)) {
    throw "No se encontro $webDistDir. El paso de verificacion deberia haber generado el build del frontend."
}

$removedFromDist = Remove-ForbiddenFiles -Path $webDistDir
foreach ($file in $removedFromDist) {
    Write-Host "  Eliminado de dist/: $file" -ForegroundColor Yellow
}

# --- 7. Carpeta de version ----------------------------------------------------

$folderName = "$(Get-Date -Format 'dd.MM.yyyy') v.$newVersion"
$destRoot = Join-Path $versionesDir $folderName
$destApi = Join-Path $destRoot "Api"
$destWeb = Join-Path $destRoot "Web"

if (Test-Path $destRoot) {
    Write-Host ""
    Write-Host "Carpeta de version existente detectada (intento previo fallido); se recrea limpia: $destRoot" -ForegroundColor Yellow
    Remove-Item -LiteralPath $destRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $destApi -Force | Out-Null
New-Item -ItemType Directory -Path $destWeb -Force | Out-Null

Copy-Item -Path (Join-Path $publishTempDir "*") -Destination $destApi -Recurse -Force
Copy-Item -Path (Join-Path $webDistDir "*") -Destination $destWeb -Recurse -Force

# --- 8. changes.log ------------------------------------------------------------

$notesText = if ($Notes.Count -gt 0) { ($Notes | ForEach-Object { "  - $_" }) -join "`n" } else { "  (completar)" }

$changesLog = @"
AURA - Registro de publicacion
================================
Version:        $newVersion
Canal:          $effectiveChannel
Fecha:          $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Tipo de bump:   $bumpType
Commit:         $commitSha
Diff vs:        $diffAnchor
Archivos:       $filesChanged
Lineas totales: $totalLines

Notas de release:
$notesText
"@

Set-Content -LiteralPath (Join-Path $destRoot "changes.log") -Value $changesLog -Encoding UTF8

# --- 9. Gate de seguridad final --------------------------------------------

$offending = Find-ForbiddenFiles -Path $destRoot
if ($offending.Count -gt 0) {
    Remove-Item -LiteralPath $destRoot -Recurse -Force
    Remove-Item -LiteralPath $publishTempDir -Recurse -Force -ErrorAction SilentlyContinue
    throw "Gate de seguridad: se encontraron archivos prohibidos en el paquete final. Publicacion abortada, appversion.json intacto.`n$($offending -join "`n")"
}

# --- 10. Confirmacion atomica de la version ---------------------------------

$appversion.version = $newVersion
$appversion.channel = $effectiveChannel
$appversion.lastPublishedCommit = $commitSha
$appversion.lastPublishedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")

$appversionTmpPath = "$appversionPath.tmp"
$appversion | ConvertTo-Json | Set-Content -LiteralPath $appversionTmpPath -Encoding UTF8
Move-Item -LiteralPath $appversionTmpPath -Destination $appversionPath -Force

# --- 11. Limpieza y resumen --------------------------------------------------

Remove-Item -LiteralPath $publishTempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Publicacion completa" -ForegroundColor Green
Write-Host "  Version: $newVersion ($effectiveChannel)" -ForegroundColor Green
Write-Host "  Carpeta: $destRoot" -ForegroundColor Green
Write-Host "  Commit:  $commitSha" -ForegroundColor Green
