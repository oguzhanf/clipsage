# Build script for ClipSage
param (
    [string]$Configuration = "Release",
    [switch]$MSIOnly = $false,
    [switch]$ZIPOnly = $false,
    [switch]$NoVersionIncrement = $false
)

# Set error action preference to stop on error
$ErrorActionPreference = "Stop"

Write-Host "Building ClipSage..." -ForegroundColor Cyan

try {
    # Call the main build script
    $buildParams = @{
        Configuration = $Configuration
    }

    if ($NoVersionIncrement) {
        $buildParams.Add("NoVersionIncrement", $true)
    }

    if ($MSIOnly) {
        $buildParams.Add("MSIOnly", $true)
    }

    if ($ZIPOnly) {
        $buildParams.Add("ZIPOnly", $true)
    }

    # Convert parameters to arguments
    $buildArgs = $buildParams.GetEnumerator() | ForEach-Object { "-$($_.Key)", $(if ($_.Value -eq $true) { $null } else { $_.Value }) } | Where-Object { $_ -ne $null }

    # Call the build script
    & "$PSScriptRoot\build\build.ps1" @buildArgs

    # Check if the build was successful
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # Get the current version from Directory.Build.props
    $buildPropsPath = Join-Path $PSScriptRoot "Directory.Build.props"
    $content = Get-Content $buildPropsPath
    $versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
    $version = ($versionLine -replace '<Version>(.*)</Version>', '$1').Trim()

    # Check if the installer was created
    $zipPath = Join-Path $PSScriptRoot "bin\ClipSage-Setup-$version.zip"
    $msiPath = Join-Path $PSScriptRoot "bin\ClipSage-Setup-$version.msi"

    if (Test-Path $zipPath) {
        Write-Host "ZIP installer created successfully: $zipPath" -ForegroundColor Green
    }

    if (Test-Path $msiPath) {
        Write-Host "MSI installer created successfully: $msiPath" -ForegroundColor Green
    }

    Write-Host "Build completed successfully" -ForegroundColor Green
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor Red
    exit 1
}
