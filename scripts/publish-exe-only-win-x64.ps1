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
    $OutputPath = Join-Path $repoDir "publish\exe-only-win-x64"
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

$runtimeFrameworkVersion = "8.0.26"
$nugetDirectSource = Join-Path $repoDir "build-cache\nuget-direct"
$nugetOfflineSource = Join-Path $workspaceDir "nuget-offline"
$localPackageSource = Join-Path $workspaceDir ".nuget-packages"
$restoreSources = @($nugetDirectSource, $nugetOfflineSource, $localPackageSource) | Where-Object { Test-Path -LiteralPath $_ }
if ($restoreSources.Count -eq 0) {
    throw "No local NuGet sources were found for exe-only publish."
}

& $dotnet restore $ProjectPath `
    --runtime win-x64 `
    --ignore-failed-sources `
    -p:RuntimeFrameworkVersion=$runtimeFrameworkVersion `
    @($restoreSources | ForEach-Object { @("--source", $_) })

& $dotnet publish $ProjectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --no-restore `
    --output $OutputPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:RuntimeFrameworkVersion=$runtimeFrameworkVersion

$exePath = Join-Path $OutputPath "PrinterSecsGem.Eq.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Single-file exe was not found: $exePath"
}

$exeLength = (Get-Item -LiteralPath $exePath).Length
if ($exeLength -lt 1000000) {
    throw "Published exe is too small for exe-only delivery: $exeLength bytes"
}

foreach ($unexpectedItem in @(
    "PrinterSecsGem.Eq.dll",
    "PrinterSecsGem.Eq.deps.json",
    "PrinterSecsGem.Eq.runtimeconfig.json"
)) {
    $unexpectedPath = Join-Path $OutputPath $unexpectedItem
    if (Test-Path -LiteralPath $unexpectedPath) {
        throw "Unexpected app runtime file remains in exe-only output: $unexpectedPath"
    }
}

Get-ChildItem -LiteralPath $OutputPath -Force |
    Where-Object { $_.Name -ne "PrinterSecsGem.Eq.exe" } |
    Remove-Item -Recurse -Force

Write-Host "Published exe-only output to $OutputPath"
Write-Host "Exe: $exePath"
