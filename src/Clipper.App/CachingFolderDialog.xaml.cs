using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using MahApps.Metro.Controls;

namespace Clipper.App
{
    /// <summary>
    /// Interaction logic for CachingFolderDialog.xaml
    /// </summary>
    public partial class CachingFolderDialog : MetroWindow
    {
        private string _selectedFolder;
        private bool _isManuallyEditing = false;

        public string SelectedFolder => _selectedFolder;

        public CachingFolderDialog()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select an empty folder for caching clipboard data";
                dialog.UseDescriptionForTitle = true;

                // If a folder is already selected, start from there
                if (!string.IsNullOrEmpty(_selectedFolder))
                {
                    dialog.SelectedPath = _selectedFolder;
                }
                else if (!string.IsNullOrEmpty(CachingFolderTextBox.Text.Trim()))
                {
                    // Use the manually entered path as a starting point
                    dialog.SelectedPath = CachingFolderTextBox.Text.Trim();
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // Check if the folder exists
                    if (!Directory.Exists(selectedPath))
                    {
                        try
                        {
                            // Create the folder if it doesn't exist
                            Directory.CreateDirectory(selectedPath);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(
                                $"Failed to create the folder: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Check if the folder is empty
                    if (!IsFolderEmpty(selectedPath))
                    {
                        var result = System.Windows.MessageBox.Show(
                            "The selected folder is not empty. ClipSage requires an empty folder for caching. Do you want to select a different folder?",
                            "Folder Not Empty",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // User wants to select a different folder
                            BrowseButton_Click(sender, e);
                            return;
                        }
                        // If user selects No, we'll use the non-empty folder anyway
                    }

                    _isManuallyEditing = true;
                    try
                    {
                        _selectedFolder = selectedPath;
                        CachingFolderTextBox.Text = _selectedFolder;
                    }
                    finally
                    {
                        _isManuallyEditing = false;
                    }
                }
            }
        }

        private bool IsFolderEmpty(string folderPath)
        {
            return !Directory.EnumerateFileSystemEntries(folderPath).Any();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // If the user has manually entered a path, use that
            if (string.IsNullOrEmpty(_selectedFolder) && !string.IsNullOrEmpty(CachingFolderTextBox.Text.Trim()))
            {
                _selectedFolder = CachingFolderTextBox.Text.Trim();
            }

            if (string.IsNullOrEmpty(_selectedFolder))
            {
                System.Windows.MessageBox.Show("Please enter or select a caching folder.", "Caching Folder Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Verify the folder exists and is accessible
            if (!Directory.Exists(_selectedFolder))
            {
                // Ask if the user wants to create the folder
                var result = System.Windows.MessageBox.Show(
                    $"The folder '{_selectedFolder}' does not exist. Would you like to create it?",
                    "Create Folder",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.CreateDirectory(_selectedFolder);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Failed to create the folder: {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            // Final check if the folder is empty
            if (!IsFolderEmpty(_selectedFolder))
            {
                var result = System.Windows.MessageBox.Show(
                    "The selected folder is not empty. Using a non-empty folder may cause issues. Are you sure you want to continue?",
                    "Folder Not Empty",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            // Show help information
            System.Windows.MessageBox.Show(
                "ClipSage requires an empty folder for caching clipboard data.\n\n" +
                "For best results:\n" +
                "1. Choose a folder in a cloud storage service (OneDrive, Google Drive, Dropbox)\n" +
                "2. Make sure the folder is empty\n" +
                "3. Ensure you have write permissions to the folder\n\n" +
                "This allows your clipboard data to be synced across multiple devices.",
                "ClipSage Caching Help",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CachingFolderTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isManuallyEditing)
                return;

            _isManuallyEditing = true;

            try
            {
                string enteredPath = CachingFolderTextBox.Text.Trim();

                if (!string.IsNullOrEmpty(enteredPath))
                {
                    // Check if the path is valid
                    if (IsValidPath(enteredPath))
                    {
                        _selectedFolder = enteredPath;
                    }
                }
            }
            finally
            {
                _isManuallyEditing = false;
            }
        }

        private bool IsValidPath(string path)
        {
            try
            {
                // Check if the path is a valid format
                var fullPath = Path.GetFullPath(path);

                // Check if the directory exists, if not, we'll create it later
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
