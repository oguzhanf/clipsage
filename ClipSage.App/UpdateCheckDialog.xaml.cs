using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ClipSage.Core.Update;
using MahApps.Metro.Controls;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for UpdateCheckDialog.xaml
    /// </summary>
    public partial class UpdateCheckDialog : MetroWindow
    {
        private UpdateInfo? _updateInfo;
        private readonly UpdateChecker _updateChecker;

        public UpdateCheckDialog()
        {
            InitializeComponent();
            _updateChecker = UpdateChecker.Instance;
            
            // Start checking for updates when the dialog is loaded
            Loaded += async (s, e) => await CheckForUpdatesAsync();
        }

        /// <summary>
        /// Adds a log message to the log text box
        /// </summary>
        /// <param name="message">The message to add</param>
        /// <param name="isError">Whether this is an error message</param>
        private void AddLogMessage(string message, bool isError = false)
        {
            // Ensure we're on the UI thread
            Dispatcher.Invoke(() =>
            {
                // Add timestamp
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string formattedMessage = $"[{timestamp}] {message}\n";
                
                // Append to log
                LogTextBox.AppendText(formattedMessage);
                
                // Scroll to the end
                LogScrollViewer.ScrollToEnd();
            });
        }

        /// <summary>
        /// Checks for updates and displays the results
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Update UI to show we're checking
                Title = "Checking for Updates";
                ProgressBar.IsIndeterminate = true;
                DownloadButton.IsEnabled = false;
                
                // Log the start of the check
                AddLogMessage("Starting update check...");
                AddLogMessage($"Current version: {_updateChecker.CurrentVersion}");
                AddLogMessage($"Connecting to: {_updateChecker.UpdateUrl}");
                
                // Check for updates
                _updateInfo = await _updateChecker.CheckForUpdateAsync(
                    progress => AddLogMessage(progress)
                );
                
                // Update the last check time in settings
                Properties.Settings.Default.LastUpdateCheck = DateTime.Now;
                Properties.Settings.Default.Save();
                
                // Update UI based on result
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 1.0;
                
                // Show the result
                if (_updateInfo != null && _updateInfo.IsUpdateAvailable)
                {
                    Title = "Update Available";
                    AddLogMessage($"Update available: v{_updateInfo.VersionString}");
                    AddLogMessage($"Release date: {_updateInfo.ReleaseDate.ToShortDateString()}");
                    AddLogMessage("Release notes:");
                    AddLogMessage($"{_updateInfo.ReleaseNotes}");
                    
                    // Enable the download button
                    DownloadButton.IsEnabled = true;
                    DownloadButton.Content = $"Download v{_updateInfo.VersionString}";
                }
                else
                {
                    Title = "No Updates Available";
                    AddLogMessage("You have the latest version.");
                }
            }
            catch (Exception ex)
            {
                // Log the error
                AddLogMessage($"Error checking for updates: {ex.Message}", true);
                
                // Update UI
                Title = "Update Check Failed";
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 0;
            }
        }

        /// <summary>
        /// Handles the Close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles the Download button click
        /// </summary>
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInfo == null || !_updateInfo.IsUpdateAvailable)
            {
                return;
            }

            try
            {
                // Close this dialog
                Close();
                
                // Create a progress dialog for the download
                var progressDialog = new ProgressDialog
                {
                    Owner = Owner, // Pass the owner from this dialog to the progress dialog
                    Title = "Downloading Update",
                    Message = $"Downloading ClipSage v{_updateInfo.VersionString}...",
                    IsIndeterminate = false
                };

                // Create a progress reporter
                var progress = new Progress<double>(value =>
                {
                    progressDialog.Progress = value;
                    progressDialog.Message = $"Downloading ClipSage v{_updateInfo.VersionString}... {value:P0}";
                });

                // Start the download in the background
                var downloadTask = _updateChecker.DownloadUpdateAsync(_updateInfo, progress);

                // Show the progress dialog
                progressDialog.Show();

                // Wait for the download to complete
                var installerPath = await downloadTask;

                // Close the progress dialog
                progressDialog.Close();

                // Check if the download was successful
                if (string.IsNullOrEmpty(installerPath))
                {
                    MessageBox.Show(
                        "Failed to download the update. Please try again later.",
                        "Download Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Ask the user if they want to install now
                var result = MessageBox.Show(
                    "The update has been downloaded. Do you want to install it now?\n\nThe application will close during installation.",
                    "Install Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Install the update
                    if (_updateChecker.InstallUpdate(installerPath))
                    {
                        // Close the application
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to start the installer. You can find it at:\n" + installerPath,
                            "Installation Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error downloading update: {ex.Message}",
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
