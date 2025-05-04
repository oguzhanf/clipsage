﻿using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for PortableSetupDialog.xaml
    /// </summary>
    public partial class PortableSetupDialog : Window
    {
        private string _currentLocation;
        private string _destinationFolder;
        private bool _isManuallyEditing = false;

        public string SelectedDestination { get; private set; }
        public bool CreateDesktopShortcut { get; private set; }
        public bool CreateStartMenuShortcut { get; private set; }

        public PortableSetupDialog()
        {
            InitializeComponent();

            // Get the current executable path
            _currentLocation = Assembly.GetExecutingAssembly().Location;
            string executableDirectory = Path.GetDirectoryName(_currentLocation) ?? string.Empty;
            CurrentLocationTextBlock.Text = executableDirectory;

            // Set default destination folder
            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _destinationFolder = Path.Combine(documentsFolder, "ClipSage");
            DestinationFolderTextBox.Text = _destinationFolder;

            // Set default values
            CreateDesktopShortcut = true;
            CreateStartMenuShortcut = true;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a destination folder for ClipSage";
                dialog.UseDescriptionForTitle = true;

                // If a folder is already selected, start from there
                if (!string.IsNullOrEmpty(_destinationFolder))
                {
                    dialog.SelectedPath = _destinationFolder;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // Check if the selected path is valid
                    if (string.IsNullOrEmpty(selectedPath))
                    {
                        MessageBox.Show("Please select a valid folder.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Check if the selected path is the same as the current location
                    string currentDir = Path.GetDirectoryName(_currentLocation) ?? string.Empty;
                    if (string.Equals(selectedPath, currentDir, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("The destination folder cannot be the same as the current location.",
                            "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _isManuallyEditing = true;
                    try
                    {
                        _destinationFolder = selectedPath;
                        DestinationFolderTextBox.Text = _destinationFolder;

                        // Check if this is a OneDrive folder
                        CheckForOneDriveFolder(_destinationFolder);
                    }
                    finally
                    {
                        _isManuallyEditing = false;
                    }
                }
            }
        }

        private void DestinationFolderTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isManuallyEditing)
                return;

            _isManuallyEditing = true;
            try
            {
                string enteredPath = DestinationFolderTextBox.Text.Trim();

                if (!string.IsNullOrEmpty(enteredPath))
                {
                    // Check if the path is valid
                    if (IsValidPath(enteredPath))
                    {
                        _destinationFolder = enteredPath;

                        // Check if this is a OneDrive folder
                        CheckForOneDriveFolder(_destinationFolder);
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
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CheckForOneDriveFolder(string folderPath)
        {
            // Check if the path contains "OneDrive"
            if (folderPath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                OneDriveWarningPanel.Visibility = Visibility.Visible;
            }
            else
            {
                OneDriveWarningPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SetupButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate the destination folder
            if (string.IsNullOrEmpty(_destinationFolder))
            {
                ShowError("Please select a valid destination folder.");
                return;
            }

            // Check if the destination folder is writable
            if (!IsDirectoryWritable(_destinationFolder))
            {
                ShowError("The selected destination folder is not writable. Please choose a different folder or run the application as administrator.");
                return;
            }

            // Set the return values
            SelectedDestination = _destinationFolder;
            CreateDesktopShortcut = CreateDesktopShortcutCheckBox.IsChecked ?? false;
            CreateStartMenuShortcut = CreateStartMenuShortcutCheckBox.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            StatusMessageTextBlock.Text = message;
            StatusMessageTextBlock.Visibility = Visibility.Visible;
        }

        private bool IsDirectoryWritable(string directoryPath)
        {
            try
            {
                // Create the directory if it doesn't exist
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

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
    }
}
