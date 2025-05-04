using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using ClipSage.App.Properties;
using ClipSage.Core.Logging;
using ClipSage.Core.Update;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool _isPortableMode = false;
        private string _originalExecutablePath = string.Empty;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Store the original executable path
            _originalExecutablePath = Assembly.GetExecutingAssembly().Location;

            // Check for command-line arguments
            if (e.Args.Length > 0)
            {
                // Check if we should check for updates
                if (e.Args.Contains("-checkupdate", StringComparer.OrdinalIgnoreCase))
                {
                    CheckForUpdatesAndExit();
                    return;
                }
            }

            // Check if we're running in portable mode
            _isPortableMode = PortableHelper.IsRunningFromPortableLocation();

            // If we're in portable mode, check if we need to set up the portable environment
            if (_isPortableMode)
            {
                // Check if this is the first run in portable mode
                if (!ClipSage.App.Properties.Settings.Default.CachingFolderConfigured ||
                    string.IsNullOrEmpty(ClipSage.App.Properties.Settings.Default.CachingFolder))
                {
                    // Set up the portable environment
                    SetupPortableEnvironment();
                }
                else
                {
                    // Verify the configured folder exists and is accessible
                    string cachingFolder = ClipSage.App.Properties.Settings.Default.CachingFolder;
                    if (!Directory.Exists(cachingFolder))
                    {
                        // Create the folder if it doesn't exist
                        try
                        {
                            Directory.CreateDirectory(cachingFolder);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Failed to create caching folder: {ex.Message}\nPlease select a new folder.",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            ShowCachingFolderDialog();
                            return;
                        }
                    }

                    // Initialize the logger
                    InitializeLogger();

                    // Create and show the main window
                    ShowMainWindow();
                }
            }
            else
            {
                // We're not in portable mode, show the portable setup dialog
                var result = MessageBox.Show(
                    "ClipSage is now a portable application. Would you like to set up ClipSage in a new location?",
                    "ClipSage Portable Setup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Show the portable setup dialog
                    var setupDialog = new PortableSetupDialog();
                    if (setupDialog.ShowDialog() == true)
                    {
                        // Copy the application to the selected destination
                        string destinationPath = setupDialog.SelectedDestination;
                        if (PortableHelper.CopyApplicationTo(destinationPath))
                        {
                            // Get the path to the copied executable
                            string executableName = Path.GetFileName(_originalExecutablePath);
                            string newExecutablePath = Path.Combine(destinationPath, executableName);

                            // Create shortcuts if requested
                            if (setupDialog.CreateDesktopShortcut)
                            {
                                PortableHelper.CreateDesktopShortcut(newExecutablePath);
                            }

                            if (setupDialog.CreateStartMenuShortcut)
                            {
                                PortableHelper.CreateStartMenuShortcut(newExecutablePath);
                            }

                            // Launch the application from the new location
                            MessageBox.Show(
                                "ClipSage has been set up in the selected location. The application will now restart from the new location.",
                                "Setup Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            // Launch the application from the new location and exit
                            PortableHelper.LaunchApplication(newExecutablePath);
                            Shutdown();
                            return;
                        }
                    }
                }

                // If we get here, either the user cancelled or there was an error
                // Continue with the normal startup process
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

                        // Create and show the main window
                        ShowMainWindow();
                    }
                }
            }
        }

        private void SetupPortableEnvironment()
        {
            try
            {
                // Get the default portable cache folder path
                string defaultCacheFolder = PortableHelper.GetDefaultPortableCacheFolder();

                // Create the cache folder if it doesn't exist
                if (!Directory.Exists(defaultCacheFolder))
                {
                    Directory.CreateDirectory(defaultCacheFolder);
                }

                // Set the cache folder in settings
                ClipSage.App.Properties.Settings.Default.CachingFolder = defaultCacheFolder;
                ClipSage.App.Properties.Settings.Default.CachingFolderConfigured = true;
                ClipSage.App.Properties.Settings.Default.Save();

                // Ask the user if they want to create shortcuts
                var result = MessageBox.Show(
                    "Would you like to create shortcuts for ClipSage?",
                    "Create Shortcuts",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Create desktop shortcut
                    PortableHelper.CreateDesktopShortcut(_originalExecutablePath);

                    // Create start menu shortcut
                    PortableHelper.CreateStartMenuShortcut(_originalExecutablePath);
                }

                // Initialize the logger
                InitializeLogger();

                // Create and show the main window
                ShowMainWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to set up portable environment: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Fall back to the caching folder dialog
                ShowCachingFolderDialog();
            }
        }

        private void ShowCachingFolderDialog()
        {
            var dialog = new CachingFolderDialog();

            // If we're in portable mode, suggest the default portable cache folder
            if (_isPortableMode)
            {
                string defaultCacheFolder = PortableHelper.GetDefaultPortableCacheFolder();
                dialog.SetInitialFolder(defaultCacheFolder);
            }

            if (dialog.ShowDialog() == true)
            {
                // User selected a folder
                ClipSage.App.Properties.Settings.Default.CachingFolder = dialog.SelectedFolder;
                ClipSage.App.Properties.Settings.Default.CachingFolderConfigured = true;
                ClipSage.App.Properties.Settings.Default.Save();

                // Initialize the logger now that we have a caching folder
                InitializeLogger();

                // Create and show the main window
                ShowMainWindow();
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
        /// Creates and shows the main window with appropriate startup settings.
        /// </summary>
        private void ShowMainWindow()
        {
            // Create the main window if it doesn't exist
            if (MainWindow == null)
            {
                MainWindow = new MainWindow();
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
                else
                {
                    // Show the window minimized
                    MainWindow.Show();
                }
            }
            else
            {
                // Show the window normally
                MainWindow.Show();
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

        /// <summary>
        /// Checks for updates and exits the application
        /// </summary>
        private async void CheckForUpdatesAndExit()
        {
            try
            {
                // Create a window to show progress
                var updateDialog = new PortableUpdateDialog();
                updateDialog.Show();

                // The dialog will handle the update check and installation
                // We don't need to do anything else here

                // The application will exit when the dialog is closed
                updateDialog.Closed += (s, e) => Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error checking for updates: {ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Exit the application
                Shutdown();
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

            // No need to dispose of any database connections as we're using XML files now

            base.OnExit(e);
        }
    }
}

