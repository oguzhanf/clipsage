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
    /// Handles checking for application updates and downloading new versions
    /// </summary>
    public class UpdateChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _updateUrl = "https://api.github.com/repos/oguzhanf/clipsage/releases";
        private static readonly string _downloadBaseUrl = "https://github.com/oguzhanf/clipsage/releases/download";

        private static UpdateChecker? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton instance of the UpdateChecker
        /// </summary>
        public static UpdateChecker Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new UpdateChecker();
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
        /// Checks if an update is available
        /// </summary>
        /// <returns>Update information if available, null otherwise</returns>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                // Set up the request with proper headers for GitHub API
                var request = new HttpRequestMessage(HttpMethod.Get, _updateUrl);
                request.Headers.Add("User-Agent", "ClipSage");
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Get the response
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                // Parse the GitHub release information
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Check if we have any releases
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                {
                    Debug.WriteLine("No releases found.");
                    return null;
                }

                // Get the first (latest) release
                var latestRelease = root[0];
                Debug.WriteLine($"Found release: {latestRelease.GetProperty("name").GetString()}");

                // Extract version from tag name (e.g., "v1.0.0" -> "1.0.0")
                var tagName = latestRelease.GetProperty("tag_name").GetString() ?? "v0.0.0";
                var versionString = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;

                // Handle pre-release suffixes (e.g., "1.0.12-alpha" -> "1.0.12")
                var dashIndex = versionString.IndexOf('-');
                if (dashIndex > 0)
                {
                    Debug.WriteLine($"Found pre-release suffix: {versionString.Substring(dashIndex)}");
                    versionString = versionString.Substring(0, dashIndex);
                }

                // Parse the version
                if (!Version.TryParse(versionString, out var latestVersion))
                {
                    Debug.WriteLine($"Failed to parse version: {versionString}");
                    return null;
                }

                // Check if this is a newer version
                var isUpdateAvailable = latestVersion > CurrentVersion;

                if (!isUpdateAvailable)
                {
                    return null;
                }

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

                // Find the MSI asset
                if (latestRelease.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString();
                        if (name != null && name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                        {
                            updateInfo.InstallerFileName = name;
                            updateInfo.InstallerSizeBytes = asset.GetProperty("size").GetInt64();
                            break;
                        }
                    }
                }

                return updateInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads the update installer
        /// </summary>
        /// <param name="updateInfo">Update information</param>
        /// <param name="progressCallback">Callback for download progress</param>
        /// <returns>Path to the downloaded installer</returns>
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

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && progressCallback != null)
                    {
                        var progressPercentage = (double)totalBytesRead / totalBytes;
                        progressCallback.Report(progressPercentage);
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
        /// Installs the downloaded update
        /// </summary>
        /// <param name="installerPath">Path to the installer</param>
        /// <returns>True if installation started successfully</returns>
        public bool InstallUpdate(string installerPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing update: {ex.Message}");
                return false;
            }
        }
    }
}
