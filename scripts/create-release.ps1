# Script to create a GitHub release for ClipSage
param (
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.13",

    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "## ClipSage v$Version Release Notes

### What's New
- New MSI installer with improved user interface
- Option to launch ClipSage after setup
- Option to start ClipSage with Windows login
- Various bug fixes and performance improvements

### Installation
1. Download the MSI installer
2. Run the installer and follow the prompts
3. Enjoy ClipSage!"
)

# Set error action preference to stop on error
$ErrorActionPreference = "Stop"

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

# Check if the MSI file exists
$msiPath = "bin\ClipSage-Setup-$Version.msi"
if (-not (Test-Path $msiPath)) {
    Write-Host "MSI file not found at: $msiPath" -ForegroundColor Red
    Write-Host "Please build the MSI package first." -ForegroundColor Yellow
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
gh release create "v$Version" $msiPath --title "ClipSage v$Version" --notes "$ReleaseNotes"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to create GitHub release." -ForegroundColor Red
    exit 1
}

Write-Host "GitHub release created successfully!" -ForegroundColor Green
Write-Host "Release URL: https://github.com/oguzhanf/clipsage/releases/tag/v$Version" -ForegroundColor Cyan
