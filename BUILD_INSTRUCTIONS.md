# Instructions for Building and Releasing v1.0.32

## Summary of Changes

The version checking issue has been fixed by:

1. **Version Updated**: Changed from `1.0.29` to `1.0.32` in `Directory.Build.props`
   - This ensures the app will correctly identify itself as v1.0.32
   - When released as v1.0.32 on GitHub, users won't see false "update available" messages

2. **Improved Build Scripts**: 
   - `create-release.ps1` now auto-detects version from `Directory.Build.props`
   - Supports both MSI and EXE files for releases
   - Added `build-and-release.ps1` to automate the entire process

3. **Documentation**: Created `docs/RELEASE_PROCESS.md` with comprehensive instructions

## What You Need to Do

Since this is a Windows WPF application, the build must be completed on a Windows machine.

### Prerequisites

Make sure you have:
- [ ] Windows machine
- [ ] .NET 9 SDK installed (`dotnet --version`)
- [ ] PowerShell 5.1 or later
- [ ] GitHub CLI installed (`winget install GitHub.cli`)
- [ ] GitHub CLI authenticated (`gh auth login`)

### Build and Release Steps

1. **Pull the latest changes**
   ```bash
   git pull origin copilot/fix-version-check-issue
   ```

2. **Run the automated build and release script**
   ```powershell
   .\scripts\build-and-release.ps1
   ```

   This will:
   - Build ClipSage v1.0.32
   - Create the executable in `bin/ClipSage-1.0.32.exe`
   - Create a git tag `v1.0.32`
   - Push the tag to GitHub
   - Create a GitHub release with the executable

3. **Verify the release**
   - Go to https://github.com/oguzhanf/clipsage/releases
   - You should see `v1.0.32` as the latest release
   - Download the executable and test it
   - Check Help > About to verify it shows v1.0.32
   - Check Help > Check for Updates - it should say "You have the latest version"

## Alternative: Manual Steps

If you prefer to do it manually:

```powershell
# 1. Build the portable executable
.\scripts\build-portable.ps1

# 2. Copy the main executable for release
Copy-Item "bin\ClipSage-1.0.32\ClipSage.App.exe" "bin\ClipSage-1.0.32.exe"

# 3. Create the GitHub release
.\scripts\create-release.ps1
```

## Troubleshooting

### If the build fails:
- Ensure you're on Windows
- Verify .NET 9 SDK is installed: `dotnet --version`
- Try cleaning: `dotnet clean ClipSage.sln`

### If GitHub release creation fails:
- Check GitHub CLI is installed: `gh --version`
- Check authentication: `gh auth status`
- Re-authenticate if needed: `gh auth login`

## Why This Fixes the Issue

**Before:**
- Code version: 1.0.29
- GitHub latest release: v1.0.31
- Result: App thinks it's outdated and shows "update available"

**After:**
- Code version: 1.0.32
- GitHub latest release: v1.0.32 (after you build and release)
- Result: App correctly identifies it has the latest version

The version in the assembly (`Directory.Build.props`) now matches the release tag, so the update checker will work correctly.

## Questions?

For more detailed information, see `docs/RELEASE_PROCESS.md`.
