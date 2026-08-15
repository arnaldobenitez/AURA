function Protect-AuraSecrets {
    param([string] $Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return $Text
    }

    $redacted = $Text
    $redacted = [regex]::Replace($redacted, '(?i)(Password|Pwd)\s*=\s*[^;]*', '$1=***')
    $redacted = [regex]::Replace($redacted, '(?i)(User Id|UID)\s*=\s*[^;]*', '$1=***')
    $redacted = [regex]::Replace($redacted, '(?i)(Server|Data Source)\s*=\s*[^;]*', '$1=***')
    return $redacted
}

function Get-AuraDevConnectionString {
    param([Parameter(Mandatory)] [string] $RepoRoot)

    if (-not [string]::IsNullOrWhiteSpace($env:AURA_DESIGNTIME_CONNSTR)) {
        return $env:AURA_DESIGNTIME_CONNSTR
    }

    $apiDir = Join-Path $RepoRoot "src\AURA.Api"
    Push-Location $apiDir
    try {
        $secretsOutput = dotnet user-secrets list 2>$null
    } finally {
        Pop-Location
    }

    foreach ($line in $secretsOutput) {
        if ($line -match '^ConnectionStrings:DefaultConnection\s*=\s*(.+)$') {
            return $Matches[1]
        }
    }

    return $null
}

function Test-AuraDatabaseName {
    param([Parameter(Mandatory)] [string] $ConnectionString)

    return $ConnectionString -match '(?i)(Database|Initial Catalog)\s*=\s*AURA\s*;'
}
