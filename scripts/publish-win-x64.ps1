param(
    [string]$ProjectPath = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoDir = Resolve-Path (Join-Path $scriptDir "..")
$workspaceDir = Resolve-Path (Join-Path $repoDir "..")

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoDir "src\PrinterSecsGem.Eq\PrinterSecsGem.Eq.csproj"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoDir "publish\win-x64"
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
& $dotnet publish $ProjectPath --configuration Release --runtime win-x64 --self-contained false --output $OutputPath --configfile $nugetConfig

$zebraSdkSource = Join-Path $workspaceDir "v4.0.3435\command_line"
$zebraSdkDll = Join-Path $zebraSdkSource "SdkApi.Desktop.CommandLine.dll"
if (Test-Path $zebraSdkDll) {
    $zebraSdkTarget = Join-Path $OutputPath "zebra-command-line"
    New-Item -ItemType Directory -Force -Path $zebraSdkTarget | Out-Null
    Copy-Item -Path (Join-Path $zebraSdkSource "*") -Destination $zebraSdkTarget -Recurse -Force
    Write-Host "Copied Zebra command line SDK to $zebraSdkTarget"
} else {
    Write-Warning "Zebra command line SDK was not found at $zebraSdkSource"
}

$secsSimulatorSamples = Join-Path $repoDir "samples\secs"
if (Test-Path $secsSimulatorSamples) {
    $secsSimulatorTarget = Join-Path $OutputPath "secs-simulator"
    New-Item -ItemType Directory -Force -Path $secsSimulatorTarget | Out-Null
    Copy-Item -Path (Join-Path $secsSimulatorSamples "*.SMD") -Destination $secsSimulatorTarget -Force
    Write-Host "Copied SECS simulator samples to $secsSimulatorTarget"
}

Write-Host "Published to $OutputPath"
