using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ClipSage.Core.Update;
using System.Windows.Controls;
using System.Diagnostics;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for PortableUpdateDialog.xaml
    /// </summary>
    public partial class PortableUpdateDialog : Window
    {
        private UpdateInfo? _updateInfo;
        private readonly PortableUpdater _updater;
        private string? _downloadedUpdatePath;

        public PortableUpdateDialog()
        {
            InitializeComponent();
            _updater = PortableUpdater.Instance;

            // Start checking for updates when the dialog is loaded
            Loaded += async (s, e) => await CheckForUpdatesAsync();
        }

        /// <summary>
        /// Adds a message to the log
        /// </summary>
        /// <param name="message">The message to add</param>
        /// <param name="isError">Whether the message is an error</param>
        private void AddLogMessage(string message, bool isError = false)
        {
            // Ensure we're on the UI thread
            Dispatcher.Invoke(() =>
            {
                // Add a timestamp to the message
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var formattedMessage = $"[{timestamp}] {message}";

                // Create a TextBlock for the message
                var textBlock = new TextBlock
                {
                    Text = formattedMessage,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                // Set the color based on whether it's an error
                if (isError)
                {
                    textBlock.Foreground = Brushes.Red;
                }

                // Add the message to the log
                LogTextBox.AppendText(formattedMessage + Environment.NewLine);

                // Scroll to the bottom
                LogScrollViewer.ScrollToBottom();
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
                StatusTextBlock.Text = "Checking for updates...";
                ProgressBar.IsIndeterminate = true;
                UpdateButton.IsEnabled = false;

                // Log the start of the check
                AddLogMessage("Starting update check...");
                AddLogMessage($"Current version: {_updater.CurrentVersion}");
                AddLogMessage($"Connecting to: {_updater.UpdateUrl}");

                // Check for updates
                _updateInfo = await _updater.CheckForUpdateAsync(
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
                    StatusTextBlock.Text = $"A new version of ClipSage is available: v{_updateInfo.VersionString}";

                    // Show version and release date
                    VersionTextBlock.Text = $"Version: v{_updateInfo.VersionString}";
                    VersionTextBlock.Visibility = Visibility.Visible;

                    ReleaseDateTextBlock.Text = $"Released: {_updateInfo.ReleaseDate.ToShortDateString()}";
                    ReleaseDateTextBlock.Visibility = Visibility.Visible;

                    AddLogMessage($"Update available: v{_updateInfo.VersionString}");
                    AddLogMessage($"Release date: {_updateInfo.ReleaseDate.ToShortDateString()}");
                    AddLogMessage("Release notes:");
                    AddLogMessage($"{_updateInfo.ReleaseNotes}");

                    // Enable the update button
                    UpdateButton.IsEnabled = true;
                    UpdateButton.Content = $"Update to v{_updateInfo.VersionString}";
                }
                else
                {
                    Title = "No Updates Available";
                    StatusTextBlock.Text = "You have the latest version of ClipSage.";
                    AddLogMessage("You have the latest version.");
                }
            }
            catch (Exception ex)
            {
                // Log the error
                AddLogMessage($"Error checking for updates: {ex.Message}", true);

                // Update UI
                Title = "Update Check Failed";
                StatusTextBlock.Text = $"Error checking for updates: {ex.Message}";
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 0;
            }
        }

        /// <summary>
        /// Handles the Close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if the user wants to enable automatic update checks
            if (AutoUpdateCheckBox.IsChecked == true)
            {
                try
                {
                    // Create the update checker task
                    if (_updater.CreateUpdateCheckerTask())
                    {
                        AddLogMessage("Automatic update checking has been enabled.");
                    }
                    else
                    {
                        AddLogMessage("Failed to enable automatic update checking.", true);
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"Error setting up automatic updates: {ex.Message}", true);
                }
            }

            Close();
        }

        /// <summary>
        /// Handles the Update button click
        /// </summary>
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInfo == null || !_updateInfo.IsUpdateAvailable)
            {
                return;
            }

            try
            {
                // Disable the update button
                UpdateButton.IsEnabled = false;
                UpdateButton.Content = "Downloading...";

                // Update the progress bar
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 0;

                // Create a progress object to track download progress
                var progress = new Progress<double>(value =>
                {
                    ProgressBar.Value = value;
                    StatusTextBlock.Text = $"Downloading update: {value:P0}";
                });

                // Start the download
                AddLogMessage("Downloading update...");
                var downloadResult = await _updater.DownloadUpdateAsync(_updateInfo, progress);

                // Check if the download was successful
                if (!downloadResult.IsSuccess)
                {
                    AddLogMessage($"Failed to download the update: {downloadResult.ErrorMessage}", true);

                    if (!string.IsNullOrEmpty(downloadResult.DetailedError))
                    {
                        AddLogMessage($"Detailed error: {downloadResult.DetailedError}", true);
                    }

                    MessageBox.Show(
                        $"Failed to download the update.\n\nError: {downloadResult.ErrorMessage}\n\n{downloadResult.DetailedError}",
                        "Download Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Reset the UI
                    UpdateButton.IsEnabled = true;
                    UpdateButton.Content = $"Update to v{_updateInfo.VersionString}";
                    return;
                }

                _downloadedUpdatePath = downloadResult.FilePath;

                AddLogMessage($"Download completed: {_downloadedUpdatePath}");

                // Ask the user if they want to install now
                var result = MessageBox.Show(
                    "The update has been downloaded. Do you want to install it now?\n\nThe application will close during installation.",
                    "Install Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Install the update
                    AddLogMessage("Starting update installation...");
                    if (_updater.RunUpdater(_downloadedUpdatePath))
                    {
                        AddLogMessage("Update process started. The application will now close.");

                        // Close the application
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        AddLogMessage("Failed to start the update process.", true);
                        MessageBox.Show(
                            "Failed to start the update process. You can find the downloaded update at:\n" + _downloadedUpdatePath,
                            "Installation Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        // Reset the UI
                        UpdateButton.IsEnabled = true;
                        UpdateButton.Content = $"Update to v{_updateInfo.VersionString}";
                    }
                }
                else
                {
                    // Reset the UI
                    UpdateButton.IsEnabled = true;
                    UpdateButton.Content = $"Update to v{_updateInfo.VersionString}";
                    AddLogMessage("Update installation postponed.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error during update: {ex.Message}", true);
                MessageBox.Show(
                    $"Error during update: {ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Reset the UI
                UpdateButton.IsEnabled = true;
                UpdateButton.Content = $"Update to v{_updateInfo.VersionString}";
            }
        }
    }
}
