# ── AIchivist Installer Build Script ───────────────────────────────────────
# Builds the full installer from source: frontend → backend → wwwroot → tests → Inno Setup
#
# Prerequisites:
#   - Node.js + npm
#   - .NET 10 SDK
#   - Inno Setup 6 (ISCC.exe on PATH or at default location)
#   - PostgreSQL 16 binaries in installer/pgsql/ (download from EDB)
#
# Usage: .\build-installer.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$frontendDir = Join-Path $root "frontend"
$backendDir = Join-Path $root "backend"
$apiProject = Join-Path $backendDir "ArchiveSearch.API"
$testProject = Join-Path $backendDir "ArchiveSearch.Tests"
$installerDir = Join-Path $root "installer"
$publishDir = Join-Path $installerDir "publish"
$wwwrootDir = Join-Path $publishDir "wwwroot"
$pgsqlDir = Join-Path $installerDir "pgsql"
$outputDir = Join-Path $installerDir "output"

$startTime = Get-Date

function Assert-Step {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        Write-Host "BUILD VALIDATION FAILED: $Message" -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "===== AIchivist Installer Build =====" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Build Angular frontend ────────────────────────────────────────
Write-Host "Step 1: Building Angular frontend..." -ForegroundColor Yellow
Push-Location $frontendDir
npm ci --silent 2>&1 | Out-Null
npm run build 2>&1
$frontendExitCode = $LASTEXITCODE
Pop-Location

Assert-Step ($frontendExitCode -eq 0) "Frontend build succeeded"
$indexHtml = Join-Path $frontendDir "dist\frontend\browser\index.html"
Assert-Step (Test-Path $indexHtml) "index.html exists in frontend dist output"

# ── Step 2: Run backend tests ─────────────────────────────────────────────
Write-Host ""
Write-Host "Step 2: Running backend tests..." -ForegroundColor Yellow
dotnet test $testProject --verbosity quiet 2>&1
Assert-Step ($LASTEXITCODE -eq 0) "All backend tests passed"

# ── Step 3: Publish .NET backend (self-contained, win-x64) ───────────────
Write-Host ""
Write-Host "Step 3: Publishing .NET backend..." -ForegroundColor Yellow

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $apiProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir 2>&1

Assert-Step ($LASTEXITCODE -eq 0) "dotnet publish succeeded"

$exePath = Join-Path $publishDir "AIchivist.exe"
Assert-Step (Test-Path $exePath) "AIchivist.exe exists in publish output"

$exeSizeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
Assert-Step ($exeSizeMB -gt 20 -and $exeSizeMB -lt 200) "Exe size is ${exeSizeMB}MB (expected 20-200MB)"

# ── Step 4: Copy Angular build output to wwwroot ──────────────────────────
Write-Host ""
Write-Host "Step 4: Copying frontend to wwwroot..." -ForegroundColor Yellow

$angularDist = Join-Path $frontendDir "dist\frontend\browser"
if (Test-Path $wwwrootDir) { Remove-Item $wwwrootDir -Recurse -Force }
Copy-Item $angularDist $wwwrootDir -Recurse

Assert-Step (Test-Path (Join-Path $wwwrootDir "index.html")) "index.html present in wwwroot"

$wwwrootFiles = (Get-ChildItem $wwwrootDir -Recurse -File).Count
Assert-Step ($wwwrootFiles -gt 1) "wwwroot has $wwwrootFiles files"

# ── Step 5: Verify PostgreSQL binaries ────────────────────────────────────
Write-Host ""
Write-Host "Step 5: Verifying PostgreSQL binaries..." -ForegroundColor Yellow

$pgCtl = Join-Path $pgsqlDir "bin\pg_ctl.exe"
if (Test-Path $pgCtl) {
    Assert-Step $true "PostgreSQL binaries found at $pgsqlDir"
} else {
    Write-Host "  [SKIP] PostgreSQL binaries not found at $pgsqlDir" -ForegroundColor DarkYellow
    Write-Host "         Download from https://www.enterprisedb.com/download-postgresql-binaries" -ForegroundColor DarkYellow
    Write-Host "         Extract pgsql/ directory to: $pgsqlDir" -ForegroundColor DarkYellow
}

# ── Step 6: Compile Inno Setup installer ──────────────────────────────────
Write-Host ""
Write-Host "Step 6: Compiling Inno Setup installer..." -ForegroundColor Yellow

$issFile = Join-Path $installerDir "AIchivist.iss"
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $null
foreach ($path in $isccPaths) {
    if (Test-Path $path) { $iscc = $path; break }
}
# Also check PATH
if (-not $iscc) {
    $iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
}

if ($iscc) {
    & $iscc $issFile
    $installerExe = Join-Path $outputDir "AIchivist-Setup-1.0.0.exe"
    Assert-Step (Test-Path $installerExe) "Installer exe created"

    $installerSizeMB = [math]::Round((Get-Item $installerExe).Length / 1MB, 1)
    Write-Host "  Installer size: ${installerSizeMB}MB" -ForegroundColor Gray
} else {
    Write-Host "  [SKIP] Inno Setup not found. Install from https://jrsoftware.org/isinfo.php" -ForegroundColor DarkYellow
    Write-Host "         The publish output is ready at: $publishDir" -ForegroundColor DarkYellow
}

# ── Summary ───────────────────────────────────────────────────────────────
$elapsed = (Get-Date) - $startTime
Write-Host ""
Write-Host "===== Build Complete =====" -ForegroundColor Cyan
Write-Host "  Duration: $([math]::Round($elapsed.TotalSeconds, 1))s" -ForegroundColor Gray
Write-Host "  Publish dir: $publishDir" -ForegroundColor Gray
Write-Host "  AIchivist.exe: ${exeSizeMB}MB" -ForegroundColor Gray
Write-Host "  wwwroot files: $wwwrootFiles" -ForegroundColor Gray

if ($iscc -and (Test-Path (Join-Path $outputDir "AIchivist-Setup-1.0.0.exe"))) {
    Write-Host "  Installer: $(Join-Path $outputDir 'AIchivist-Setup-1.0.0.exe')" -ForegroundColor Green
} else {
    Write-Host "  Installer: [not built - install Inno Setup 6 or add PostgreSQL binaries]" -ForegroundColor DarkYellow
}

Write-Host ""
