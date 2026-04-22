$ErrorActionPreference = "Stop"

$InstallerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $InstallerRoot
$PayloadPath = Join-Path $RepoRoot "dist\TextLayer"
$AppExePath = Join-Path $PayloadPath "TextLayer.exe"
$EngDataPath = Join-Path $PayloadPath "tessdata\eng.traineddata"
$RusDataPath = Join-Path $PayloadPath "tessdata\rus.traineddata"
$ScriptPath = Join-Path $InstallerRoot "TextLayer.iss"
$OutputPath = Join-Path $RepoRoot "release\TextLayer-Setup-0.1.0.exe"

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw $Message
    }
}

function Find-InnoSetupCompiler {
    if (-not [string]::IsNullOrWhiteSpace($env:INNO_SETUP_COMPILER)) {
        if (Test-Path -LiteralPath $env:INNO_SETUP_COMPILER) {
            return $env:INNO_SETUP_COMPILER
        }

        throw "INNO_SETUP_COMPILER is set but does not point to a file: '$env:INNO_SETUP_COMPILER'."
    }

    $fromPath = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $standardPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $standardPaths) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup compiler was not found. Install Inno Setup 6 or set INNO_SETUP_COMPILER to ISCC.exe."
}

Assert-PathExists -Path $PayloadPath -Message "Missing canonical payload folder: '$PayloadPath'. Run publish.bat from the repository root first."
Assert-PathExists -Path $AppExePath -Message "Missing published app executable: '$AppExePath'. Run publish.bat from the repository root first."
Assert-PathExists -Path $EngDataPath -Message "Missing OCR data file: '$EngDataPath'. The installer payload is incomplete."
Assert-PathExists -Path $RusDataPath -Message "Missing OCR data file: '$RusDataPath'. The installer payload is incomplete."
Assert-PathExists -Path $ScriptPath -Message "Missing Inno Setup script: '$ScriptPath'."

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null

if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Force
}

$compiler = Find-InnoSetupCompiler
Write-Host "Using Inno Setup compiler: $compiler"
Write-Host "Payload: $PayloadPath"

& $compiler $ScriptPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Assert-PathExists -Path $OutputPath -Message "Installer compiler completed but expected output was not found: '$OutputPath'."

Write-Host ""
Write-Host "TextLayer installer built successfully." -ForegroundColor Green
Write-Host "Installer: $OutputPath"
