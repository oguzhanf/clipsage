# MSI installer builder script for ClipSage
param (
    [string]$Configuration = "Release",
    [switch]$NoPublish = $false
)

# Set error action preference to stop on error
$ErrorActionPreference = "Stop"

Write-Host "Building ClipSage MSI installer..." -ForegroundColor Cyan

try {
    # Get the current version from Directory.Build.props
    $buildPropsPath = Join-Path $PSScriptRoot "Directory.Build.props"
    $content = Get-Content $buildPropsPath
    $versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
    $version = ($versionLine -replace '<Version>(.*)</Version>', '$1').Trim()

    Write-Host "Using version: $version" -ForegroundColor Yellow

    # Set paths
    $binDir = Join-Path $PSScriptRoot "bin"
    $publishDir = Join-Path $binDir "ClipSage-$version"
    $outputPath = Join-Path $binDir "ClipSage-Setup-$version.msi"
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
        $appProjectPath = Join-Path $PSScriptRoot "ClipSage.App\ClipSage.App.csproj"
        Write-Host "Publishing from: $appProjectPath" -ForegroundColor Yellow
        Write-Host "Publishing to: $publishDir" -ForegroundColor Yellow

        if (Test-Path $publishDir) {
            try {
                Remove-Item -Path $publishDir -Recurse -Force -ErrorAction Stop
            }
            catch {
                Write-Warning "Could not remove existing publish directory. Some files may be in use."
                Write-Warning "Attempting to continue with existing files..."

                # Try to stop any running instances of the application
                $processName = "ClipSage.App"
                $runningProcesses = Get-Process -Name $processName -ErrorAction SilentlyContinue

                if ($runningProcesses) {
                    Write-Host "Stopping running instances of $processName..." -ForegroundColor Yellow
                    Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
                    Start-Sleep -Seconds 2  # Give some time for processes to terminate
                }

                # Try again after stopping processes
                try {
                    Remove-Item -Path $publishDir -Recurse -Force -ErrorAction Stop
                }
                catch {
                    Write-Warning "Still unable to remove directory. Will try to publish anyway."
                }
            }
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
    $wxsPath = Join-Path $tempDir "ClipSage.wxs"
    $wxsContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
  <Package Name="ClipSage"
           Version="$version"
           Manufacturer="ClipSage"
           UpgradeCode="6fe30b47-2626-43ff-9384-d324f425a2a2"
           InstallerVersion="200">

    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <!-- Properties for UI customization -->
    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT" Value="Launch ClipSage when setup exits" />
    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOX" Value="1" />
    <Property Id="STARTUPCHECKBOX" Value="1" />
    <Property Id="WIXUI_EXITDIALOGOPTIONALTEXT" Value="Start ClipSage automatically when you log in to Windows" />
    <Property Id="WixShellExecTarget" Value="[#ClipSageExe]" />

    <!-- Custom action to launch the application after installation -->
    <CustomAction Id="LaunchApplication"
                  FileRef="ClipSageExe"
                  ExeCommand=""
                  Return="asyncNoWait" />

    <!-- Execute the custom action after installation if the checkbox is checked -->
    <InstallExecuteSequence>
      <Custom Action="LaunchApplication" After="InstallFinalize" Condition="WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1 AND NOT Installed" />
    </InstallExecuteSequence>

    <!-- Define the directory structure -->
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="ClipSage">
        <!-- Main application component -->
        <Component Id="MainExecutable" Guid="*">
          <File Id="ClipSageExe" Source="ClipSage.App.exe" KeyPath="yes" />
        </Component>

        <!-- Registry entry for auto-start -->
        <Component Id="AutoStartRegistry" Guid="*" Condition="STARTUPCHECKBOX = 1">
          <RegistryValue Root="HKCU"
                         Key="Software\Microsoft\Windows\CurrentVersion\Run"
                         Name="ClipSage"
                         Value="&quot;[INSTALLFOLDER]ClipSage.App.exe&quot;"
                         Type="string"
                         KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>

    <!-- Define program menu and desktop shortcuts -->
    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="ClipSage">
        <Component Id="ApplicationShortcut" Guid="*">
          <Shortcut Id="ApplicationStartMenuShortcut"
                    Name="ClipSage"
                    Description="ClipSage Clipboard Manager"
                    Target="[INSTALLFOLDER]ClipSage.App.exe"
                    WorkingDirectory="INSTALLFOLDER"/>
          <RemoveFolder Id="RemoveApplicationProgramsFolder" On="uninstall"/>
          <RegistryValue Root="HKCU" Key="Software\ClipSage" Name="installed_startmenu" Type="integer" Value="1" KeyPath="yes"/>
        </Component>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="DesktopFolder">
      <Component Id="ApplicationShortcutDesktop" Guid="*">
        <Shortcut Id="ApplicationDesktopShortcut"
                  Name="ClipSage"
                  Description="ClipSage Clipboard Manager"
                  Target="[INSTALLFOLDER]ClipSage.App.exe"
                  WorkingDirectory="INSTALLFOLDER"/>
        <RegistryValue Root="HKCU" Key="Software\ClipSage" Name="installed_desktop" Type="integer" Value="1" KeyPath="yes"/>
      </Component>
    </StandardDirectory>

    <!-- Define the features to install -->
    <Feature Id="ProductFeature" Title="ClipSage" Level="1">
      <ComponentRef Id="MainExecutable" />
      <ComponentRef Id="ApplicationShortcut" />
      <ComponentRef Id="ApplicationShortcutDesktop" />
      <ComponentRef Id="AutoStartRegistry" />
      <ComponentGroupRef Id="PublishedComponents" />
    </Feature>

    <!-- Use the WixUI_InstallDir dialog set -->
    <ui:WixUI Id="WixUI_InstallDir" />
  </Package>
</Wix>
"@

    # Write the WiX source file
    Set-Content -Path $wxsPath -Value $wxsContent -Encoding UTF8

    # Create a component group file for all published files
    Write-Host "Creating component group for published files..." -ForegroundColor Yellow
    $componentsWxsPath = Join-Path $tempDir "Components.wxs"
    $componentsContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <ComponentGroup Id="PublishedComponents" Directory="INSTALLFOLDER">
"@

    # Get all files in the publish directory
    $files = Get-ChildItem -Path $publishDir -Recurse -File

    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($publishDir.Length + 1)
        $id = $relativePath.Replace(".", "_").Replace("\", "_").Replace(" ", "_").Replace("-", "_")

        if ($relativePath -eq "ClipSage.App.exe") {
            # Skip the main executable as it's already included in the main WXS file
            continue
        }

        $componentsContent += @"

      <Component Id="Component_$id" Guid="*">
        <File Id="File_$id" Source="$relativePath" KeyPath="yes" />
      </Component>
"@
    }

    $componentsContent += @"

    </ComponentGroup>
  </Fragment>
</Wix>
"@

    Set-Content -Path $componentsWxsPath -Value $componentsContent

    # Build the MSI using WiX Toolset v6.0
    Write-Host "Building MSI installer using WiX Toolset v6.0..." -ForegroundColor Yellow

    # Run the WiX tool to build the MSI
    & "$wixPath\wix.exe" build -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -bindpath "$publishDir" -o "$outputPath" "$wxsPath" "$componentsWxsPath"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build MSI installer (Exit code: $LASTEXITCODE)"

        # Try a simpler approach without UI extension
        Write-Host "Trying alternative build approach..." -ForegroundColor Yellow
        & "$wixPath\wix.exe" build -ext WixToolset.Util.wixext -bindpath "$publishDir" -o "$outputPath" "$wxsPath"

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
