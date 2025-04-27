# ZIP installer builder script for ClipperMVP
param (
    [string]$Version = "",
    [string]$Configuration = "Release",
    [switch]$NoPublish = $false
)

# Get version from Directory.Build.props if not specified
if ([string]::IsNullOrEmpty($Version)) {
    $buildPropsPath = Join-Path $PSScriptRoot "..\..\Directory.Build.props"
    if (Test-Path $buildPropsPath) {
        $content = Get-Content $buildPropsPath
        $versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
        if ($versionLine) {
            $Version = ($versionLine -replace '<Version>(.*)</Version>', '$1').Trim()
            Write-Host "Using version from Directory.Build.props: $Version"
        }
        else {
            Write-Error "Could not find version in Directory.Build.props"
            exit 1
        }
    }
    else {
        Write-Error "Could not find Directory.Build.props"
        exit 1
    }
}

# Set paths
$rootDir = Join-Path $PSScriptRoot "..\..\"
$srcDir = Join-Path $rootDir "src"
$binDir = Join-Path $rootDir "bin"
$publishDir = Join-Path $binDir "ClipperMVP-$Version"
$outputDir = Join-Path $binDir "installers"
$tempDir = Join-Path $outputDir "temp"

# Create directories if they don't exist
if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
}
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Ensure the version doesn't have extra spaces
$Version = $Version.Trim()

# Update paths with clean version
$publishDir = Join-Path $binDir "ClipperMVP-$Version"
$outputDir = Join-Path $binDir "installers"

# Publish the application if needed
if (-not $NoPublish) {
    Write-Host "Publishing application..."
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }

    # Run dotnet publish
    $appProjectPath = Join-Path $srcDir "Clipper.App\Clipper.App.csproj"
    Write-Host "Publishing from: $appProjectPath"
    Write-Host "Publishing to: $publishDir"

    try {
        & dotnet publish $appProjectPath -c $Configuration -r win-x64 --self-contained false -o $publishDir

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish application (Exit code: $LASTEXITCODE)"
            exit 1
        }
    }
    catch {
        Write-Error "Failed to publish application: $_"
        exit 1
    }
}

# Create a simple installer batch file
$installerPath = Join-Path $tempDir "install.bat"
$installerContent = @"
@echo off
echo Installing ClipperMVP v$Version...
echo.

set INSTALL_DIR=%ProgramFiles%\ClipSage\ClipperMVP
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

echo Copying files to %INSTALL_DIR%...
xcopy /E /Y "%~dp0app\*" "%INSTALL_DIR%\"

echo Creating shortcuts...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"^$ws = New-Object -ComObject WScript.Shell; ^
^$shortcut = ^$ws.CreateShortcut('%USERPROFILE%\Desktop\ClipperMVP.lnk'); ^
^$shortcut.TargetPath = '%INSTALL_DIR%\Clipper.App.exe'; ^
^$shortcut.Save(); ^
^$startMenu = ^$ws.SpecialFolders.Item('Programs'); ^
^$shortcutMenu = ^$ws.CreateShortcut(^$startMenu + '\ClipperMVP.lnk'); ^
^$shortcutMenu.TargetPath = '%INSTALL_DIR%\Clipper.App.exe'; ^
^$shortcutMenu.Save()"

echo Adding registry entries for auto-update...
reg add "HKLM\SOFTWARE\ClipSage\ClipperMVP" /v Version /t REG_SZ /d "$Version" /f
reg add "HKLM\SOFTWARE\ClipSage\ClipperMVP" /v InstallLocation /t REG_SZ /d "%INSTALL_DIR%" /f
reg add "HKLM\SOFTWARE\ClipSage\ClipperMVP" /v AutoUpdate /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\ClipSage\ClipperMVP" /v UpdateUrl /t REG_SZ /d "https://clipsage.app/api/check-update" /f

echo Installation complete!
echo.
echo Starting ClipperMVP...
start "" "%INSTALL_DIR%\Clipper.App.exe"
"@

# Create a readme file
$readmePath = Join-Path $tempDir "README.txt"
$readmeContent = @"
ClipperMVP v$Version
====================

Installation Instructions:
1. Extract all files from this ZIP archive
2. Run install.bat to install ClipperMVP

Manual Installation:
1. Extract all files from this ZIP archive
2. Copy the contents of the 'app' folder to a location of your choice
3. Create shortcuts to Clipper.App.exe as needed

For more information, visit https://clipsage.app
"@

# Create the temporary directory structure
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempDir "app") -Force | Out-Null

# Copy the published files to the app directory
Copy-Item -Path "$publishDir\*" -Destination (Join-Path $tempDir "app") -Recurse -Force

# Write the installer batch file and readme
Set-Content -Path $installerPath -Value $installerContent -Encoding ASCII
Set-Content -Path $readmePath -Value $readmeContent -Encoding ASCII

# Create the ZIP file
$zipPath = Join-Path $outputDir "ClipperMVP-Setup-$Version.zip"
Write-Host "Creating ZIP file: $zipPath"

# Create the ZIP file
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -Force

# Check if the ZIP file was created successfully
if (Test-Path $zipPath) {
    Write-Host "ZIP file created successfully: $zipPath"

    # Copy to bin directory for convenience
    Copy-Item -Path $zipPath -Destination $binDir -Force
    Write-Host "ZIP file copied to: $(Join-Path $binDir (Split-Path $zipPath -Leaf))"

    # Clean up the temporary directory
    Remove-Item -Path $tempDir -Recurse -Force

    return $true
}
else {
    Write-Error "Failed to create ZIP file"
    return $false
}
