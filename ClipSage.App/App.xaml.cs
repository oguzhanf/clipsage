using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using ClipSage.App.Properties;
using ClipSage.Core.Logging;

namespace ClipSage.App
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
            if (!ClipSage.App.Properties.Settings.Default.CachingFolderConfigured ||
                string.IsNullOrEmpty(ClipSage.App.Properties.Settings.Default.CachingFolder))
            {
                ShowCachingFolderDialog();
            }
            else
            {
                // Verify the configured folder exists and is accessible
                string cachingFolder = ClipSage.App.Properties.Settings.Default.CachingFolder;
                if (!Directory.Exists(cachingFolder))
                {
                    MessageBox.Show(
                        $"The configured caching folder '{cachingFolder}' does not exist or is not accessible. Please select a new folder.",
                        "Caching Folder Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ShowCachingFolderDialog();
                }
                else
                {
                    // Initialize the logger
                    InitializeLogger();
                }
            }

            // Check if we should start minimized
            bool startMinimized = ClipSage.App.Properties.Settings.Default.StartMinimized;
            bool minimizeToTray = ClipSage.App.Properties.Settings.Default.MinimizeToTray;

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
                ClipSage.App.Properties.Settings.Default.CachingFolder = dialog.SelectedFolder;
                ClipSage.App.Properties.Settings.Default.CachingFolderConfigured = true;
                ClipSage.App.Properties.Settings.Default.Save();

                // Initialize the logger now that we have a caching folder
                InitializeLogger();
            }
            else
            {
                // User didn't select a folder, exit the application
                MessageBox.Show(
                    "A caching folder is required to use ClipSage. The application will now exit.",
                    "Caching Folder Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Shutdown();
            }
        }

        /// <summary>
        /// Initializes the logger with the current caching folder.
        /// </summary>
        private void InitializeLogger()
        {
            try
            {
                string cachingFolder = ClipSage.App.Properties.Settings.Default.CachingFolder;
                if (!string.IsNullOrEmpty(cachingFolder) && Directory.Exists(cachingFolder))
                {
                    // Initialize the logger
                    Logger.Instance.Initialize(cachingFolder);
                    Logger.Instance.Info("Application started");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing logger: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Save any application settings
            ClipSage.App.Properties.Settings.Default.Save();

            // Log application exit
            try
            {
                Logger.Instance.Info("Application exiting");
                Logger.Instance.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down logger: {ex.Message}");
            }

            // Dispose of the database connection manager
            try
            {
                ClipSage.Core.Storage.DatabaseConnectionManager.Instance.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing database connection: {ex.Message}");
                Logger.Instance.Error("Error disposing database connection", ex);
            }

            base.OnExit(e);
        }
    }
}

