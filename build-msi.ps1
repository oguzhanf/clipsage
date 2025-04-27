# MSI installer builder script for ClipperMVP
param (
    [string]$Configuration = "Release",
    [switch]$NoPublish = $false
)

# Set error action preference to stop on error
$ErrorActionPreference = "Stop"

Write-Host "Building ClipperMVP MSI installer..." -ForegroundColor Cyan

try {
    # Get the current version from Directory.Build.props
    $buildPropsPath = Join-Path $PSScriptRoot "Directory.Build.props"
    $content = Get-Content $buildPropsPath
    $versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
    $version = ($versionLine -replace '<Version>(.*)</Version>', '$1').Trim()

    Write-Host "Using version: $version" -ForegroundColor Yellow

    # Set paths
    $srcDir = Join-Path $PSScriptRoot "src"
    $binDir = Join-Path $PSScriptRoot "bin"
    $publishDir = Join-Path $binDir "ClipperMVP-$version"
    $installerDir = Join-Path $srcDir "Clipper.Installer"
    $outputPath = Join-Path $binDir "ClipperMVP-Setup-$version.msi"
    $tempDir = Join-Path $binDir "temp-wix"

    # Create directories if they don't exist
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }

    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    } else {
        Remove-Item -Path "$tempDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }

    # Publish the application if needed
    if (-not $NoPublish) {
        Write-Host "Publishing application..." -ForegroundColor Yellow

        # Clean the solution
        Write-Host "Cleaning solution..." -ForegroundColor Yellow
        dotnet clean -c $Configuration

        # Build the solution
        Write-Host "Building solution..." -ForegroundColor Yellow
        dotnet build -c $Configuration

        # Publish the application
        $appProjectPath = Join-Path $srcDir "Clipper.App\Clipper.App.csproj"
        Write-Host "Publishing from: $appProjectPath" -ForegroundColor Yellow
        Write-Host "Publishing to: $publishDir" -ForegroundColor Yellow

        if (Test-Path $publishDir) {
            Remove-Item -Path $publishDir -Recurse -Force
        }

        dotnet publish $appProjectPath -c $Configuration -r win-x64 --self-contained false -o $publishDir

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish application (Exit code: $LASTEXITCODE)"
            exit 1
        }
    }

    # Check if WiX Toolset v6.0 is installed
    $wixPath = "C:\Program Files\WiX Toolset v6.0\bin"
    if (-not (Test-Path $wixPath)) {
        Write-Error "WiX Toolset v6.0 not found at $wixPath"
        exit 1
    }

    # Create a simplified WiX source file that works with WiX v6.0
    $wxsPath = Join-Path $tempDir "ClipperMVP.wxs"
    $wxsContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="ClipperMVP"
           Version="$version"
           Manufacturer="ClipSage"
           UpgradeCode="6fe30b47-2626-43ff-9384-d324f425a2a2"
           InstallerVersion="200">

    <SummaryInformation Description="ClipperMVP Installer" />

    <Media Id="1" Cabinet="product.cab" EmbedCab="yes" CompressionLevel="high" />

    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed." />

    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="COMPANYFOLDER" Name="ClipSage">
        <Directory Id="INSTALLFOLDER" Name="ClipperMVP">
          <Component Id="MainExecutable" Guid="*">
            <File Id="ClipperExe" Source="Clipper.App.exe" KeyPath="yes">
              <Shortcut Id="startmenuShortcut"
                        Directory="ProgramMenuFolder"
                        Name="ClipperMVP"
                        WorkingDirectory="INSTALLFOLDER"
                        Advertise="yes" />
              <Shortcut Id="desktopShortcut"
                        Directory="DesktopFolder"
                        Name="ClipperMVP"
                        WorkingDirectory="INSTALLFOLDER"
                        Advertise="yes" />
            </File>
          </Component>
          <Component Id="CoreLibrary" Guid="*">
            <File Id="ClipperCoreDll" Source="Clipper.Core.dll" KeyPath="yes" />
          </Component>
          <Component Id="ControlzExDll" Guid="*">
            <File Id="ControlzExDll" Source="ControlzEx.dll" KeyPath="yes" />
          </Component>
          <Component Id="HardcodetNotifyIconDll" Guid="*">
            <File Id="HardcodetNotifyIconDll" Source="Hardcodet.NotifyIcon.Wpf.dll" KeyPath="yes" />
          </Component>
          <Component Id="LiteDBDll" Guid="*">
            <File Id="LiteDBDll" Source="LiteDB.dll" KeyPath="yes" />
          </Component>
          <Component Id="MahAppsMetroDll" Guid="*">
            <File Id="MahAppsMetroDll" Source="MahApps.Metro.dll" KeyPath="yes" />
          </Component>
          <Component Id="MicrosoftXamlBehaviorsDll" Guid="*">
            <File Id="MicrosoftXamlBehaviorsDll" Source="Microsoft.Xaml.Behaviors.dll" KeyPath="yes" />
          </Component>
          <Component Id="NHotkeyDll" Guid="*">
            <File Id="NHotkeyDll" Source="NHotkey.dll" KeyPath="yes" />
          </Component>
          <Component Id="NHotkeyWpfDll" Guid="*">
            <File Id="NHotkeyWpfDll" Source="NHotkey.Wpf.dll" KeyPath="yes" />
          </Component>
          <Component Id="NLogDll" Guid="*">
            <File Id="NLogDll" Source="NLog.dll" KeyPath="yes" />
          </Component>
        </Directory>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder" />
    <StandardDirectory Id="DesktopFolder" />

    <Feature Id="ProductFeature" Title="ClipperMVP" Level="1">
      <ComponentRef Id="MainExecutable" />
      <ComponentRef Id="CoreLibrary" />
      <ComponentRef Id="ControlzExDll" />
      <ComponentRef Id="HardcodetNotifyIconDll" />
      <ComponentRef Id="LiteDBDll" />
      <ComponentRef Id="MahAppsMetroDll" />
      <ComponentRef Id="MicrosoftXamlBehaviorsDll" />
      <ComponentRef Id="NHotkeyDll" />
      <ComponentRef Id="NHotkeyWpfDll" />
      <ComponentRef Id="NLogDll" />
    </Feature>

    <UI>
      <UI Id="WixUI_InstallDir" />
    </UI>
  </Package>
</Wix>
"@

    # Write the WiX source file
    Set-Content -Path $wxsPath -Value $wxsContent -Encoding UTF8

    # Build the MSI using WiX Toolset v6.0
    Write-Host "Building MSI installer using WiX Toolset v6.0..." -ForegroundColor Yellow

    # Run the WiX tool to build the MSI
    & "$wixPath\wix.exe" build -ext WixToolset.UI.wixext -bindpath "$publishDir" -o "$outputPath" "$wxsPath"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build MSI installer (Exit code: $LASTEXITCODE)"

        # Try a simpler approach without UI extension
        Write-Host "Trying alternative build approach..." -ForegroundColor Yellow
        & "$wixPath\wix.exe" build -bindpath "$publishDir" -o "$outputPath" "$wxsPath"

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to build MSI installer with alternative approach (Exit code: $LASTEXITCODE)"
            exit 1
        }
    }

    Write-Host "MSI installer created successfully: $outputPath" -ForegroundColor Green

    # Verify the MSI file
    if (Test-Path $outputPath) {
        $fileInfo = Get-Item $outputPath
        Write-Host "MSI file size: $($fileInfo.Length) bytes" -ForegroundColor Green
        Write-Host "MSI file created at: $($fileInfo.LastWriteTime)" -ForegroundColor Green
    } else {
        Write-Error "MSI file was not created at the expected location: $outputPath"
    }
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor Red
    exit 1
}
finally {
    # Clean up temporary files
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
