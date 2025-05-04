using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClipSage.Core.Update
{
    /// <summary>
    /// Handles updating the portable application
    /// </summary>
    public class PortableUpdater
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _updateUrl = "https://api.github.com/repos/oguzhanf/clipsage/releases";
        private static readonly string _downloadBaseUrl = "https://github.com/oguzhanf/clipsage/releases/download";
        // private static readonly string _updaterFileName = "ClipSageUpdater.exe";

        /// <summary>
        /// Gets the URL used for checking updates
        /// </summary>
        public string UpdateUrl => _updateUrl;

        private static PortableUpdater? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton instance of the PortableUpdater
        /// </summary>
        public static PortableUpdater Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new PortableUpdater();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gets the current application version
        /// </summary>
        public Version CurrentVersion
        {
            get
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version ?? new Version(1, 0, 0, 0);
            }
        }

        /// <summary>
        /// Gets the current application path
        /// </summary>
        public string CurrentApplicationPath
        {
            get
            {
                // For single-file applications, use AppContext.BaseDirectory instead of Assembly.Location
                string appDirectory = AppContext.BaseDirectory;
                string exeName = Process.GetCurrentProcess().MainModule?.FileName ?? "ClipSage.App.exe";
                return Path.Combine(appDirectory, Path.GetFileName(exeName));
            }
        }

        /// <summary>
        /// Checks if an update is available
        /// </summary>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <returns>Update information if available, null otherwise</returns>
        public async Task<UpdateInfo?> CheckForUpdateAsync(Action<string>? progressCallback = null)
        {
            try
            {
                // Report progress
                progressCallback?.Invoke("Setting up request to GitHub API...");

                // Set up the request with proper headers for GitHub API
                var request = new HttpRequestMessage(HttpMethod.Get, _updateUrl);
                request.Headers.Add("User-Agent", "ClipSage");
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Report progress
                progressCallback?.Invoke("Connecting to GitHub API...");

                // Send the request
                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Report progress
                progressCallback?.Invoke("Parsing response...");

                // Parse the response
                var content = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);
                var releases = document.RootElement;

                // Check if we have any releases
                if (releases.ValueKind != JsonValueKind.Array || releases.GetArrayLength() == 0)
                {
                    progressCallback?.Invoke("No releases found.");
                    return null;
                }

                // Get the latest release
                var latestRelease = releases[0];
                var tagName = latestRelease.GetProperty("tag_name").GetString() ?? "";
                var versionString = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;

                // Parse the version
                if (!Version.TryParse(versionString, out var latestVersion))
                {
                    progressCallback?.Invoke($"Invalid version format: {versionString}");
                    return null;
                }

                // Compare with current version
                if (latestVersion <= CurrentVersion)
                {
                    progressCallback?.Invoke($"Current version {CurrentVersion} is up to date.");
                    return null;
                }

                // Report progress
                progressCallback?.Invoke($"Update available: v{versionString}");
                progressCallback?.Invoke("Gathering update details...");

                // Create update info
                var updateInfo = new UpdateInfo
                {
                    IsUpdateAvailable = true,
                    VersionString = versionString,
                    ReleaseNotes = latestRelease.GetProperty("body").GetString() ?? "No release notes available.",
                    ReleaseDate = latestRelease.GetProperty("published_at").GetDateTime(),
                    IsMandatory = false,
                    TagName = tagName // Store the original tag name for download
                };

                // Find the ZIP asset
                if (latestRelease.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString();
                        if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            updateInfo.InstallerFileName = name;
                            updateInfo.InstallerSizeBytes = asset.GetProperty("size").GetInt64();
                            progressCallback?.Invoke($"Found update package: {name} ({FormatFileSize(updateInfo.InstallerSizeBytes)})");
                            break;
                        }
                    }
                }

                progressCallback?.Invoke("Update check completed successfully.");

                return updateInfo;
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Error checking for updates: {ex.Message}");
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads the update package
        /// </summary>
        /// <param name="updateInfo">Update information</param>
        /// <param name="progressCallback">Callback for download progress</param>
        /// <returns>Path to the downloaded package</returns>
        public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progressCallback = null)
        {
            try
            {
                // Use the original tag name from the UpdateInfo
                // If TagName is not set, fall back to the version string with "v" prefix
                var tagName = !string.IsNullOrEmpty(updateInfo.TagName)
                    ? updateInfo.TagName
                    : "v" + updateInfo.VersionString;

                Debug.WriteLine($"Using tag name for download: {tagName}");

                var downloadUrl = $"{_downloadBaseUrl}/{tagName}/{updateInfo.InstallerFileName}";
                var tempPath = Path.Combine(Path.GetTempPath(), updateInfo.InstallerFileName);

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var bytesRead = 0;
                var totalBytesRead = 0L;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && progressCallback != null)
                    {
                        var progress = (double)totalBytesRead / totalBytes;
                        progressCallback.Report(progress);
                    }
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading update: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates an updater script that will replace the application with the new version
        /// </summary>
        /// <param name="updateZipPath">Path to the downloaded update ZIP file</param>
        /// <returns>True if the updater was created successfully</returns>
        public bool CreateUpdaterScript(string updateZipPath)
        {
            try
            {
                // Get the application directory
                string appDirectory = Path.GetDirectoryName(CurrentApplicationPath) ?? AppContext.BaseDirectory;
                string appExeName = Path.GetFileName(CurrentApplicationPath);
                string updaterScriptPath = Path.Combine(Path.GetTempPath(), "ClipSageUpdater.ps1");

                // Create the PowerShell updater script
                string script = $@"
# ClipSage Updater Script
# This script updates the portable ClipSage application

# Wait for the application to exit
Start-Sleep -Seconds 2

# Define paths
$appDirectory = ""{appDirectory.Replace("\\", "\\\\")}""
$appExePath = ""{CurrentApplicationPath.Replace("\\", "\\\\")}""
$updateZipPath = ""{updateZipPath.Replace("\\", "\\\\")}""
$tempExtractPath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), ""ClipSageUpdate"")

Write-Host ""ClipSage Updater: Starting update process...""
Write-Host ""Application directory: $appDirectory""
Write-Host ""Update package: $updateZipPath""

# Create temp directory for extraction if it doesn't exist
if (-not (Test-Path $tempExtractPath)) {{
    New-Item -ItemType Directory -Path $tempExtractPath -Force | Out-Null
}}

try {{
    # Extract the ZIP file
    Write-Host ""Extracting update package...""
    Expand-Archive -Path $updateZipPath -DestinationPath $tempExtractPath -Force

    # Find the executable in the extracted files
    $newExePath = Get-ChildItem -Path $tempExtractPath -Filter ""*.exe"" -Recurse | Select-Object -First 1 -ExpandProperty FullName

    if (-not $newExePath) {{
        Write-Host ""Error: Could not find executable in update package.""
        exit 1
    }}

    Write-Host ""Found new executable: $newExePath""

    # Copy all files from the extracted directory to the app directory
    Write-Host ""Copying files to application directory...""
    $extractedDir = [System.IO.Path]::GetDirectoryName($newExePath)
    Copy-Item -Path ""$extractedDir\*"" -Destination $appDirectory -Recurse -Force

    # Start the updated application
    Write-Host ""Starting updated application...""
    Start-Process -FilePath $appExePath

    # Clean up
    Write-Host ""Cleaning up...""
    Remove-Item -Path $updateZipPath -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""Update completed successfully!""
}}
catch {{
    Write-Host ""Error during update: $_""
    exit 1
}}
";

                // Write the script to a file
                File.WriteAllText(updaterScriptPath, script);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating updater script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs the updater script to install the update
        /// </summary>
        /// <param name="updateZipPath">Path to the downloaded update ZIP file</param>
        /// <returns>True if the updater was started successfully</returns>
        public bool RunUpdater(string updateZipPath)
        {
            try
            {
                // Create the updater script
                if (!CreateUpdaterScript(updateZipPath))
                {
                    return false;
                }

                // Get the updater script path
                string updaterScriptPath = Path.Combine(Path.GetTempPath(), "ClipSageUpdater.ps1");

                // Start PowerShell to run the updater script
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{updaterScriptPath}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running updater: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a scheduled task to check for updates periodically
        /// </summary>
        /// <param name="intervalHours">Interval in hours between update checks</param>
        /// <returns>True if the task was created successfully</returns>
        public bool CreateUpdateCheckerTask(int intervalHours = 24)
        {
            try
            {
                // Get the application directory and executable path
                string appDirectory = Path.GetDirectoryName(CurrentApplicationPath) ?? AppContext.BaseDirectory;
                string appExeName = Path.GetFileName(CurrentApplicationPath);
                string taskName = "ClipSageUpdateChecker";
                string scriptPath = Path.Combine(appDirectory, "UpdateChecker.ps1");

                // Create the PowerShell script for the update checker
                string script = $@"
# ClipSage Update Checker Script
# This script checks for updates to the portable ClipSage application

# Define paths
$appDirectory = ""{appDirectory.Replace("\\", "\\\\")}""
$appExePath = ""{CurrentApplicationPath.Replace("\\", "\\\\")}""

# Check if the application is running
$isRunning = Get-Process -Name ""ClipSage.App"" -ErrorAction SilentlyContinue

# Only proceed if the application is not running
if (-not $isRunning) {{
    # Start the application with the update check parameter
    Start-Process -FilePath $appExePath -ArgumentList ""-checkupdate""
}}
";

                // Write the script to a file
                File.WriteAllText(scriptPath, script);

                // Create the scheduled task
                string taskCommand = $@"
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-ExecutionPolicy Bypass -NoProfile -File ""{scriptPath}""'
$trigger = New-ScheduledTaskTrigger -Daily -At 12:00
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
$principal = New-ScheduledTaskPrincipal -UserId (Get-CimInstance -ClassName Win32_ComputerSystem | Select-Object -ExpandProperty UserName) -LogonType Interactive -RunLevel Limited

# Remove the task if it already exists
Unregister-ScheduledTask -TaskName ""{taskName}"" -Confirm:$false -ErrorAction SilentlyContinue

# Create the new task
Register-ScheduledTask -TaskName ""{taskName}"" -Action $action -Trigger $trigger -Settings $settings -Principal $principal
";

                // Run the PowerShell command to create the task
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"{taskCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating update checker task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted string</returns>
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
