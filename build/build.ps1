# Main build script for ClipperMVP
param (
    [string]$Configuration = "Release",
    [string]$VersionType = "build", # Can be major, minor, build, or revision
    [switch]$NoVersionIncrement = $false,
    [switch]$SkipInstallers = $false,
    [switch]$MSIOnly = $false,
    [switch]$ZIPOnly = $false
)

# Function to increment version
function Increment-Version {
    param (
        [string]$VersionFile,
        [string]$VersionType
    )

    # Read the current version
    $content = Get-Content $VersionFile
    $versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
    $assemblyVersionLine = $content | Where-Object { $_ -match '<AssemblyVersion>(.*)</AssemblyVersion>' }
    $fileVersionLine = $content | Where-Object { $_ -match '<FileVersion>(.*)</FileVersion>' }

    if ($versionLine -and $assemblyVersionLine -and $fileVersionLine) {
        $version = [version]($versionLine -replace '<Version>(.*)</Version>', '$1')
        $assemblyVersion = [version]($assemblyVersionLine -replace '<AssemblyVersion>(.*)</AssemblyVersion>', '$1')
        $fileVersion = [version]($fileVersionLine -replace '<FileVersion>(.*)</FileVersion>', '$1')

        # Increment the version based on the type
        $major = $version.Major
        $minor = $version.Minor
        $build = $version.Build
        $revision = $version.Revision

        switch ($VersionType) {
            "major" {
                $major++
                $minor = 0
                $build = 0
                $revision = 0
            }
            "minor" {
                $minor++
                $build = 0
                $revision = 0
            }
            "build" {
                $build++
                $revision = 0
            }
            "revision" {
                $revision++
            }
        }

        # Create the new version strings
        $newVersion = "$major.$minor.$build"
        $newAssemblyVersion = "$major.$minor.$build.$revision"
        $newFileVersion = "$major.$minor.$build.$revision"

        # Update the version in the file
        $content = $content -replace '<Version>(.*)</Version>', "<Version>$newVersion</Version>"
        $content = $content -replace '<AssemblyVersion>(.*)</AssemblyVersion>', "<AssemblyVersion>$newAssemblyVersion</AssemblyVersion>"
        $content = $content -replace '<FileVersion>(.*)</FileVersion>', "<FileVersion>$newFileVersion</FileVersion>"

        # Write the updated content back to the file
        Set-Content $VersionFile $content

        return $newVersion
    }
    else {
        Write-Error "Could not find version information in $VersionFile"
        return $null
    }
}

# Main build script
try {
    # Set the version file path
    $versionFile = Join-Path $PSScriptRoot "..\Directory.Build.props"

    # Increment the version if requested
    if (-not $NoVersionIncrement) {
        $newVersion = Increment-Version -VersionFile $versionFile -VersionType $VersionType
        if ($newVersion) {
            Write-Host "Version incremented to $newVersion"
        }
        else {
            Write-Error "Failed to increment version"
            exit 1
        }
    }
    else {
        # Get the current version from the file
        $content = Get-Content $versionFile
        $versionLine = $content | Where-Object { $_ -match '<Version>(.*)</Version>' }
        $newVersion = ($versionLine -replace '<Version>(.*)</Version>', '$1').Trim()
        Write-Host "Using current version: $newVersion"
    }

    # Clean the solution
    Write-Host "Cleaning solution..."
    dotnet clean -c $Configuration

    # Build the solution
    Write-Host "Building solution..."
    dotnet build -c $Configuration

    # Create the bin directory if it doesn't exist
    $binDir = Join-Path $PSScriptRoot "..\bin"
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }

    # Create the installers directory if it doesn't exist
    $installersDir = Join-Path $binDir "installers"
    if (-not (Test-Path $installersDir)) {
        New-Item -ItemType Directory -Path $installersDir -Force | Out-Null
    }

    # Publish the application
    Write-Host "Publishing application..."
    $publishDir = Join-Path $binDir "ClipperMVP-$newVersion"

    # Publish the application
    $appProjectPath = Join-Path $PSScriptRoot "..\src\Clipper.App\Clipper.App.csproj"
    Write-Host "Publishing from: $appProjectPath"
    Write-Host "Publishing to: $publishDir"

    try {
        dotnet publish $appProjectPath -c $Configuration -r win-x64 --self-contained false -o $publishDir

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish application (Exit code: $LASTEXITCODE)"
            exit 1
        }
    }
    catch {
        Write-Error "Failed to publish application: $_"
        exit 1
    }

    # Create installers if requested
    if (-not $SkipInstallers) {
        Write-Host "Creating installers..."

        # Build MSI installer if requested
        if (-not $ZIPOnly) {
            Write-Host "Building MSI installer..."
            $msiScript = Join-Path $PSScriptRoot "installers\build-msi.ps1"
            if (Test-Path $msiScript) {
                & $msiScript -Version $newVersion -Configuration $Configuration -NoPublish
            }
            else {
                Write-Warning "MSI installer script not found: $msiScript"
            }
        }

        # Build ZIP installer if requested
        if (-not $MSIOnly) {
            Write-Host "Building ZIP installer..."
            $zipScript = Join-Path $PSScriptRoot "installers\build-zip.ps1"
            if (Test-Path $zipScript) {
                & $zipScript -Version $newVersion -Configuration $Configuration -NoPublish
            }
            else {
                Write-Warning "ZIP installer script not found: $zipScript"
            }
        }
    }

    Write-Host "Build completed successfully"
}
catch {
    Write-Error "Build failed: $_"
    exit 1
}
