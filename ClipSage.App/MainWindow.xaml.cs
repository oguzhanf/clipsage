using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using ClipSage.Core.Storage;
using ClipSage.Core.Update;
using ClipSage.Core.Logging;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _closeToTray;
        private bool _forceClose;
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Update window title and status bar to include version
            var version = UpdateChecker.Instance.CurrentVersion;
            Title = $"ClipSage v{version} - Advanced Clipboard Manager";
            VersionText.Text = $"v{version}";

            // Initialize _closeToTray from settings
            _closeToTray = Properties.Settings.Default.MinimizeToTray;

            // Set up the preview pane
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedEntry))
                {
                    UpdatePreviewPane();
                }
            };

            // Initialize with empty preview
            PreviewPane.ContentTemplate = PreviewPane.Resources["EmptyPreviewTemplate"] as DataTemplate;

            // Set up system tray icon
            SetupTrayIcon();

            // Register global hotkey
            RegisterGlobalHotkey();




            // Check for updates on startup if enabled
            if (Properties.Settings.Default.CheckForUpdates)
            {
                // Check for updates silently in the background
                _ = CheckForUpdatesSilentlyAsync();
            }
        }

        /// <summary>
        /// Indicates whether the tray icon has been successfully initialized
        /// </summary>
        public bool IsTrayIconInitialized { get; private set; }

        private void SetupTrayIcon()
        {
            try
            {
                // Log that we're setting up the tray icon
                Console.WriteLine("Setting up tray icon...");
                Logger.Instance.Info("Setting up tray icon");

                // Create the tray icon programmatically
                _trayIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon();
                _trayIcon.ToolTipText = "ClipSage";
                _trayIcon.TrayLeftMouseDown += TrayIcon_TrayLeftMouseDown;
                _trayIcon.ContextMenu = Resources["TrayMenu"] as ContextMenu;
                _trayIcon.Visibility = Visibility.Visible; // Explicitly set visibility

                try
                {
                    // Try to load the icon from resources
                    var iconUri = new Uri("pack://application:,,,/Resources/clipboard.ico", UriKind.Absolute);
                    _trayIcon.Icon = new System.Drawing.Icon(Application.GetResourceStream(iconUri).Stream);
                    Console.WriteLine("Successfully loaded tray icon from resources");
                    Logger.Instance.Info("Successfully loaded tray icon from resources");
                }
                catch (Exception ex)
                {
                    // If loading fails, use a system icon as fallback
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                    Console.WriteLine($"Failed to load tray icon: {ex.Message}");
                    Logger.Instance.Warning($"Failed to load tray icon: {ex.Message}");
                }

                // Verify the tray icon is properly initialized
                if (_trayIcon.Icon != null)
                {
                    IsTrayIconInitialized = true;
                    Console.WriteLine("Tray icon successfully initialized");
                    Logger.Instance.Info("Tray icon successfully initialized");
                }
                else
                {
                    IsTrayIconInitialized = false;
                    Console.WriteLine("Tray icon initialization failed: Icon is null");
                    Logger.Instance.Warning("Tray icon initialization failed: Icon is null");
                }

                // Update status bar on startup
                _viewModel.EventStatusText = "ClipSage is running in the background";
            }
            catch (Exception ex)
            {
                // Log any exceptions during tray icon setup
                IsTrayIconInitialized = false;
                Console.WriteLine($"Error setting up tray icon: {ex.Message}");
                Logger.Instance.Error("Error setting up tray icon", ex);

                // Show a message to the user
                MessageBox.Show(
                    $"Failed to initialize system tray icon: {ex.Message}\n\nThe application will continue to run, but you may not see the tray icon.",
                    "Tray Icon Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RegisterGlobalHotkey()
        {
            try
            {
                // Register Ctrl+Shift+V as global hotkey
                NHotkey.Wpf.HotkeyManager.Current.AddOrReplace("ShowClipperQuickPicker", Key.V, ModifierKeys.Control | ModifierKeys.Shift, OnGlobalHotkey);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to register global hotkey: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnGlobalHotkey(object? sender, NHotkey.HotkeyEventArgs e)
        {
            // Show the quick picker
            ShowQuickPicker();
            e.Handled = true;
        }

        private void ShowQuickPicker()
        {
            // For now, just show the main window
            // In the future, this could be a lightweight popup
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Show();
            Activate();
        }

        private void UpdatePreviewPane()
        {
            if (_viewModel.SelectedEntry == null)
            {
                PreviewPane.ContentTemplate = PreviewPane.Resources["EmptyPreviewTemplate"] as DataTemplate;
                PreviewPane.Content = null;
                return;
            }

            if (_viewModel.SelectedEntry.DataType == ClipSage.Core.Storage.ClipboardDataType.Text)
            {
                PreviewPane.ContentTemplate = PreviewPane.Resources["TextPreviewTemplate"] as DataTemplate;
                PreviewPane.Content = _viewModel.SelectedEntry;
            }
            else if (_viewModel.SelectedEntry.DataType == ClipSage.Core.Storage.ClipboardDataType.Image && _viewModel.SelectedEntry.ImageBytes != null)
            {
                try
                {
                    var imageViewModel = new ImagePreviewViewModel(_viewModel.SelectedEntry);
                    PreviewPane.ContentTemplate = PreviewPane.Resources["ImagePreviewTemplate"] as DataTemplate;
                    PreviewPane.Content = imageViewModel;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    PreviewPane.ContentTemplate = PreviewPane.Resources["EmptyPreviewTemplate"] as DataTemplate;
                    PreviewPane.Content = null;
                }
            }
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedEntry != null)
            {
                await _viewModel.CopyToClipboardAsync(_viewModel.SelectedEntry);
            }
        }

        private async void PinButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedEntry != null && sender is Button button && button.Content != null)
            {
                bool isPinned = button.Content.ToString() == "Unpin";
                await _viewModel.PinEntryAsync(_viewModel.SelectedEntry, !isPinned);
                button.Content = isPinned ? "Pin" : "Unpin";
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedEntry != null)
            {
                var result = MessageBox.Show("Are you sure you want to delete this clipboard entry?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _viewModel.DeleteEntryAsync(_viewModel.SelectedEntry);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // If minimize to tray is enabled and we're not forcing a close, hide the window instead of closing it
            if (_closeToTray && !_forceClose)
            {
                // Only hide the window if the tray icon is properly initialized
                if (IsTrayIconInitialized && _trayIcon != null)
                {
                    Console.WriteLine("Hiding window instead of closing (tray icon is initialized)");
                    Logger.Instance.Info("Hiding window instead of closing (tray icon is initialized)");
                    e.Cancel = true;
                    Hide();
                    return;
                }
                else
                {
                    // If the tray icon is not initialized, show a warning and proceed with closing
                    Console.WriteLine("Cannot hide to tray: tray icon is not initialized");
                    Logger.Instance.Warning("Cannot hide to tray: tray icon is not initialized");
                    MessageBox.Show(
                        "The system tray icon is not properly initialized. The application will exit instead of minimizing to tray.",
                        "Tray Icon Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            Console.WriteLine("Application is closing");
            Logger.Instance.Info("Application is closing");
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Clean up resources
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Clean up tray icon
            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            // Clean up hotkey
            try
            {
                NHotkey.Wpf.HotkeyManager.Current.Remove("ShowClipperQuickPicker");
            }
            catch { /* Ignore errors during cleanup */ }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _closeToTray)
            {
                // Only hide the window if the tray icon is properly initialized
                if (IsTrayIconInitialized && _trayIcon != null)
                {
                    Console.WriteLine("Hiding window instead of minimizing (tray icon is initialized)");
                    Logger.Instance.Info("Hiding window instead of minimizing (tray icon is initialized)");

                    // Hide window instead of minimizing if we're using the tray
                    Hide();
                    WindowState = WindowState.Normal; // Reset state for next time
                }
                else
                {
                    // If the tray icon is not initialized, show a warning and keep the window minimized
                    Console.WriteLine("Cannot hide to tray: tray icon is not initialized");
                    Logger.Instance.Warning("Cannot hide to tray: tray icon is not initialized");

                    // Show a notification to the user
                    MessageBox.Show(
                        "The system tray icon is not properly initialized. The window will be minimized instead of hidden to tray.",
                        "Tray Icon Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Keep the window minimized but visible
                    WindowState = WindowState.Minimized;
                }
            }

            base.OnStateChanged(e);
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Open settings dialog
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;

            if (settingsWindow.ShowDialog() == true)
            {
                // Update _closeToTray from settings after dialog is closed
                _closeToTray = Properties.Settings.Default.MinimizeToTray;
            }
        }



        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Set flag to force close and exit the application
            _forceClose = true;
            Close();
        }

        private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            // Toggle window visibility on tray icon click
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            // Get the current version
            var version = UpdateChecker.Instance.CurrentVersion;

            // Show the about dialog
            MessageBox.Show(
                $"ClipSage - Advanced Clipboard Manager\nVersion {version}\n\nDeveloped by Oguzhan Filizlibay\n\nGitHub: https://github.com/oguzhanf/clipsage\nFree and Open Source Software",
                $"About ClipSage v{version}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MonitoringToggle_Click(object sender, RoutedEventArgs e)
        {
            // Toggle clipboard monitoring
            _viewModel.ToggleMonitoring();

            // Update the toggle button state to match the actual monitoring state
            if (MonitoringToggle.IsChecked != _viewModel.IsMonitoring)
            {
                MonitoringToggle.IsChecked = _viewModel.IsMonitoring;
            }
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            // Check for updates and show results
            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesSilentlyAsync()
        {
            try
            {
                // Only check if it's been more than 24 hours since the last check
                var lastCheck = Properties.Settings.Default.LastUpdateCheck;
                if (lastCheck > new DateTime(1900, 1, 1) &&
                    (DateTime.Now - lastCheck).TotalHours < 24)
                {
                    _viewModel.UpdateStatusText = "Update check: Last check was within 24 hours";
                    return;
                }

                // Update status to show we're checking
                _viewModel.UpdateStatusText = "Update check: Starting...";

                // Check for updates
                var updater = PortableUpdater.Instance;
                var updateInfo = await updater.CheckForUpdateAsync(
                    progress =>
                    {
                        // Update the status bar with progress
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _viewModel.UpdateStatusText = $"Update check: {progress}";
                        });
                    });

                // Update the last check time
                Properties.Settings.Default.LastUpdateCheck = DateTime.Now;
                Properties.Settings.Default.Save();

                // If an update is available, show a notification
                if (updateInfo != null && updateInfo.IsUpdateAvailable)
                {
                    // Update status bar
                    _viewModel.UpdateStatusText = $"Update available: v{updateInfo.VersionString}";

                    // Show a balloon tip
                    if (_trayIcon != null)
                    {
                        _trayIcon.ShowBalloonTip(
                            "Update Available",
                            $"ClipSage v{updateInfo.VersionString} is available. Click here to update.",
                            BalloonIcon.Info);

                        // Handle balloon click to open the update dialog
                        _trayIcon.TrayBalloonTipClicked += (s, args) =>
                        {
                            Show();
                            WindowState = WindowState.Normal;
                            Activate();

                            var updateDialog = new PortableUpdateDialog();
                            updateDialog.Owner = this;
                            updateDialog.ShowDialog();
                        };
                    }

                    // If auto-install is enabled, show the update dialog
                    if (Properties.Settings.Default.AutoInstallUpdates)
                    {
                        var updateDialog = new PortableUpdateDialog();
                        updateDialog.Owner = this;
                        updateDialog.ShowDialog();
                    }
                }
                else
                {
                    // No update available
                    _viewModel.UpdateStatusText = "Update check: You have the latest version";
                }
            }
            catch (Exception ex)
            {
                // Log the error and update status bar
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                _viewModel.UpdateStatusText = $"Update check failed: {ex.Message}";
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            await Task.Yield(); // Make this method actually async
            try
            {
                // Update status to show we're checking
                _viewModel.UpdateStatusText = "Update check: Starting...";

                // Show the portable update dialog
                var updateDialog = new PortableUpdateDialog();
                updateDialog.Owner = this;
                updateDialog.ShowDialog();

                // Update the status bar after the dialog is closed
                _viewModel.UpdateStatusText = "Update check completed";
            }
            catch (Exception ex)
            {
                // Log the error and update status bar
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                _viewModel.UpdateStatusText = $"Update check failed: {ex.Message}";

                // Show error to the user
                MessageBox.Show(
                    $"Error checking for updates: {ex.Message}",
                    "Update Check Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
        {
            try
            {
                // Create a progress dialog if this is a manual update
                ProgressDialog? progressDialog = null;
                if (IsVisible)
                {
                    progressDialog = new ProgressDialog
                    {
                        Owner = this,
                        Title = "Downloading Update",
                        Message = $"Downloading ClipSage v{updateInfo.VersionString}...",
                        IsIndeterminate = false
                    };
                }

                // Create a progress reporter
                var progress = new Progress<double>(value =>
                {
                    // Update the progress dialog if it exists
                    if (progressDialog != null)
                    {
                        progressDialog.Progress = value;
                        progressDialog.Message = $"Downloading ClipSage v{updateInfo.VersionString}... {value:P0}";
                    }

                    // Always update the status bar
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _viewModel.UpdateStatusText = $"Downloading update: {value:P0}";
                    });
                });

                // Start the download in the background
                var updateChecker = UpdateChecker.Instance;

                // Show the progress dialog if it exists
                if (progressDialog != null)
                {
                    progressDialog.Show();
                }

                // Start the download
                var downloadTask = updateChecker.DownloadUpdateAsync(updateInfo, progress);

                // Wait for the download to complete
                var downloadResult = await downloadTask;

                // Close the progress dialog if it exists
                if (progressDialog != null)
                {
                    progressDialog.Close();
                }

                // Check if the download was successful
                if (!downloadResult.IsSuccess)
                {
                    _viewModel.UpdateStatusText = $"Download failed: {downloadResult.ErrorMessage}";

                    if (IsVisible)
                    {
                        MessageBox.Show(
                            $"Failed to download the update.\n\nError: {downloadResult.ErrorMessage}\n\n{downloadResult.DetailedError}",
                            "Download Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    return;
                }

                var installerPath = downloadResult.FilePath;

                // Update status
                _viewModel.UpdateStatusText = "Download complete. Ready to install.";

                // If this is a silent update and auto-install is enabled, install without prompting
                if (!IsVisible && Properties.Settings.Default.AutoInstallUpdates)
                {
                    // Install the update
                    if (installerPath != null && updateChecker.InstallUpdate(installerPath))
                    {
                        // Set flag to force close and exit the application
                        _forceClose = true;
                        Close();
                    }
                    return;
                }

                // Ask the user if they want to install now
                var result = MessageBox.Show(
                    "The update has been downloaded. Do you want to install it now?\n\nThe application will close during installation.",
                    "Install Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Install the update
                    if (installerPath != null && updateChecker.InstallUpdate(installerPath))
                    {
                        // Set flag to force close and exit the application
                        _forceClose = true;
                        Close();
                    }
                    else
                    {
                        _viewModel.UpdateStatusText = "Installation failed";

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
                _viewModel.UpdateStatusText = $"Download error: {ex.Message}";

                if (IsVisible)
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

    public class ImagePreviewViewModel
    {
        private readonly ClipboardEntryViewModel _entryViewModel;

        public BitmapImage ImageSource { get; }

        // Properties needed for the details card
        public Guid Id => _entryViewModel.Id;
        public DateTime Timestamp => _entryViewModel.Timestamp;
        public ClipSage.Core.Storage.ClipboardDataType DataType => _entryViewModel.DataType;
        public string? ComputerName => _entryViewModel.ComputerName;
        public string FormattedComputerName => _entryViewModel.FormattedComputerName;
        public string ItemAge => _entryViewModel.ItemAge;
        public string SourceComputer => _entryViewModel.SourceComputer;
        public string ContentTypeDescription => _entryViewModel.ContentTypeDescription;

        public ImagePreviewViewModel(ClipboardEntryViewModel entryViewModel)
        {
            _entryViewModel = entryViewModel;

            if (entryViewModel.ImageBytes == null)
                throw new ArgumentException("Entry does not contain image data");

            using var stream = new MemoryStream(entryViewModel.ImageBytes);
            ImageSource = new BitmapImage();
            ImageSource.BeginInit();
            ImageSource.StreamSource = stream;
            ImageSource.CacheOption = BitmapCacheOption.OnLoad;
            ImageSource.EndInit();
            ImageSource.Freeze(); // Make it thread-safe
        }
    }
}