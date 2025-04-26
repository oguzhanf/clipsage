using System;
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
        
        public string SelectedFolder => _selectedFolder;
        public bool RemindLater => RemindLaterCheckBox.IsChecked ?? false;
        
        public CachingFolderDialog()
        {
            InitializeComponent();
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

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _selectedFolder = dialog.SelectedPath;
                    CachingFolderTextBox.Text = _selectedFolder;
                }
            }
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolder))
            {
                System.Windows.MessageBox.Show("Please select a caching folder.", "Caching Folder Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            DialogResult = true;
            Close();
        }
        
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
