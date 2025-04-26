using System;
using System.Configuration;
using System.Data;
using System.Windows;
using Clipper.App.Properties;

namespace Clipper.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if caching folder is configured
            if (!Clipper.App.Properties.Settings.Default.CachingFolderConfigured)
            {
                ShowCachingFolderDialog();
            }

            // Check if we should start minimized
            bool startMinimized = Clipper.App.Properties.Settings.Default.StartMinimized;
            bool minimizeToTray = Clipper.App.Properties.Settings.Default.MinimizeToTray;

            if (startMinimized)
            {
                MainWindow.WindowState = WindowState.Minimized;

                if (minimizeToTray)
                {
                    // Hide the main window
                    MainWindow.Hide();
                }
            }
        }

        private void ShowCachingFolderDialog()
        {
            var dialog = new CachingFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                // User selected a folder
                Clipper.App.Properties.Settings.Default.CachingFolder = dialog.SelectedFolder;
                Clipper.App.Properties.Settings.Default.CachingFolderConfigured = true;
                Clipper.App.Properties.Settings.Default.Save();
            }
            else if (!dialog.RemindLater)
            {
                // User skipped and doesn't want to be reminded
                Clipper.App.Properties.Settings.Default.CachingFolderConfigured = true;
                Clipper.App.Properties.Settings.Default.Save();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Save any application settings
            // We'll implement this later

            base.OnExit(e);
        }
    }
}

