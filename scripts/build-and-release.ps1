# Build and Release script for ClipSage
# This script builds a portable executable and creates a GitHub release

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = ""
)

# Set error action preference to stop on error
$ErrorActionPreference = "Stop"

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "ClipSage Build and Release Script" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Get version from Directory.Build.props
Write-Host "Step 1: Reading version from Directory.Build.props..." -ForegroundColor Yellow
$buildPropsPath = Join-Path $rootDir "Directory.Build.props"
if (-not (Test-Path $buildPropsPath)) {
    Write-Host "ERROR: Directory.Build.props not found!" -ForegroundColor Red
    exit 1
}

$content = Get-Content $buildPropsPath
$versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
if (-not $versionLine) {
    Write-Host "ERROR: Could not find version in Directory.Build.props!" -ForegroundColor Red
    exit 1
}

$Version = ($versionLine -replace '<Version>(.*)</Version>', '$1').Trim()
Write-Host "  Version: $Version" -ForegroundColor Green
Write-Host ""

# Step 2: Build the portable executable
Write-Host "Step 2: Building portable executable..." -ForegroundColor Yellow
$buildScript = Join-Path $scriptDir "build-portable.ps1"
if (-not (Test-Path $buildScript)) {
    Write-Host "ERROR: build-portable.ps1 not found!" -ForegroundColor Red
    exit 1
}

& $buildScript -Configuration $Configuration -Version $Version

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host ""

# Step 3: Rename the ZIP to EXE if needed (for GitHub releases)
Write-Host "Step 3: Preparing release artifacts..." -ForegroundColor Yellow
$zipPath = Join-Path $rootDir "bin\ClipSage-$Version.zip"
$exePath = Join-Path $rootDir "bin\ClipSage-$Version.exe"
$publishDir = Join-Path $rootDir "bin\ClipSage-$Version"

# Check if we have the published directory with the main executable
$mainExePath = Join-Path $publishDir "ClipSage.App.exe"
if (Test-Path $mainExePath) {
    Write-Host "  Copying executable for release..." -ForegroundColor Cyan
    Copy-Item -Path $mainExePath -Destination $exePath -Force
    Write-Host "  Created: $exePath" -ForegroundColor Green
}

if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: Could not create release executable!" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 4: Create GitHub release
Write-Host "Step 4: Creating GitHub release..." -ForegroundColor Yellow
$releaseScript = Join-Path $scriptDir "create-release.ps1"
if (-not (Test-Path $releaseScript)) {
    Write-Host "ERROR: create-release.ps1 not found!" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrEmpty($ReleaseNotes)) {
    & $releaseScript -Version $Version
} else {
    & $releaseScript -Version $Version -ReleaseNotes $ReleaseNotes
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Release creation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host ""

Write-Host "====================================" -ForegroundColor Green
Write-Host "Build and Release Complete!" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Version: v$Version" -ForegroundColor Cyan
Write-Host "Release URL: https://github.com/oguzhanf/clipsage/releases/tag/v$Version" -ForegroundColor Cyan
