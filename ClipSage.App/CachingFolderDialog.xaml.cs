using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for CachingFolderDialog.xaml
    /// </summary>
    public partial class CachingFolderDialog : Window
    {
        private string _selectedFolder = string.Empty;
        private bool _isManuallyEditing = false;

        public string SelectedFolder => _selectedFolder;

        public CachingFolderDialog()
        {
            InitializeComponent();
        }

        public void SetInitialFolder(string folderPath)
        {
            if (!string.IsNullOrEmpty(folderPath))
            {
                _selectedFolder = folderPath;
                _isManuallyEditing = true;
                try
                {
                    CachingFolderTextBox.Text = folderPath;
                }
                finally
                {
                    _isManuallyEditing = false;
                }
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder for caching clipboard data";
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

                    // Check if the folder contains ClipSage cache files
                    if (ContainsClipSageCache(selectedPath))
                    {
                        var result = System.Windows.MessageBox.Show(
                            "This folder appears to contain existing ClipSage cache data. Would you like to reuse this cache folder?",
                            "Existing Cache Detected",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.No)
                        {
                            // User doesn't want to reuse the cache folder
                            BrowseButton_Click(sender, e);
                            return;
                        }
                        // If user selects Yes, we'll reuse the existing cache folder
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

        /// <summary>
        /// Checks if the folder contains ClipSage cache files or folders
        /// </summary>
        /// <param name="folderPath">The folder path to check</param>
        /// <returns>True if the folder contains ClipSage cache files or folders</returns>
        private bool ContainsClipSageCache(string folderPath)
        {
            // Check for the history.db file
            if (File.Exists(Path.Combine(folderPath, "history.db")))
                return true;

            // Check for cache subfolders
            string[] cacheFolders = { "Text", "Images", "FilePaths", "Files" };
            foreach (var folder in cacheFolders)
            {
                if (Directory.Exists(Path.Combine(folderPath, folder)))
                    return true;
            }

            return false;
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

            // Check if the folder contains non-ClipSage files
            if (!Directory.Exists(_selectedFolder) || !ContainsClipSageCache(_selectedFolder))
            {
                // Only warn if the folder exists and contains other files
                if (Directory.Exists(_selectedFolder) && Directory.EnumerateFileSystemEntries(_selectedFolder).Any())
                {
                    var result = System.Windows.MessageBox.Show(
                        "The selected folder contains files that don't appear to be ClipSage cache files. " +
                        "ClipSage will create its own cache structure in this folder without modifying existing files. Continue?",
                        "Non-Cache Files Detected",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }
                }
            }

            DialogResult = true;
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            // Show help information
            System.Windows.MessageBox.Show(
                "ClipSage needs a folder for caching clipboard data.\n\n" +
                "For best results:\n" +
                "1. Choose a folder in a cloud storage service (OneDrive, Google Drive, Dropbox)\n" +
                "2. If you've used ClipSage before, you can reuse your existing cache folder\n" +
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
