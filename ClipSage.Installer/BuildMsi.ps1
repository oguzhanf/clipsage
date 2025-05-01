param(
    [string]$WixToolsetPath,
    [string]$Version,
    [string]$BindPath,
    [string]$OutputPath,
    [string]$ProductWxsPath
)

Write-Host "Building MSI with WiX Toolset v6.0..."
Write-Host "WixToolsetPath: $WixToolsetPath"
Write-Host "Version: $Version"
Write-Host "BindPath: $BindPath"
Write-Host "OutputPath: $OutputPath"
Write-Host "ProductWxsPath: $ProductWxsPath"

# Ensure the output directory exists
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force
}

# Build the command
$wixExe = Join-Path $WixToolsetPath "wix.exe"
$outputMsi = Join-Path $OutputPath "ClipSage-Setup-$Version.msi"

# Check if wix.exe exists
if (-not (Test-Path $wixExe)) {
    Write-Error "WiX executable not found at: $wixExe"
    exit 1
}

# Run the WiX command
$command = "$wixExe build -d Version=$Version -b `"$BindPath`" `"$ProductWxsPath`" -o `"$outputMsi`""
Write-Host "Running command: $command"

# Execute the command
Invoke-Expression $command

# Check the exit code
if ($LASTEXITCODE -ne 0) {
    Write-Error "WiX build failed with exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "MSI build completed successfully: $outputMsi"
