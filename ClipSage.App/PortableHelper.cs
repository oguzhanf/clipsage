using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Diagnostics;
using System.Security.Principal;

namespace ClipSage.App
{
    /// <summary>
    /// Helper class for portable application functionality
    /// </summary>
    public static class PortableHelper
    {
        /// <summary>
        /// Checks if the application is running from a portable location
        /// </summary>
        /// <returns>True if running from a portable location, false otherwise</returns>
        public static bool IsRunningFromPortableLocation()
        {
            try
            {
                // Get the current executable path
                string executableDirectory = AppContext.BaseDirectory;

                // Check if the directory is writable
                return IsDirectoryWritable(executableDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking portable status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory is writable
        /// </summary>
        /// <param name="directoryPath">The directory path to check</param>
        /// <returns>True if the directory is writable, false otherwise</returns>
        private static bool IsDirectoryWritable(string directoryPath)
        {
            try
            {
                // Try to create a temporary file in the directory
                string testFile = Path.Combine(directoryPath, $"test_{Guid.NewGuid()}.tmp");
                using (FileStream fs = File.Create(testFile))
                {
                    fs.WriteByte(0);
                }

                // If we get here, the directory is writable
                File.Delete(testFile);
                return true;
            }
            catch
            {
                // If an exception occurs, the directory is not writable
                return false;
            }
        }

        /// <summary>
        /// Gets the default cache folder path. Prefers a cloud-synced folder
        /// (OneDrive → Google Drive → Dropbox) so clipboard history syncs across
        /// machines out of the box. Falls back to %USERPROFILE%\Documents\ClipSage,
        /// then to a Cache folder next to the executable as a last resort.
        /// </summary>
        public static string GetDefaultPortableCacheFolder()
        {
            var cloud = TryFindCloudSyncRoot();
            if (cloud != null)
            {
                return Path.Combine(cloud, "ClipSage");
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documents) && Directory.Exists(documents))
            {
                return Path.Combine(documents, "ClipSage");
            }

            return Path.Combine(AppContext.BaseDirectory, "Cache");
        }

        /// <summary>
        /// Looks for a cloud-sync root on the current machine. Detection order:
        /// 1. OneDrive client env vars (OneDriveCommercial → OneDrive → OneDriveConsumer)
        /// 2. %USERPROFILE%\OneDrive, OneDrive - Personal, OneDrive - {tenant}
        /// 3. %USERPROFILE%\Google Drive
        /// 4. %USERPROFILE%\Dropbox
        /// Returns null if no cloud root is found.
        /// </summary>
        public static string? TryFindCloudSyncRoot()
        {
            // OneDrive sets these env vars in its sign-in flow.
            foreach (var name in new[] { "OneDriveCommercial", "OneDrive", "OneDriveConsumer" })
            {
                var path = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
                return null;

            // OneDrive default install names.
            foreach (var name in new[] { "OneDrive", "OneDrive - Personal" })
            {
                var path = Path.Combine(userProfile, name);
                if (Directory.Exists(path))
                    return path;
            }

            // Tenanted OneDrive (e.g. "OneDrive - Contoso").
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(userProfile, "OneDrive - *"))
                {
                    return dir;
                }
            }
            catch { /* enumeration can fail on locked profiles; ignore */ }

            // Google Drive desktop client (older naming).
            foreach (var name in new[] { "Google Drive", "GoogleDrive" })
            {
                var path = Path.Combine(userProfile, name);
                if (Directory.Exists(path))
                    return path;
            }

            // Dropbox.
            var dropbox = Path.Combine(userProfile, "Dropbox");
            if (Directory.Exists(dropbox))
                return dropbox;

            return null;
        }

        /// <summary>
        /// Copies the application to a new location
        /// </summary>
        /// <param name="destinationPath">The destination path</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool CopyApplicationTo(string destinationPath)
        {
            try
            {
                // Get the current executable path
                string executableDirectory = AppContext.BaseDirectory;
                string executableName = Process.GetCurrentProcess().MainModule?.FileName ?? "ClipSage.App.exe";
                executableName = Path.GetFileName(executableName);

                // Create the destination directory if it doesn't exist
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                // Copy all files from the current directory to the destination
                foreach (string file in Directory.GetFiles(executableDirectory))
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(destinationPath, fileName);
                    File.Copy(file, destFile, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying application: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Creates a desktop shortcut for the application
        /// </summary>
        /// <param name="targetPath">The target path of the executable</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool CreateDesktopShortcut(string targetPath)
        {
            try
            {
                // Check if we have permission to create shortcuts
                if (!IsAdministrator() && !IsDirectoryWritable(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)))
                {
                    MessageBox.Show("You don't have permission to create a desktop shortcut. Try running the application as administrator.",
                        "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Create a desktop shortcut using PowerShell
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "ClipSage.lnk");

                string workingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                string psCommand = $@"
                    $WshShell = New-Object -ComObject WScript.Shell
                    $Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}')
                    $Shortcut.TargetPath = '{targetPath.Replace("'", "''")}'
                    $Shortcut.WorkingDirectory = '{workingDirectory.Replace("'", "''")}'
                    $Shortcut.Description = 'ClipSage Clipboard Manager'
                    $Shortcut.IconLocation = '{targetPath.Replace("'", "''")}'
                    $Shortcut.Save()
                ";

                // Execute the PowerShell command
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-Command \"{psCommand}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        MessageBox.Show($"Error creating desktop shortcut: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating desktop shortcut: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Creates a start menu shortcut for the application
        /// </summary>
        /// <param name="targetPath">The target path of the executable</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool CreateStartMenuShortcut(string targetPath)
        {
            try
            {
                // Check if we have permission to create shortcuts
                string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                if (!IsAdministrator() && !IsDirectoryWritable(startMenuPath))
                {
                    MessageBox.Show("You don't have permission to create a start menu shortcut. Try running the application as administrator.",
                        "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Create a start menu shortcut using PowerShell
                string shortcutPath = Path.Combine(startMenuPath, "ClipSage.lnk");

                string workingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                string psCommand = $@"
                    $WshShell = New-Object -ComObject WScript.Shell
                    $Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}')
                    $Shortcut.TargetPath = '{targetPath.Replace("'", "''")}'
                    $Shortcut.WorkingDirectory = '{workingDirectory.Replace("'", "''")}'
                    $Shortcut.Description = 'ClipSage Clipboard Manager'
                    $Shortcut.IconLocation = '{targetPath.Replace("'", "''")}'
                    $Shortcut.Save()
                ";

                // Execute the PowerShell command
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-Command \"{psCommand}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        MessageBox.Show($"Error creating start menu shortcut: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating start menu shortcut: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Checks if the current process is running as administrator
        /// </summary>
        /// <returns>True if running as administrator, false otherwise</returns>
        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Launches the application at the specified path
        /// </summary>
        /// <param name="executablePath">The path to the executable</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool LaunchApplication(string executablePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching application: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
