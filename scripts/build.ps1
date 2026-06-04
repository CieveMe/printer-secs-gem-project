param(
    [string]$SolutionPath = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoDir = Resolve-Path (Join-Path $scriptDir "..")
$workspaceDir = Resolve-Path (Join-Path $repoDir "..")

if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $SolutionPath = Join-Path $repoDir "PrinterSecsGem.sln"
}

$localDotnet = Join-Path $workspaceDir ".dotnet-sdk\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }

$env:DOTNET_CLI_HOME = Join-Path $workspaceDir ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $workspaceDir ".nuget-packages"
$env:APPDATA = Join-Path $workspaceDir ".appdata\Roaming"
$env:LOCALAPPDATA = Join-Path $workspaceDir ".appdata\Local"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:APPDATA, $env:LOCALAPPDATA | Out-Null

if ($dotnet -eq "dotnet" -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found. Install .NET 8 SDK first."
}

$sdkList = & $dotnet --list-sdks
if (-not $sdkList) {
    throw ".NET SDK was not found. Install .NET 8 SDK first, not only the runtime."
}

$nugetConfig = Join-Path $repoDir "NuGet.Config"
& $dotnet restore $SolutionPath --configfile $nugetConfig
& $dotnet build $SolutionPath --configuration Release --no-restore
