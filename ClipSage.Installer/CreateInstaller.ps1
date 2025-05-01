# CreateInstaller.ps1
# This script creates a self-extracting installer for ClipperMVP

# Set variables
$appName = "ClipperMVP"
$version = "1.0.11"
$sourceDir = "..\Clipper.App\bin\Debug\net9.0-windows"
$outputDir = "bin\Debug"
$installerName = "$appName-Setup-$version.exe"

# Create output directory if it doesn't exist
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Clean up any existing installer
if (Test-Path "$outputDir\$installerName") {
    Remove-Item "$outputDir\$installerName" -Force
}

# Create a temporary directory for the installer files
$tempDir = ".\temp_installer"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Copy application files to the temporary directory
Copy-Item "$sourceDir\*" $tempDir -Recurse

# Create the install.bat script
$installBatContent = @"
@echo off
echo Installing $appName $version...
echo.

REM Create the installation directory
mkdir "C:\Program Files\ClipSage\$appName" 2>nul

REM Copy the application files
xcopy /Y /E "%~dp0*.*" "C:\Program Files\ClipSage\$appName\" /EXCLUDE:%~dp0install.bat

REM Create a shortcut on the desktop
powershell "\$WshShell = New-Object -ComObject WScript.Shell; \$Shortcut = \$WshShell.CreateShortcut('%USERPROFILE%\Desktop\$appName.lnk'); \$Shortcut.TargetPath = 'C:\Program Files\ClipSage\$appName\Clipper.App.exe'; \$Shortcut.Save()"

REM Create a shortcut in the Start Menu
powershell "\$WshShell = New-Object -ComObject WScript.Shell; \$Shortcut = \$WshShell.CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\$appName.lnk'); \$Shortcut.TargetPath = 'C:\Program Files\ClipSage\$appName\Clipper.App.exe'; \$Shortcut.Save()"

REM Add registry entries for auto-update
reg add "HKLM\SOFTWARE\ClipSage\$appName" /v "Version" /t REG_SZ /d "$version" /f
reg add "HKLM\SOFTWARE\ClipSage\$appName" /v "InstallLocation" /t REG_SZ /d "C:\Program Files\ClipSage\$appName" /f
reg add "HKLM\SOFTWARE\ClipSage\$appName" /v "AutoUpdate" /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\ClipSage\$appName" /v "UpdateUrl" /t REG_SZ /d "https://clipsage.app/api/check-update" /f

echo.
echo Installation completed successfully!
echo.
echo Press any key to exit...
pause >nul
"@

# Save the install.bat script to the temporary directory
$installBatContent | Out-File "$tempDir\install.bat" -Encoding ASCII

# Create a self-extracting 7-Zip archive
# Note: This requires 7-Zip to be installed
$sevenZipPath = "C:\Program Files\7-Zip\7z.exe"
if (Test-Path $sevenZipPath) {
    # Create a config file for the self-extracting archive
    $sfxConfigContent = @"
;!@Install@!UTF-8!
Title="$appName Installer"
BeginPrompt="Do you want to install $appName $version?"
RunProgram="install.bat"
;!@InstallEnd@!
"@
    $sfxConfigContent | Out-File ".\sfx_config.txt" -Encoding ASCII

    # Create a 7z archive
    & $sevenZipPath a -r "$outputDir\temp.7z" "$tempDir\*"

    # Create the self-extracting archive
    $sfxModule = "C:\Program Files\7-Zip\7z.sfx"
    if (Test-Path $sfxModule) {
        Get-Content $sfxModule, ".\sfx_config.txt", "$outputDir\temp.7z" -Encoding Byte -ReadCount 0 | Set-Content "$outputDir\$installerName" -Encoding Byte
        Write-Host "Self-extracting installer created: $outputDir\$installerName"
    } else {
        Write-Host "Error: 7-Zip SFX module not found."
    }

    # Clean up temporary files
    Remove-Item ".\sfx_config.txt" -Force
    Remove-Item "$outputDir\temp.7z" -Force
} else {
    Write-Host "Error: 7-Zip not found. Please install 7-Zip to create a self-extracting installer."
    
    # As a fallback, just copy the install.bat to the output directory
    Copy-Item "$tempDir\install.bat" "$outputDir\install.bat"
    Write-Host "Fallback installer created: $outputDir\install.bat"
}

# Clean up the temporary directory
Remove-Item $tempDir -Recurse -Force

Write-Host "Installer creation completed."
