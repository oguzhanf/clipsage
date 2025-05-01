# Script to upload an MSI installer to GitHub as a release
param (
    [Parameter(Mandatory=$true)]
    [string]$Token,
    
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.14",
    
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

# Check if the MSI file exists
$msiPath = "bin\ClipSage-Setup-$Version.msi"
if (-not (Test-Path $msiPath)) {
    Write-Host "MSI file not found at: $msiPath" -ForegroundColor Red
    Write-Host "Please build the MSI package first." -ForegroundColor Yellow
    exit 1
}

# Set up headers for API requests
$headers = @{
    "Authorization" = "token $Token"
    "Accept" = "application/vnd.github.v3+json"
}

try {
    # Check if the release already exists
    $checkUrl = "https://api.github.com/repos/oguzhanf/clipsage/releases/tags/v$Version"
    try {
        $existingRelease = Invoke-RestMethod -Uri $checkUrl -Headers $headers -Method Get
        $releaseId = $existingRelease.id
        
        # Update the existing release
        $updateUrl = "https://api.github.com/repos/oguzhanf/clipsage/releases/$releaseId"
        $body = @{
            tag_name = "v$Version"
            name = "ClipSage v$Version"
            body = $ReleaseNotes
            draft = $false
            prerelease = $false
        } | ConvertTo-Json
        
        Write-Host "Updating existing release..." -ForegroundColor Yellow
        $response = Invoke-RestMethod -Uri $updateUrl -Headers $headers -Method Patch -Body $body -ContentType "application/json"
        Write-Host "Release updated successfully." -ForegroundColor Green
    } catch {
        # Create a new release
        $createUrl = "https://api.github.com/repos/oguzhanf/clipsage/releases"
        $body = @{
            tag_name = "v$Version"
            name = "ClipSage v$Version"
            body = $ReleaseNotes
            draft = $false
            prerelease = $false
        } | ConvertTo-Json
        
        Write-Host "Creating new release..." -ForegroundColor Yellow
        $response = Invoke-RestMethod -Uri $createUrl -Headers $headers -Method Post -Body $body -ContentType "application/json"
        $releaseId = $response.id
        Write-Host "Release created successfully." -ForegroundColor Green
    }
    
    # Check if the asset already exists and delete it if it does
    $assetsUrl = "https://api.github.com/repos/oguzhanf/clipsage/releases/$releaseId/assets"
    $assets = Invoke-RestMethod -Uri $assetsUrl -Headers $headers -Method Get
    $msiFileName = "ClipSage-Setup-$Version.msi"
    $existingAsset = $assets | Where-Object { $_.name -eq $msiFileName }
    
    if ($existingAsset) {
        Write-Host "Deleting existing asset..." -ForegroundColor Yellow
        $deleteUrl = "https://api.github.com/repos/oguzhanf/clipsage/releases/assets/$($existingAsset.id)"
        Invoke-RestMethod -Uri $deleteUrl -Headers $headers -Method Delete
        Write-Host "Existing asset deleted." -ForegroundColor Green
    }
    
    # Upload the MSI file
    $uploadUrl = "https://uploads.github.com/repos/oguzhanf/clipsage/releases/$releaseId/assets?name=$msiFileName"
    $uploadHeaders = @{
        "Authorization" = "token $Token"
        "Content-Type" = "application/octet-stream"
    }
    
    Write-Host "Uploading MSI file..." -ForegroundColor Yellow
    $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Headers $uploadHeaders -Method Post -InFile $msiPath
    Write-Host "MSI file uploaded successfully." -ForegroundColor Green
    
    Write-Host "GitHub release process completed successfully!" -ForegroundColor Cyan
    Write-Host "Release URL: https://github.com/oguzhanf/clipsage/releases/tag/v$Version" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    
    # Try to get more details about the error
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body: $responseBody" -ForegroundColor Red
    }
    
    exit 1
}
