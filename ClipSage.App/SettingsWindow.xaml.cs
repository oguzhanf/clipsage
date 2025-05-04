using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipSage.Core.Update;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            DataContext = _viewModel;

            // Update the last check time display
            UpdateLastCheckTimeDisplay();
        }

        private void UpdateLastCheckTimeDisplay()
        {
            if (_viewModel.LastUpdateCheck > new DateTime(1900, 1, 1))
            {
                LastUpdateCheckText.Text = $"Last checked: {_viewModel.LastUpdateCheck:g}";
            }
            else
            {
                LastUpdateCheckText.Text = "Last checked: Never";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveSettings();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseCachingFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CachingFolderDialog();

            // If a folder is already selected, set it as the initial value
            if (!string.IsNullOrEmpty(_viewModel.CachingFolder))
            {
                dialog.SetInitialFolder(_viewModel.CachingFolder);
            }

            // Show the dialog as a modal dialog
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                string oldFolder = _viewModel.CachingFolder;
                string newFolder = dialog.SelectedFolder;

                // Check if we need to copy files from the old folder to the new one
                if (!string.IsNullOrEmpty(oldFolder) &&
                    !string.IsNullOrEmpty(newFolder) &&
                    !string.Equals(oldFolder, newFolder, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(oldFolder))
                {
                    // Ask the user if they want to copy the files
                    var result = MessageBox.Show(
                        $"Do you want to copy existing cache files from the current folder to the new folder?\n\nFrom: {oldFolder}\nTo: {newFolder}",
                        "Copy Cache Files",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            // Copy all files from the old folder to the new one
                            CopyFilesRecursively(oldFolder, newFolder);
                            MessageBox.Show(
                                "Cache files were successfully copied to the new location.",
                                "Files Copied",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Failed to copy cache files: {ex.Message}\n\nThe cache folder will still be changed, but files were not copied.",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }

                // Update the view model with the new folder
                _viewModel.CachingFolder = newFolder;
                _viewModel.CachingFolderConfigured = true;
            }
        }

        private void CopyFilesRecursively(string sourceDir, string targetDir)
        {
            // Create the target directory if it doesn't exist
            Directory.CreateDirectory(targetDir);

            // Copy all files from source to target
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true); // Overwrite if exists
            }

            // Copy all subdirectories and their contents
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                string destDir = Path.Combine(targetDir, dirName);
                CopyFilesRecursively(directory, destDir);
            }
        }

        private void CachingFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string enteredPath = CachingFolderTextBox.Text.Trim();

            if (string.IsNullOrEmpty(enteredPath))
            {
                CachingFolderWarning.Text = "A caching folder is required.";
                CachingFolderWarning.Visibility = Visibility.Visible;
                return;
            }

            // Check if the path is valid
            if (!IsValidPath(enteredPath))
            {
                CachingFolderWarning.Text = "The entered path is not valid.";
                CachingFolderWarning.Visibility = Visibility.Visible;
                return;
            }

            // Check if the directory exists
            if (!Directory.Exists(enteredPath))
            {
                CachingFolderWarning.Text = "The directory does not exist. It will be created when you save settings.";
                CachingFolderWarning.Visibility = Visibility.Visible;
                return;
            }

            // Path is valid and exists
            CachingFolderWarning.Visibility = Visibility.Collapsed;

            // Update the view model
            _viewModel.CachingFolder = enteredPath;
            _viewModel.CachingFolderConfigured = true;
        }

        private bool IsValidPath(string path)
        {
            try
            {
                // Check if the path is a valid format
                var fullPath = Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            // Open the update check dialog
            var updateCheckDialog = new UpdateCheckDialog();
            updateCheckDialog.Owner = this;

            // Show the dialog
            updateCheckDialog.ShowDialog();

            // Update the last check time display after the dialog is closed
            // (the dialog will have updated the settings if a check was performed)
            UpdateLastCheckTimeDisplay();
        }

        private async Task DownloadAndInstallUpdate(UpdateInfo updateInfo)
        {
            try
            {
                // Create a progress dialog
                var progressDialog = new ProgressDialog
                {
                    Owner = this,
                    Title = "Downloading Update",
                    Message = $"Downloading ClipSage v{updateInfo.VersionString}...",
                    IsIndeterminate = false
                };

                // Create a progress reporter
                var progress = new Progress<double>(value =>
                {
                    progressDialog.Progress = value;
                    progressDialog.Message = $"Downloading ClipSage v{updateInfo.VersionString}... {value:P0}";
                });

                // Start the download in the background
                var updateChecker = UpdateChecker.Instance;
                var downloadTask = updateChecker.DownloadUpdateAsync(updateInfo, progress);

                // Show the progress dialog
                progressDialog.Show();

                // Wait for the download to complete
                var downloadResult = await downloadTask;

                // Close the progress dialog
                progressDialog.Close();

                // Check if the download was successful
                if (!downloadResult.IsSuccess)
                {
                    MessageBox.Show(
                        $"Failed to download the update.\n\nError: {downloadResult.ErrorMessage}\n\n{downloadResult.DetailedError}",
                        "Download Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var installerPath = downloadResult.FilePath;

                // Ask the user if they want to install now
                var result = MessageBox.Show(
                    "The update has been downloaded. Do you want to install it now?\n\nThe application will close during installation.",
                    "Install Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Install the update
                    if (updateChecker.InstallUpdate(installerPath))
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
