using System;
using System.Windows;
using System.Windows.Forms;
using MahApps.Metro.Controls;

namespace Clipper.App
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : MetroWindow
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            DataContext = _viewModel;
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
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder for caching clipboard data";
                dialog.UseDescriptionForTitle = true;

                // If a folder is already selected, start from there
                if (!string.IsNullOrEmpty(_viewModel.CachingFolder))
                {
                    dialog.SelectedPath = _viewModel.CachingFolder;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _viewModel.CachingFolder = dialog.SelectedPath;
                    _viewModel.CachingFolderConfigured = true;
                }
            }
        }
    }
}
