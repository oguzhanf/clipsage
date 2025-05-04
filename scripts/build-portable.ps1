# build-portable.ps1
# This script builds a portable version of ClipSage

param(
    [string]$Configuration = "Release",
    [string]$Version = ""
)

# Set error action preference to stop on error
$ErrorActionPreference = "Stop"

# Get the current directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$outputDir = Join-Path $rootDir "bin"

# If version is not provided, get it from Directory.Build.props
if ([string]::IsNullOrEmpty($Version)) {
    $buildPropsPath = Join-Path $rootDir "Directory.Build.props"
    if (Test-Path $buildPropsPath) {
        $content = Get-Content $buildPropsPath
        $versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
        if ($versionLine) {
            $Version = ($versionLine -replace '<Version>(.*)</Version>', '$1').Trim()
        }
    }
    
    # If still no version, use a default
    if ([string]::IsNullOrEmpty($Version)) {
        $Version = "1.0.0"
    }
}

Write-Host "Building ClipSage v$Version..." -ForegroundColor Cyan

# Create the output directory if it doesn't exist
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Build the solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build "$rootDir\ClipSage.sln" -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Publish the application
Write-Host "Publishing application..." -ForegroundColor Yellow
$publishDir = Join-Path $outputDir "ClipSage-$Version"

# Create the publish directory if it doesn't exist
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir | Out-Null

# Publish the application
dotnet publish "$rootDir\ClipSage.App\ClipSage.App.csproj" -c $Configuration -r win-x64 --self-contained false -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Create a ZIP file
Write-Host "Creating ZIP file..." -ForegroundColor Yellow
$zipPath = Join-Path $outputDir "ClipSage-$Version.zip"

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath)

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Output: $zipPath" -ForegroundColor Cyan
