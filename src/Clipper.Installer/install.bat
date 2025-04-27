@echo off
echo Installing ClipperMVP...
echo.

REM Create the installation directory
mkdir "C:\Program Files\ClipSage\ClipperMVP" 2>nul

REM Copy the application files
xcopy /Y "..\Clipper.App\bin\Debug\net9.0-windows\*.*" "C:\Program Files\ClipSage\ClipperMVP\"

REM Create a shortcut on the desktop
powershell "$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%USERPROFILE%\Desktop\ClipperMVP.lnk'); $Shortcut.TargetPath = 'C:\Program Files\ClipSage\ClipperMVP\Clipper.App.exe'; $Shortcut.Save()"

REM Create a shortcut in the Start Menu
powershell "$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\ClipperMVP.lnk'); $Shortcut.TargetPath = 'C:\Program Files\ClipSage\ClipperMVP\Clipper.App.exe'; $Shortcut.Save()"

echo.
echo Installation completed successfully!
echo.
echo Press any key to exit...
pause >nul
