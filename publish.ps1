$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $RepoRoot "src\TextLayer.App\TextLayer.App.csproj"
$OutputRoot = Join-Path $RepoRoot "dist"
$OutputPath = Join-Path $OutputRoot "TextLayer"
$RuntimeIdentifier = "win-x64"

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Could not find the TextLayer app project at '$ProjectPath'."
}

if (Test-Path -LiteralPath $OutputPath) {
    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    $resolvedRoot = [System.IO.Path]::GetFullPath($OutputRoot)
    if (-not $resolvedOutput.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete output outside the expected dist folder: '$resolvedOutput'."
    }

    Remove-Item -LiteralPath $OutputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

dotnet publish $ProjectPath `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained false `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "TextLayer published successfully." -ForegroundColor Green
Write-Host "Canonical app folder: $OutputPath"
Write-Host "Launch: .\Start-TextLayer.bat"
