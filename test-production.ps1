<#
.SYNOPSIS
    Local production testing script for AIchivist on-demand PostgreSQL management.

.DESCRIPTION
    This script creates a local production-like environment to test the PostgreSQL
    on-demand process management without needing to build the full installer.

    It performs the following:
    1. Publishes the backend in Release mode
    2. Copies frontend build to wwwroot
    3. Sets up PostgreSQL binaries and data directory
    4. Creates production configuration
    5. Launches AIchivist.exe for testing

.EXAMPLE
    .\test-production.ps1
#>

param(
    [switch]$SkipBuild,
    [switch]$SkipFrontend,
    [switch]$ReinitDatabase
)

$ErrorActionPreference = "Stop"

# ── Helper Functions ────────────────────────────────────────────────────────

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Assert-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    try {
        & $Action
        Write-Success $Name
    }
    catch {
        Write-ErrorMsg "$Name failed: $_"
        exit 1
    }
}

# ── Configuration ──────────────────────────────────────────────────────────

$rootDir = $PSScriptRoot
$testPublishDir = Join-Path $rootDir "test-publish"
$backendDir = Join-Path $rootDir "backend"
$frontendDir = Join-Path $rootDir "frontend"
$installerDir = Join-Path $rootDir "installer"
$apiProject = Join-Path $backendDir "ArchiveSearch.API\ArchiveSearch.API.csproj"

# ── Pre-flight Checks ─────────────────────────────────────────────────────

Write-Step "Pre-flight checks"

# Check for required tools
Assert-Step "Checking for .NET SDK" {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw ".NET SDK not found. Install from https://dot.net"
    }
}

Assert-Step "Checking for Node.js" {
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        throw "Node.js not found. Install from https://nodejs.org"
    }
}

# Check for PostgreSQL binaries
$pgsqlSourceDir = Join-Path $installerDir "pgsql"
$pgCtl = Join-Path $pgsqlSourceDir "bin\pg_ctl.exe"

Assert-Step "Checking for PostgreSQL binaries" {
    if (-not (Test-Path $pgCtl)) {
        throw "PostgreSQL binaries not found at $pgsqlSourceDir. Extract PostgreSQL 16 binaries there first."
    }
}

# Check ports are available
Write-Step "Checking port availability"
$portsInUse = netstat -ano | Select-String -Pattern ":(5265|5433)\s" | Out-String
if ($portsInUse -match ":5265") {
    Write-ErrorMsg "Port 5265 (app) is already in use. Stop any running AIchivist instances."
    exit 1
}
if ($portsInUse -match ":5433") {
    Write-ErrorMsg "Port 5433 (PostgreSQL) is already in use. Stop any running PostgreSQL on port 5433."
    exit 1
}
Write-Success "Ports 5265 and 5433 are available"

# ── Step 1: Build Frontend ────────────────────────────────────────────────

if (-not $SkipFrontend) {
    Write-Step "Building frontend (Angular 21)"

    Assert-Step "npm install" {
        Push-Location $frontendDir
        try {
            npm install --silent 2>&1 | Out-Null
        }
        finally {
            Pop-Location
        }
    }

    Assert-Step "npm run build" {
        Push-Location $frontendDir
        try {
            npm run build --silent 2>&1 | Out-Null
            if (-not (Test-Path "dist\frontend\browser\index.html")) {
                throw "Frontend build did not produce index.html"
            }
        }
        finally {
            Pop-Location
        }
    }
}
else {
    Write-Step "Skipping frontend build (--SkipFrontend)"
    if (-not (Test-Path (Join-Path $frontendDir "dist\frontend\browser\index.html"))) {
        Write-ErrorMsg "Frontend build not found and --SkipFrontend specified"
        exit 1
    }
}

# ── Step 2: Publish Backend ───────────────────────────────────────────────

if (-not $SkipBuild) {
    Write-Step "Publishing backend (.NET 10 Release)"

    Assert-Step "dotnet publish" {
        dotnet publish $apiProject `
            -c Release `
            -r win-x64 `
            --self-contained true `
            -o $testPublishDir `
            --nologo `
            -v quiet

        if (-not (Test-Path (Join-Path $testPublishDir "AIchivist.exe"))) {
            throw "Publish did not produce AIchivist.exe"
        }
    }
}
else {
    Write-Step "Skipping backend build (--SkipBuild)"
    if (-not (Test-Path (Join-Path $testPublishDir "AIchivist.exe"))) {
        Write-ErrorMsg "AIchivist.exe not found and --SkipBuild specified"
        exit 1
    }
}

# ── Step 3: Copy Frontend to wwwroot ──────────────────────────────────────

Write-Step "Copying frontend build to wwwroot"

Assert-Step "Copy Angular output" {
    $frontendBuildDir = Join-Path $frontendDir "dist\frontend\browser"
    $wwwrootDir = Join-Path $testPublishDir "wwwroot"

    if (Test-Path $wwwrootDir) {
        Remove-Item $wwwrootDir -Recurse -Force
    }

    Copy-Item $frontendBuildDir -Destination $wwwrootDir -Recurse

    if (-not (Test-Path (Join-Path $wwwrootDir "index.html"))) {
        throw "Failed to copy frontend to wwwroot"
    }
}

# ── Step 4: Setup PostgreSQL ──────────────────────────────────────────────

Write-Step "Setting up PostgreSQL for test environment"

$pgsqlDestDir = Join-Path $testPublishDir "pgsql"
$dataDir = Join-Path $pgsqlDestDir "data"

# Copy PostgreSQL binaries if not already present
if (-not (Test-Path $pgsqlDestDir)) {
    Assert-Step "Copy PostgreSQL binaries" {
        Copy-Item $pgsqlSourceDir -Destination $pgsqlDestDir -Recurse
    }
}
else {
    Write-Success "PostgreSQL binaries already present"
}

# Initialize or reinitialize data directory
if ($ReinitDatabase -or -not (Test-Path (Join-Path $dataDir "PG_VERSION"))) {
    Write-Step "Initializing PostgreSQL data directory"

    # Remove existing data directory if reinit requested
    if (Test-Path $dataDir) {
        Remove-Item $dataDir -Recurse -Force
    }

    Assert-Step "Run init-postgres.bat" {
        $initScript = Join-Path $installerDir "scripts\init-postgres.bat"
        if (-not (Test-Path $initScript)) {
            throw "init-postgres.bat not found at $initScript"
        }

        # Run init script with test-publish pgsql directory
        Push-Location (Join-Path $installerDir "scripts")
        try {
            $env:PGSQL_DIR = $pgsqlDestDir
            cmd /c "`"$initScript`" `"$pgsqlDestDir`"" 2>&1 | Write-Host

            if ($LASTEXITCODE -ne 0) {
                throw "init-postgres.bat exited with code $LASTEXITCODE"
            }
        }
        finally {
            Remove-Item Env:\PGSQL_DIR -ErrorAction SilentlyContinue
            Pop-Location
        }
    }
}
else {
    Write-Success "PostgreSQL data directory already initialized"
}

# ── Step 5: Create Configuration ──────────────────────────────────────────

Write-Step "Creating production configuration"

Assert-Step "Create appsettings.local.json" {
    # Try to get API key from user secrets first
    $apiKey = ""
    try {
        $secretsJson = dotnet user-secrets list --project $apiProject 2>&1 | Out-String
        if ($secretsJson -match 'ANTHROPIC_API_KEY\s*=\s*(.+)') {
            $apiKey = $matches[1].Trim()
        }
    }
    catch {
        # Ignore errors, will check environment next
    }

    # Fall back to environment variable
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        $apiKey = $env:ANTHROPIC_API_KEY
    }

    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        Write-Host "Warning: No ANTHROPIC_API_KEY found. App will start in setup mode." -ForegroundColor Yellow
        $apiKey = ""
    }

    $config = @{
        ANTHROPIC_API_KEY = $apiKey
        ConnectionStrings = @{
            Default = "Host=localhost;Port=5433;Database=archive_search;Username=archive;Password=archive"
        }
    }

    $configJson = $config | ConvertTo-Json -Depth 10

    # Write to %LOCALAPPDATA%\AIchivist\config\ where the app reads it from
    $appDataConfigDir = Join-Path $env:LOCALAPPDATA "AIchivist\config"
    New-Item -ItemType Directory -Force -Path $appDataConfigDir | Out-Null
    $configPath = Join-Path $appDataConfigDir "appsettings.local.json"
    Set-Content -Path $configPath -Value $configJson -Encoding UTF8
    Write-Host "  Config written to: $configPath" -ForegroundColor Gray
}

# ── Step 6: Launch AIchivist ──────────────────────────────────────────────

Write-Step "Launching AIchivist.exe"

Write-Host ""
Write-Host "==================================================================================" -ForegroundColor Green
Write-Host "  Production test environment is ready!" -ForegroundColor Green
Write-Host "==================================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Application: " -NoNewline
Write-Host "http://localhost:5265" -ForegroundColor Cyan
Write-Host "  PostgreSQL:  " -NoNewline
Write-Host "localhost:5433 (managed by AIchivist.exe)" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Press Ctrl+C to stop and verify clean PostgreSQL shutdown" -ForegroundColor Yellow
Write-Host ""
Write-Host "==================================================================================" -ForegroundColor Green
Write-Host ""

# Set environment to Production (save original to restore later)
$originalAspNetEnv = $env:ASPNETCORE_ENVIRONMENT
$env:ASPNETCORE_ENVIRONMENT = "Production"

# Launch AIchivist.exe
$aichivistExe = Join-Path $testPublishDir "AIchivist.exe"

try {
    & $aichivistExe
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $originalAspNetEnv

    Write-Host ""
    Write-Host "==================================================================================" -ForegroundColor Cyan
    Write-Host "  Testing complete. Check console output above for PostgreSQL shutdown messages." -ForegroundColor Cyan
    Write-Host "  Expected: '[PostgreSQL] Stopping...' and '[PostgreSQL] Stopped successfully.'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Verify NO postgres.exe processes remain in Task Manager." -ForegroundColor Yellow
    Write-Host "==================================================================================" -ForegroundColor Cyan
    Write-Host ""
}
