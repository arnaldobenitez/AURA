[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$cliProject = Join-Path $repoRoot "src\AURA.ProjectMemory.Cli\AURA.ProjectMemory.Cli.csproj"

if (-not (Test-Path -LiteralPath $cliProject -PathType Leaf)) {
    throw "Could not locate the AURA Project Memory CLI at '$cliProject'."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found in PATH. Install the SDK required by global.json or use a machine with dotnet available."
}

$cliDll = @(
    (Join-Path $repoRoot "src\AURA.ProjectMemory.Cli\bin\Debug\net10.0\aura.dll"),
    (Join-Path $repoRoot "src\AURA.ProjectMemory.Cli\bin\Release\net10.0\aura.dll")
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1

if ($null -ne $cliDll) {
    & dotnet $cliDll @Arguments
}
else {
    & dotnet run --project $cliProject -- @Arguments
}

exit $LASTEXITCODE
