# ClipperMVP Build System

This directory contains the build scripts for ClipperMVP.

## Build Scripts

### Main Build Script

- `build.ps1`: The main build script that builds the solution and creates installers.

### Installer Scripts

- `installers/build-msi.ps1`: Builds an MSI installer using WiX Toolset.
- `installers/build-zip.ps1`: Builds a ZIP installer.

## Usage

### Building the Solution

To build the solution and create installers, use the PowerShell scripts in the root directory:

```powershell
.\build-project.ps1
```

This will:
1. Increment the build version
2. Clean the solution
3. Build the solution
4. Publish the application
5. Create MSI and ZIP installers

### Command-Line Options for build-project.ps1

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

### Building MSI Installer Directly

To build only the MSI installer:

```powershell
.\build-msi.ps1
```

Options:
- `-Configuration`: The build configuration to use (default: "Release").
- `-NoPublish`: Skip publishing the application first.

## Requirements

- .NET 9 SDK
- PowerShell 5.1 or later
- WiX Toolset v6.0 (for MSI installers, installed at `C:\Program Files\WiX Toolset v6.0\bin`)
