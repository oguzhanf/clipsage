using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using MahApps.Metro.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using ClipSage.Core.Storage;
using ClipSage.Core.Update;

namespace ClipSage.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private readonly MainViewModel _viewModel;
        private bool _closeToTray = true;
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

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
                // Use Task.Run to avoid blocking the UI thread
                // We don't need to await this as it's a background operation
                _ = Task.Run(async () => await CheckForUpdatesAsync());
            }
        }

        private void SetupTrayIcon()
        {
            // Create the tray icon programmatically
            _trayIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon();
            _trayIcon.ToolTipText = "ClipSage";
            _trayIcon.TrayLeftMouseDown += TrayIcon_TrayLeftMouseDown;
            _trayIcon.ContextMenu = Resources["TrayMenu"] as ContextMenu;

            try
            {
                // Try to load the icon from resources
                var iconUri = new Uri("pack://application:,,,/Resources/clipboard.ico", UriKind.Absolute);
                _trayIcon.Icon = new System.Drawing.Icon(Application.GetResourceStream(iconUri).Stream);
            }
            catch (Exception ex)
            {
                // If loading fails, use a system icon as fallback
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                Console.WriteLine($"Failed to load tray icon: {ex.Message}");
            }

            // Show balloon tip on startup
            _trayIcon.ShowBalloonTip("ClipSage", "ClipSage is running in the background", BalloonIcon.Info);
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
                    var imageViewModel = new ImagePreviewViewModel(_viewModel.SelectedEntry.ImageBytes);
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
                // Hide window instead of minimizing if we're using the tray
                Hide();
                WindowState = WindowState.Normal; // Reset state for next time
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

            settingsWindow.ShowDialog();
        }



        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Exit the application
            Application.Current.Shutdown();
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
                "About ClipSage",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            // Open the update check dialog
            var updateCheckDialog = new UpdateCheckDialog();
            updateCheckDialog.Owner = this;
            updateCheckDialog.ShowDialog();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Only check if it's been more than 24 hours since the last check
                var lastCheck = Properties.Settings.Default.LastUpdateCheck;
                if (lastCheck > new DateTime(1900, 1, 1) &&
                    (DateTime.Now - lastCheck).TotalHours < 24)
                {
                    return;
                }

                // Check for updates
                var updateChecker = UpdateChecker.Instance;
                var updateInfo = await updateChecker.CheckForUpdateAsync(
                    progress => Console.WriteLine($"Update check: {progress}"));

                // Update the last check time
                Properties.Settings.Default.LastUpdateCheck = DateTime.Now;
                Properties.Settings.Default.Save();

                // If an update is available, show a notification
                if (updateInfo != null && updateInfo.IsUpdateAvailable)
                {
                    // Show a balloon tip
                    if (_trayIcon != null)
                    {
                        _trayIcon.ShowBalloonTip(
                            "Update Available",
                            $"ClipSage v{updateInfo.VersionString} is available. Click here to update.",
                            BalloonIcon.Info);

                        // Handle balloon click to open the update check dialog
                        _trayIcon.TrayBalloonTipClicked += (s, args) =>
                        {
                            Show();
                            WindowState = WindowState.Normal;
                            Activate();

                            var updateCheckDialog = new UpdateCheckDialog();
                            updateCheckDialog.Owner = this;
                            updateCheckDialog.ShowDialog();
                        };
                    }

                    // If auto-install is enabled, download and install the update
                    if (Properties.Settings.Default.AutoInstallUpdates)
                    {
                        await DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't show it to the user
                Console.WriteLine($"Error checking for updates: {ex.Message}");
            }
        }

        private async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
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
                var installerPath = await downloadTask;

                // Close the progress dialog
                progressDialog.Close();

                // Check if the download was successful
                if (string.IsNullOrEmpty(installerPath))
                {
                    MessageBox.Show(
                        "Failed to download the update. Please try again later.",
                        "Download Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
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

    public class ImagePreviewViewModel
    {
        public BitmapImage ImageSource { get; }

        public ImagePreviewViewModel(byte[] imageBytes)
        {
            using var stream = new MemoryStream(imageBytes);
            ImageSource = new BitmapImage();
            ImageSource.BeginInit();
            ImageSource.StreamSource = stream;
            ImageSource.CacheOption = BitmapCacheOption.OnLoad;
            ImageSource.EndInit();
            ImageSource.Freeze(); // Make it thread-safe
        }
    }
}