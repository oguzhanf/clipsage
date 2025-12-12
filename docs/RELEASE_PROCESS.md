# Release Process for ClipSage

This document describes the proper process for building and releasing ClipSage to ensure version numbers are correctly synchronized between the code and GitHub releases.

## Understanding the Version Check Issue

ClipSage checks for updates by:
1. Reading its own version from the assembly (defined in `Directory.Build.props`)
2. Querying the GitHub API for the latest release tag
3. Comparing the two versions

**The Problem:** If the version in the code (e.g., 1.0.29) is lower than the latest GitHub release (e.g., 1.0.31), users will always see an "update available" message even if they're running the latest version.

**The Solution:** Always increment the version in `Directory.Build.props` BEFORE building and creating a new release.

## Version Number Location

The version number is defined in `/Directory.Build.props`:

```xml
<PropertyGroup>
  <Version>1.0.32</Version>
  <AssemblyVersion>1.0.32.0</AssemblyVersion>
  <FileVersion>1.0.32.0</FileVersion>
  <InformationalVersion>1.0.32</InformationalVersion>
  ...
</PropertyGroup>
```

## Automated Build and Release Process

### Prerequisites

1. Windows machine (required for WPF/Windows-specific builds)
2. .NET 9 SDK installed
3. PowerShell 5.1 or later
4. GitHub CLI (`gh`) installed and authenticated
   - Install with winget: `winget install GitHub.cli`
   - Or with chocolatey: `choco install gh`
   - Or download from: https://cli.github.com/
   - Login: `gh auth login`

### Steps

1. **Update the Version Number**
   
   Edit `Directory.Build.props` and increment the version number:
   ```xml
   <Version>1.0.32</Version>
   <AssemblyVersion>1.0.32.0</AssemblyVersion>
   <FileVersion>1.0.32.0</FileVersion>
   <InformationalVersion>1.0.32</InformationalVersion>
   ```

2. **Commit the Version Change**
   
   ```bash
   git add Directory.Build.props
   git commit -m "Bump version to 1.0.32"
   git push
   ```

3. **Build and Release**
   
   Run the automated build and release script:
   ```powershell
   .\scripts\build-and-release.ps1
   ```
   
   Or with custom release notes:
   ```powershell
   .\scripts\build-and-release.ps1 -ReleaseNotes "## ClipSage v1.0.32

   ### What's New
   - Fixed critical bug
   - Added new feature
   
   ### Installation
   1. Download the executable
   2. Run it
   3. Enjoy!"
   ```

## Manual Build and Release Process

If you prefer to do it manually:

### 1. Build the Portable Executable

```powershell
.\scripts\build-portable.ps1
```

This creates:
- `bin/ClipSage-{version}/` - Published application files
- `bin/ClipSage-{version}.zip` - Zipped application

### 2. Copy the Main Executable

```powershell
# Copy the main executable for release
$version = "1.0.32"  # Replace with your version
Copy-Item "bin\ClipSage-$version\ClipSage.App.exe" "bin\ClipSage-$version.exe"
```

### 3. Create the GitHub Release

```powershell
.\scripts\create-release.ps1
```

This will:
- Read the version from `Directory.Build.props`
- Look for the executable in `bin/ClipSage-{version}.exe`
- Create a git tag `v{version}`
- Push the tag to GitHub
- Create a GitHub release with the executable

## Verifying the Release

After creating a release:

1. **Check the GitHub Release Page**
   - Go to https://github.com/oguzhanf/clipsage/releases
   - Verify the latest release has the correct version tag (e.g., `v1.0.32`)
   - Verify the executable is attached (`ClipSage-1.0.32.exe`)

2. **Download and Test**
   - Download the executable from the release
   - Run it and check Help > About to see the version
   - Go to Help > Check for Updates
   - It should say "You have the latest version" or show no update available

## Troubleshooting

### "Build failed with exit code 1"
- Make sure you're on Windows
- Ensure .NET 9 SDK is installed: `dotnet --version`
- Try cleaning the solution: `dotnet clean`

### "GitHub CLI not found"
- Install GitHub CLI: `winget install GitHub.cli`
- Or download from https://cli.github.com/

### "Not logged in to GitHub"
- Run: `gh auth login`
- Follow the prompts to authenticate

### "Version mismatch after release"
- Make sure you updated `Directory.Build.props` BEFORE building
- Check that all 4 version fields are updated:
  - `<Version>`
  - `<AssemblyVersion>`
  - `<FileVersion>`
  - `<InformationalVersion>`

## Best Practices

1. **Always increment version before building**
   - Never build and release with the same version as the previous release
   - Use semantic versioning (MAJOR.MINOR.PATCH)

2. **Test locally before releasing**
   - Build the executable
   - Run it locally to ensure it works
   - Check the version in Help > About

3. **Write meaningful release notes**
   - Describe what changed
   - List bug fixes and new features
   - Include installation instructions

4. **Keep versions synchronized**
   - The version in code should match the release tag
   - Example: Code has `1.0.32` → Release is `v1.0.32` → Executable is `ClipSage-1.0.32.exe`
