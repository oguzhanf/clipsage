# Script to create a GitHub release for ClipSage
param (
    [Parameter(Mandatory=$false)]
    [string]$Version = "",

    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = ""
)

# Set error action preference to stop on error
$ErrorActionPreference = "Stop"

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

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
    
    # If still no version, exit
    if ([string]::IsNullOrEmpty($Version)) {
        Write-Host "Could not determine version. Please provide it as a parameter or ensure Directory.Build.props exists." -ForegroundColor Red
        exit 1
    }
}

# If release notes are not provided, use default
if ([string]::IsNullOrEmpty($ReleaseNotes)) {
    $ReleaseNotes = "## ClipSage v$Version Release Notes

### What's New
- Fixed version checking to ensure accurate update detection
- Improved application stability
- Various bug fixes and improvements

### Installation
1. Download the executable
2. Run the executable
3. Enjoy ClipSage!"
}

Write-Host "Creating GitHub release for ClipSage v$Version..." -ForegroundColor Cyan

# Check if GitHub CLI is installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI (gh) is not installed. Please install it first." -ForegroundColor Red
    Write-Host "You can install it with: winget install GitHub.cli" -ForegroundColor Yellow
    exit 1
}

# Check if user is authenticated with GitHub
$authStatus = gh auth status 2>&1
if ($authStatus -match "not logged in") {
    Write-Host "You are not logged in to GitHub. Please authenticate first." -ForegroundColor Red
    Write-Host "Run: gh auth login" -ForegroundColor Yellow
    exit 1
}

# Check if the MSI or EXE file exists
$msiPath = Join-Path $rootDir "bin\ClipSage-Setup-$Version.msi"
$exePath = Join-Path $rootDir "bin\ClipSage-$Version.exe"
$installerPath = ""

if (Test-Path $msiPath) {
    $installerPath = $msiPath
    Write-Host "Found MSI installer: $msiPath" -ForegroundColor Green
} elseif (Test-Path $exePath) {
    $installerPath = $exePath
    Write-Host "Found EXE installer: $exePath" -ForegroundColor Green
} else {
    Write-Host "No installer file found." -ForegroundColor Red
    Write-Host "Looked for:" -ForegroundColor Yellow
    Write-Host "  - $msiPath" -ForegroundColor Yellow
    Write-Host "  - $exePath" -ForegroundColor Yellow
    Write-Host "Please build the installer first." -ForegroundColor Yellow
    exit 1
}

# Create a tag for the release
Write-Host "Creating tag v$Version..." -ForegroundColor Yellow
git tag -a "v$Version" -m "Release v$Version"

# Push the tag to GitHub
Write-Host "Pushing tag to GitHub..." -ForegroundColor Yellow
git push origin "v$Version"

# Create the release
Write-Host "Creating GitHub release..." -ForegroundColor Yellow
gh release create "v$Version" $installerPath --title "ClipSage v$Version" --notes "$ReleaseNotes"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to create GitHub release." -ForegroundColor Red
    exit 1
}

Write-Host "GitHub release created successfully!" -ForegroundColor Green
Write-Host "Release URL: https://github.com/oguzhanf/clipsage/releases/tag/v$Version" -ForegroundColor Cyan
