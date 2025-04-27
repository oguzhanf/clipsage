using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Clipper.Core.Update
{
    /// <summary>
    /// Handles checking for application updates and downloading new versions
    /// </summary>
    public class UpdateChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _updateUrl = "https://clipsage.app/api/updates";
        private static readonly string _downloadBaseUrl = "https://clipsage.app/download";
        
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
                var response = await _httpClient.GetStringAsync($"{_updateUrl}?version={CurrentVersion}");
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response);
                
                if (updateInfo != null && updateInfo.IsUpdateAvailable)
                {
                    return updateInfo;
                }
                
                return null;
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
                var downloadUrl = $"{_downloadBaseUrl}/{updateInfo.InstallerFileName}";
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
