# Building ClipperMVP

This document describes how to build ClipperMVP and create installers.

## Prerequisites

- .NET 9 SDK
- PowerShell 5.1 or later
- WiX Toolset v6.0 (for MSI installers, installed at `C:\Program Files\WiX Toolset v6.0\bin`)

## Building the Project

To build the project and create installers, use the PowerShell scripts in the root directory:

```powershell
.\build-project.ps1
```

This will:
1. Increment the build version
2. Clean the solution
3. Build the solution
4. Publish the application
5. Create MSI and ZIP installers

### Command-Line Options

The main build script supports the following options:

- `-Configuration`: The build configuration to use (default: "Release").
- `-NoVersionIncrement`: Skip incrementing the version.
- `-MSIOnly`: Only create an MSI installer.
- `-ZIPOnly`: Only create a ZIP installer.

Examples:

```powershell
# Build with default options
.\build-project.ps1

# Build without incrementing the version
.\build-project.ps1 -NoVersionIncrement

# Build with Debug configuration
.\build-project.ps1 -Configuration Debug

# Build and only create an MSI installer
.\build-project.ps1 -MSIOnly

# Build and only create a ZIP installer
.\build-project.ps1 -ZIPOnly
```

## Building MSI Installer Directly

To build only the MSI installer:

```powershell
.\build-msi.ps1
```

Options:
- `-Configuration`: The build configuration to use (default: "Release").
- `-NoPublish`: Skip publishing the application first.

## Output

The build scripts create the following output:

- `bin/ClipperMVP-{version}/`: Published application files.
- `bin/ClipperMVP-Setup-{version}.msi`: MSI installer.
- `bin/ClipperMVP-Setup-{version}.zip`: ZIP installer.

## Troubleshooting

If you encounter issues with the WiX Toolset, make sure it's installed at the expected location: `C:\Program Files\WiX Toolset v6.0\bin`.

If the MSI installer fails to build, the script will automatically fall back to creating a ZIP installer.
