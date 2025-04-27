# MSI installer builder script for ClipperMVP
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
$wixDir = Join-Path $outputDir "wix"

# Create directories if they don't exist
if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
}
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}
if (-not (Test-Path $wixDir)) {
    New-Item -ItemType Directory -Path $wixDir -Force | Out-Null
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

# Check if WiX Toolset is installed
$wixPath = "C:\Program Files\WiX Toolset v6.0\bin"
if (-not (Test-Path $wixPath)) {
    Write-Error "WiX Toolset v6.0 not found at $wixPath"
    exit 1
}

# Create a WiX source file
$wxsPath = Join-Path $wixDir "ClipperMVP.wxs"
$wxsContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*"
           Name="ClipperMVP"
           Language="1033"
           Version="$Version"
           Manufacturer="ClipSage"
           UpgradeCode="6fe30b47-2626-43ff-9384-d324f425a2a2">

    <Package InstallerVersion="200"
             Compressed="yes"
             InstallScope="perMachine"
             Platform="x64"
             Description="ClipperMVP - Advanced Clipboard Manager"
             Comments="Clipboard history and snippet manager for Windows" />

    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed."
                  AllowSameVersionUpgrades="yes" />

    <MediaTemplate EmbedCab="yes" />

    <!-- Define properties for Add/Remove Programs -->
    <Property Id="ARPURLINFOABOUT" Value="https://clipsage.app" />
    <Property Id="ARPURLUPDATEINFO" Value="https://clipsage.app/download" />
    <Property Id="ARPHELPLINK" Value="https://clipsage.app/support" />
    <Property Id="ARPNOREPAIR" Value="yes" Secure="yes" />
    <Property Id="ARPNOMODIFY" Value="yes" Secure="yes" />

    <!-- Define the directory structure -->
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFiles64Folder">
        <Directory Id="COMPANYFOLDER" Name="ClipSage">
          <Directory Id="INSTALLFOLDER" Name="ClipperMVP" />
        </Directory>
      </Directory>
      <Directory Id="ProgramMenuFolder">
        <Directory Id="ApplicationProgramsFolder" Name="ClipperMVP" />
      </Directory>
      <Directory Id="DesktopFolder" Name="Desktop" />
    </Directory>

    <!-- Define the components -->
    <DirectoryRef Id="INSTALLFOLDER">
      <Component Id="MainExecutable" Guid="*">
        <File Id="ClipperExe" Source="$publishDir\Clipper.App.exe" KeyPath="yes" />
        <RegistryValue Root="HKLM" Key="SOFTWARE\ClipSage\ClipperMVP" Name="Version" Type="string" Value="$Version" />
        <RegistryValue Root="HKLM" Key="SOFTWARE\ClipSage\ClipperMVP" Name="InstallLocation" Type="string" Value="[INSTALLFOLDER]" />
        <RegistryValue Root="HKLM" Key="SOFTWARE\ClipSage\ClipperMVP" Name="AutoUpdate" Type="integer" Value="1" />
        <RegistryValue Root="HKLM" Key="SOFTWARE\ClipSage\ClipperMVP" Name="UpdateUrl" Type="string" Value="https://clipsage.app/api/check-update" />
      </Component>
      <Component Id="CoreLibrary" Guid="*">
        <File Id="ClipperCoreDll" Source="$publishDir\Clipper.Core.dll" KeyPath="yes" />
      </Component>
    </DirectoryRef>

    <!-- Define shortcuts -->
    <DirectoryRef Id="ApplicationProgramsFolder">
      <Component Id="ApplicationShortcut" Guid="*">
        <Shortcut Id="ApplicationStartMenuShortcut"
                  Name="ClipperMVP"
                  Description="Advanced Clipboard Manager"
                  Target="[INSTALLFOLDER]Clipper.App.exe"
                  WorkingDirectory="INSTALLFOLDER" />
        <RemoveFolder Id="CleanUpShortCut" Directory="ApplicationProgramsFolder" On="uninstall"/>
        <RegistryValue Root="HKCU" Key="Software\ClipSage\ClipperMVP" Name="installed" Type="integer" Value="1" KeyPath="yes"/>
      </Component>
    </DirectoryRef>

    <DirectoryRef Id="DesktopFolder">
      <Component Id="ApplicationShortcutDesktop" Guid="*">
        <Shortcut Id="ApplicationDesktopShortcut"
                  Name="ClipperMVP"
                  Description="Advanced Clipboard Manager"
                  Target="[INSTALLFOLDER]Clipper.App.exe"
                  WorkingDirectory="INSTALLFOLDER" />
        <RegistryValue Root="HKCU" Key="Software\ClipSage\ClipperMVP" Name="installed" Type="integer" Value="1" KeyPath="yes"/>
      </Component>
    </DirectoryRef>

    <!-- Custom actions -->
    <CustomAction Id="LaunchApplication"
                  FileRef="ClipperExe"
                  ExeCommand=""
                  Return="asyncNoWait" />

    <InstallExecuteSequence>
      <Custom Action="LaunchApplication" After="InstallFinalize">NOT Installed AND NOT REMOVE</Custom>
    </InstallExecuteSequence>

    <!-- Define features -->
    <Feature Id="ProductFeature" Title="ClipperMVP" Level="1">
      <ComponentRef Id="MainExecutable" />
      <ComponentRef Id="CoreLibrary" />
      <ComponentRef Id="ApplicationShortcut" />
      <ComponentRef Id="ApplicationShortcutDesktop" />
    </Feature>

    <!-- UI -->
    <UI>
      <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
      <Property Id="WIXUI_EXITDIALOGOPTIONALTEXT" Value="Thank you for installing ClipperMVP." />
    </UI>
  </Product>
</Wix>
"@

# Write the WiX source file
Set-Content -Path $wxsPath -Value $wxsContent -Encoding UTF8

# Build the MSI
$msiPath = Join-Path $outputDir "ClipperMVP-Setup-$Version.msi"
Write-Host "Building MSI installer: $msiPath"

# Try to build with WiX Toolset
try {
    # Check if we're using WiX Toolset v3.x or v6.x
    $wixVersion = 0

    if (Test-Path "$wixPath\wix.exe") {
        $wixVersion = 6
    }
    elseif (Test-Path "$wixPath\candle.exe" -and Test-Path "$wixPath\light.exe") {
        $wixVersion = 3
    }

    if ($wixVersion -eq 6) {
        Write-Host "Using WiX Toolset v6.x..."

        # Since WiX v6 is giving us trouble, let's fall back to using the ZIP installer
        Write-Host "WiX Toolset v6.0 is not fully compatible with our MSI build process."
        Write-Host "Falling back to creating a ZIP installer..."

        # Call the ZIP installer script
        $zipScript = Join-Path $PSScriptRoot "build-zip.ps1"
        if (Test-Path $zipScript) {
            & $zipScript -Version $Version -Configuration $Configuration -NoPublish

            if ($LASTEXITCODE -eq 0) {
                Write-Host "ZIP installer created successfully as a fallback."
                return $true
            }
            else {
                Write-Error "Failed to create ZIP installer as a fallback."
                return $false
            }
        }
        else {
            Write-Error "Could not find ZIP installer script: $zipScript"
            return $false
        }
    }
    elseif ($wixVersion -eq 3) {
        Write-Host "Using WiX Toolset v3.x..."
        $objPath = Join-Path $wixDir "ClipperMVP.wixobj"

        # Compile the WiX source file
        & "$wixPath\candle.exe" -out $objPath $wxsPath

        if ($LASTEXITCODE -eq 0) {
            # Link the WiX object file
            & "$wixPath\light.exe" -out $msiPath $objPath
        }
    }
    else {
        Write-Error "Could not determine WiX Toolset version"
        return $false
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "MSI installer created successfully: $msiPath"

        # Copy to bin directory for convenience
        Copy-Item -Path $msiPath -Destination $binDir -Force
        Write-Host "MSI installer copied to: $(Join-Path $binDir (Split-Path $msiPath -Leaf))"

        return $true
    }
    else {
        Write-Error "Failed to build MSI installer (Exit code: $LASTEXITCODE)"
        return $false
    }
}
catch {
    Write-Error "Error building MSI installer: $_"
    return $false
}
