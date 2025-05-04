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

        /// <summary>
        /// Gets the URL used for checking updates
        /// </summary>
        public string UpdateUrl => _updateUrl;

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

                // Get the response
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Report progress
                progressCallback?.Invoke("Receiving data from GitHub...");

                var content = await response.Content.ReadAsStringAsync();

                // Report progress
                progressCallback?.Invoke("Parsing release information...");

                // Parse the GitHub release information
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Check if we have any releases
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                {
                    Debug.WriteLine("No releases found.");
                    progressCallback?.Invoke("No releases found.");
                    return null;
                }

                // Get the first (latest) release
                var latestRelease = root[0];
                var releaseName = latestRelease.GetProperty("name").GetString() ?? "Unknown";
                Debug.WriteLine($"Found release: {releaseName}");
                progressCallback?.Invoke($"Found latest release: {releaseName}");

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

                progressCallback?.Invoke($"Comparing versions - Latest: {latestVersion}, Current: {CurrentVersion}");

                if (!isUpdateAvailable)
                {
                    progressCallback?.Invoke("No update available. You have the latest version.");
                    return null;
                }

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
                            progressCallback?.Invoke($"Found installer: {name} ({FormatFileSize(updateInfo.InstallerSizeBytes)})");
                            break;
                        }
                    }
                }

                progressCallback?.Invoke("Update check completed successfully.");

                return updateInfo;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error checking for updates: {ex.Message}";
                Debug.WriteLine(errorMessage);
                progressCallback?.Invoke(errorMessage);
                return null;
            }
        }

        /// <summary>
        /// Result of the download operation
        /// </summary>
        public class DownloadResult
        {
            /// <summary>
            /// Path to the downloaded file if successful
            /// </summary>
            public string? FilePath { get; set; }

            /// <summary>
            /// Error message if the download failed
            /// </summary>
            public string? ErrorMessage { get; set; }

            /// <summary>
            /// Detailed error information if available
            /// </summary>
            public string? DetailedError { get; set; }

            /// <summary>
            /// HTTP status code if applicable
            /// </summary>
            public int? StatusCode { get; set; }

            /// <summary>
            /// Whether the download was successful
            /// </summary>
            public bool IsSuccess => !string.IsNullOrEmpty(FilePath) && string.IsNullOrEmpty(ErrorMessage);
        }

        /// <summary>
        /// Downloads the update installer
        /// </summary>
        /// <param name="updateInfo">Update information</param>
        /// <param name="progressCallback">Callback for download progress</param>
        /// <returns>Download result with path to the downloaded installer or error information</returns>
        public async Task<DownloadResult> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progressCallback = null)
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

                // Log the download URL for debugging
                Debug.WriteLine($"Download URL: {downloadUrl}");

                try
                {
                    using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                    // Check for HTTP errors
                    if (!response.IsSuccessStatusCode)
                    {
                        return new DownloadResult
                        {
                            ErrorMessage = $"HTTP error: {(int)response.StatusCode} {response.ReasonPhrase}",
                            DetailedError = $"Failed to download from {downloadUrl}. Server returned status code {(int)response.StatusCode} {response.ReasonPhrase}.",
                            StatusCode = (int)response.StatusCode
                        };
                    }

                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using var contentStream = await response.Content.ReadAsStreamAsync();

                    // Check if the file already exists and try to delete it
                    if (File.Exists(tempPath))
                    {
                        try
                        {
                            File.Delete(tempPath);
                        }
                        catch (IOException ioEx)
                        {
                            return new DownloadResult
                            {
                                ErrorMessage = "File access error",
                                DetailedError = $"Could not access the temporary file: {tempPath}. The file may be in use by another process. Details: {ioEx.Message}"
                            };
                        }
                    }

                    try
                    {
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
                    }
                    catch (IOException ioEx)
                    {
                        return new DownloadResult
                        {
                            ErrorMessage = "File write error",
                            DetailedError = $"Error writing to temporary file: {tempPath}. Details: {ioEx.Message}"
                        };
                    }

                    // Verify the file exists and has content
                    var fileInfo = new FileInfo(tempPath);
                    if (!fileInfo.Exists || fileInfo.Length == 0)
                    {
                        return new DownloadResult
                        {
                            ErrorMessage = "Download verification failed",
                            DetailedError = $"The downloaded file is missing or empty: {tempPath}"
                        };
                    }

                    return new DownloadResult { FilePath = tempPath };
                }
                catch (HttpRequestException httpEx)
                {
                    return new DownloadResult
                    {
                        ErrorMessage = "Network error",
                        DetailedError = $"Network error while downloading update: {httpEx.Message}. Status code: {httpEx.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading update: {ex.Message}");
                return new DownloadResult
                {
                    ErrorMessage = "Download failed",
                    DetailedError = $"Unexpected error during download: {ex.Message}\n{ex.StackTrace}"
                };
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

        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">The size in bytes</param>
        /// <returns>A formatted string (e.g., "1.23 MB")</returns>
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

            return $"{number:n2} {suffixes[counter]}";
        }
    }
}
