# Quick Start Guide: Creating a Release for ClipSage

This guide will help you create a new release of ClipSage using the automated GitHub Actions workflows.

## 🚀 Quick Method (Recommended - No Windows Required!)

### Step 1: Merge This PR

First, merge this pull request to the main branch to get the automated workflows.

### Step 2: Trigger the Build and Release Workflow

1. **Navigate to the Actions tab** in your GitHub repository:
   - Go to: https://github.com/oguzhanf/clipsage/actions

2. **Select the "Build and Release" workflow** from the left sidebar

3. **Click the "Run workflow" button** (top right of the workflow runs list)

4. **Configure the workflow run:**
   - Branch: Select `main` (or whichever branch you want to build from)
   - Create a GitHub release: Keep checked ✅
   
5. **Click "Run workflow"**

6. **Wait for the workflow to complete** (usually takes 2-3 minutes)
   - You can watch the progress in real-time
   - The workflow will build on GitHub's Windows runners
   - No local Windows machine needed!

### Step 3: Verify the Release

1. Navigate to the releases page: https://github.com/oguzhanf/clipsage/releases

2. You should see a new release `v1.0.32` with two files:
   - `ClipSage-1.0.32.exe` - Portable executable (recommended for users)
   - `ClipSage-1.0.32.zip` - ZIP archive with all files

3. Download the executable and test it to ensure everything works

## 📋 What the Workflow Does

The automated workflow:
- ✅ Builds ClipSage on Windows runners
- ✅ Runs all tests
- ✅ Creates a portable single-file executable
- ✅ Creates a ZIP archive with all files
- ✅ Publishes a GitHub release with both artifacts
- ✅ Generates release notes automatically

## 🏷️ Alternative: Tag-Based Release

If you prefer to trigger releases via git tags:

```bash
# Make sure you're on main branch with latest changes
git checkout main
git pull

# Create and push a tag
git tag v1.0.32
git push origin v1.0.32
```

This will automatically trigger the Build and Release workflow.

## 📝 Updating the Version

If you want to release a different version (not 1.0.32):

1. Edit `Directory.Build.props` and update all version fields:
   ```xml
   <Version>1.0.33</Version>
   <AssemblyVersion>1.0.33.0</AssemblyVersion>
   <FileVersion>1.0.33.0</FileVersion>
   <InformationalVersion>1.0.33</InformationalVersion>
   ```

2. Commit and push the change:
   ```bash
   git add Directory.Build.props
   git commit -m "Bump version to 1.0.33"
   git push
   ```

3. Then trigger the workflow as described above

## 🔄 Continuous Integration

The repository now also has a CI workflow that:
- Runs automatically on every push to main branches
- Runs automatically on every pull request
- Builds the project to verify it compiles
- Runs all tests
- Provides early feedback on code changes

## 📚 More Information

For detailed documentation, see:
- [RELEASE_PROCESS.md](../docs/RELEASE_PROCESS.md) - Complete release guide
- [README.md](../README.md) - Project overview and building instructions

## ❓ Need Help?

If you encounter any issues:
1. Check the workflow run logs in the Actions tab
2. Review the [troubleshooting section](../docs/RELEASE_PROCESS.md#troubleshooting) in RELEASE_PROCESS.md
3. Open an issue if you need assistance

---

**Current Version:** 1.0.32  
**Ready to Release:** ✅ Yes - All workflows are configured and ready to use!
