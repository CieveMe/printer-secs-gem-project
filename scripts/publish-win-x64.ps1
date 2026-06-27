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

$localDotnetCandidates = @(
    (Join-Path $workspaceDir "dotnet-sdk-8.0.421-win-x64\dotnet.exe"),
    (Join-Path $workspaceDir ".dotnet-sdk\dotnet.exe")
)
$dotnet = ($localDotnetCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    $dotnet = "dotnet"
}

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

function Assert-Utf8NoBomFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file was not found: $Path"
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw "File must be UTF-8 without BOM, but UTF-8 BOM was found: $Path"
    }
}

function Assert-RequiredPublishItem {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required publish item was not found: $Path"
    }
}

function Assert-NoKnownMojibakeText {
    param([Parameter(Mandatory = $true)][string]$Path)

    $text = [System.IO.File]::ReadAllText($Path, [System.Text.UTF8Encoding]::new($false, $true))
    $knownBadPhrases = @(
        ([string][char]0x6924 + [string][char]0x5815 + [string][char]0x5134),
        ([string][char]0x93C9 + [string][char]0xFF04 + [string][char]0x721C),
        [string][char]0xFFFD)
    foreach ($phrase in $knownBadPhrases) {
        if ($text.Contains([string]$phrase)) {
            throw "Known mojibake text was found in $Path"
        }
    }
}

$nugetConfig = Join-Path $repoDir "NuGet.Config"
& $dotnet publish $ProjectPath --configuration Release --runtime win-x64 --self-contained false --output $OutputPath --configfile $nugetConfig

foreach ($generatedConfig in @("PrinterSecsGem.Eq.dll.config", "PrinterSecsGem.Eq.exe.config")) {
    $generatedConfigPath = Join-Path $OutputPath $generatedConfig
    if (Test-Path $generatedConfigPath) {
        Remove-Item -LiteralPath $generatedConfigPath -Force
        Write-Host "Removed generated config file $generatedConfigPath; customer-facing config is App.config"
    }
}

Assert-Utf8NoBomFile (Join-Path $OutputPath "App.config")
Assert-NoKnownMojibakeText (Join-Path $OutputPath "App.config")

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

foreach ($requiredItem in @(
    "PrinterSecsGem.Eq.exe",
    "App.config",
    "log4net.config",
    "stop-printer-secs-gem.cmd",
    "zebra-command-line",
    "secs-simulator"
)) {
    Assert-RequiredPublishItem (Join-Path $OutputPath $requiredItem)
}

foreach ($forbiddenItem in @("PrinterSecsGem.Eq.dll.config", "PrinterSecsGem.Eq.exe.config")) {
    $forbiddenPath = Join-Path $OutputPath $forbiddenItem
    if (Test-Path -LiteralPath $forbiddenPath) {
        throw "Forbidden generated config file remains in publish output: $forbiddenPath"
    }
}

Write-Host "Published to $OutputPath"
