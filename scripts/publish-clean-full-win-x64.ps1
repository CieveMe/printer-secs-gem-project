param(
    [string]$ProjectPath = "",
    [string]$OutputPath = "",
    [string]$ExeOnlyOutputPath = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoDir = Resolve-Path (Join-Path $scriptDir "..")
$workspaceDir = Resolve-Path (Join-Path $repoDir "..")

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoDir "src\PrinterSecsGem.Eq\PrinterSecsGem.Eq.csproj"
}

if ([string]::IsNullOrWhiteSpace($ExeOnlyOutputPath)) {
    $ExeOnlyOutputPath = Join-Path $repoDir "publish\exe-only-win-x64"
}

function Get-ProjectVersion {
    param([Parameter(Mandatory = $true)][string]$Path)

    $projectText = [System.IO.File]::ReadAllText($Path)
    if ($projectText -match "<Version>([^<]+)</Version>") {
        return $Matches[1]
    }

    return "unknown"
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

function Assert-NoRootFilesByExtension {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Extension
    )

    $matches = Get-ChildItem -LiteralPath $Path -File -Filter "*$Extension"
    if ($matches.Count -gt 0) {
        throw "Unexpected $Extension file found in customer package root: $($matches.Name -join ', ')"
    }
}

function Assert-RequiredItem {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required customer package item was not found: $Path"
    }
}

$version = Get-ProjectVersion -Path $ProjectPath
$safeVersion = $version -replace "[^0-9A-Za-z._-]", "_"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoDir "publish\printer-secs-gem-clean-full-v$safeVersion-$timestamp"
}

if (Test-Path -LiteralPath $OutputPath) {
    throw "Output path already exists. Choose a new OutputPath or delete it manually: $OutputPath"
}

$exeOnlyScript = Join-Path $scriptDir "publish-exe-only-win-x64.ps1"
if (-not (Test-Path -LiteralPath $exeOnlyScript)) {
    throw "Exe-only publish script was not found: $exeOnlyScript"
}

& $exeOnlyScript -ProjectPath $ProjectPath -OutputPath $ExeOnlyOutputPath

$exeSource = Join-Path $ExeOnlyOutputPath "PrinterSecsGem.Eq.exe"
Assert-RequiredItem $exeSource

$exeLength = (Get-Item -LiteralPath $exeSource).Length
if ($exeLength -lt 1000000) {
    throw "Published exe is too small for exe-only delivery: $exeLength bytes"
}

$sourceDir = Join-Path $repoDir "src\PrinterSecsGem.Eq"
$zebraSdkSource = Join-Path $workspaceDir "v4.0.3435\command_line"
if (-not (Test-Path -LiteralPath (Join-Path $zebraSdkSource "SdkApi.Desktop.CommandLine.dll"))) {
    throw "Zebra command line SDK was not found: $zebraSdkSource"
}

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
Copy-Item -LiteralPath $exeSource -Destination (Join-Path $OutputPath "PrinterSecsGem.Eq.exe") -Force
Copy-Item -LiteralPath (Join-Path $sourceDir "App.config") -Destination (Join-Path $OutputPath "App.config") -Force
Copy-Item -LiteralPath (Join-Path $sourceDir "log4net.config") -Destination (Join-Path $OutputPath "log4net.config") -Force
Copy-Item -LiteralPath (Join-Path $sourceDir "CUSTOMER_README_CN.txt") -Destination (Join-Path $OutputPath "CUSTOMER_README_CN.txt") -Force

$zebraSdkTarget = Join-Path $OutputPath "zebra-command-line"
New-Item -ItemType Directory -Force -Path $zebraSdkTarget | Out-Null
Copy-Item -Path (Join-Path $zebraSdkSource "*") -Destination $zebraSdkTarget -Recurse -Force

foreach ($requiredItem in @(
    "PrinterSecsGem.Eq.exe",
    "App.config",
    "log4net.config",
    "CUSTOMER_README_CN.txt",
    "zebra-command-line"
)) {
    Assert-RequiredItem (Join-Path $OutputPath $requiredItem)
}

Assert-Utf8NoBomFile (Join-Path $OutputPath "App.config")
Assert-Utf8NoBomFile (Join-Path $OutputPath "log4net.config")
Assert-Utf8NoBomFile (Join-Path $OutputPath "CUSTOMER_README_CN.txt")

Assert-NoRootFilesByExtension -Path $OutputPath -Extension ".dll"
Assert-NoRootFilesByExtension -Path $OutputPath -Extension ".cmd"
Assert-NoRootFilesByExtension -Path $OutputPath -Extension ".ps1"

foreach ($unexpectedItem in @(
    "PrinterSecsGem.Eq.dll",
    "PrinterSecsGem.Eq.deps.json",
    "PrinterSecsGem.Eq.runtimeconfig.json"
)) {
    $unexpectedPath = Join-Path $OutputPath $unexpectedItem
    if (Test-Path -LiteralPath $unexpectedPath) {
        throw "Unexpected app runtime file remains in customer package: $unexpectedPath"
    }
}

$zipPath = "$OutputPath.zip"
if (Test-Path -LiteralPath $zipPath) {
    throw "Zip path already exists: $zipPath"
}

Compress-Archive -Path (Join-Path $OutputPath "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Published clean full customer package to $OutputPath"
Write-Host "Zip: $zipPath"
Write-Host "Version: v$version"
