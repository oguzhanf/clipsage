using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ClipSage.App.Properties;

namespace ClipSage.App
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        // General settings
        private bool _startWithWindows;
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                if (_startWithWindows != value)
                {
                    _startWithWindows = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _startMinimized;
        public bool StartMinimized
        {
            get => _startMinimized;
            set
            {
                if (_startMinimized != value)
                {
                    _startMinimized = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _minimizeToTray;
        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                if (_minimizeToTray != value)
                {
                    _minimizeToTray = value;
                    OnPropertyChanged();
                }
            }
        }

        // Clipboard history settings
        private int _maxHistorySize;
        public int MaxHistorySize
        {
            get => _maxHistorySize;
            set
            {
                if (_maxHistorySize != value)
                {
                    _maxHistorySize = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _ignoreDuplicates;
        public bool IgnoreDuplicates
        {
            get => _ignoreDuplicates;
            set
            {
                if (_ignoreDuplicates != value)
                {
                    _ignoreDuplicates = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _truncateLargeText;
        public bool TruncateLargeText
        {
            get => _truncateLargeText;
            set
            {
                if (_truncateLargeText != value)
                {
                    _truncateLargeText = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _maxTextLength;
        public int MaxTextLength
        {
            get => _maxTextLength;
            set
            {
                if (_maxTextLength != value)
                {
                    _maxTextLength = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _ignoreLargeImages;
        public bool IgnoreLargeImages
        {
            get => _ignoreLargeImages;
            set
            {
                if (_ignoreLargeImages != value)
                {
                    _ignoreLargeImages = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _maxImageSize;
        public int MaxImageSize
        {
            get => _maxImageSize;
            set
            {
                if (_maxImageSize != value)
                {
                    _maxImageSize = value;
                    OnPropertyChanged();
                }
            }
        }

        // Caching settings
        private string _cachingFolder = string.Empty;
        public string CachingFolder
        {
            get => _cachingFolder;
            set
            {
                if (_cachingFolder != value)
                {
                    _cachingFolder = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        private bool _cachingFolderConfigured;
        public bool CachingFolderConfigured
        {
            get => _cachingFolderConfigured;
            set
            {
                if (_cachingFolderConfigured != value)
                {
                    _cachingFolderConfigured = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _cacheFiles;
        public bool CacheFiles
        {
            get => _cacheFiles;
            set
            {
                if (_cacheFiles != value)
                {
                    _cacheFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _maxFileCacheSize;
        public int MaxFileCacheSize
        {
            get => _maxFileCacheSize;
            set
            {
                if (_maxFileCacheSize != value)
                {
                    _maxFileCacheSize = value;
                    OnPropertyChanged();
                }
            }
        }

        // Update settings
        private bool _checkForUpdates;
        public bool CheckForUpdates
        {
            get => _checkForUpdates;
            set
            {
                if (_checkForUpdates != value)
                {
                    _checkForUpdates = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _autoInstallUpdates;
        public bool AutoInstallUpdates
        {
            get => _autoInstallUpdates;
            set
            {
                if (_autoInstallUpdates != value)
                {
                    _autoInstallUpdates = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime _lastUpdateCheck;
        public DateTime LastUpdateCheck
        {
            get => _lastUpdateCheck;
            set
            {
                if (_lastUpdateCheck != value)
                {
                    _lastUpdateCheck = value;
                    OnPropertyChanged();
                }
            }
        }

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load settings from application settings
            // For now, we'll use default values
            StartWithWindows = Properties.Settings.Default.StartWithWindows;
            StartMinimized = Properties.Settings.Default.StartMinimized;
            MinimizeToTray = Properties.Settings.Default.MinimizeToTray;
            MaxHistorySize = Properties.Settings.Default.MaxHistorySize;
            IgnoreDuplicates = Properties.Settings.Default.IgnoreDuplicates;
            TruncateLargeText = Properties.Settings.Default.TruncateLargeText;
            MaxTextLength = Properties.Settings.Default.MaxTextLength;
            IgnoreLargeImages = Properties.Settings.Default.IgnoreLargeImages;
            MaxImageSize = Properties.Settings.Default.MaxImageSize;
            CachingFolder = Properties.Settings.Default.CachingFolder;
            CachingFolderConfigured = Properties.Settings.Default.CachingFolderConfigured;
            CacheFiles = Properties.Settings.Default.CacheFiles;
            MaxFileCacheSize = Properties.Settings.Default.MaxFileCacheSize;
            CheckForUpdates = Properties.Settings.Default.CheckForUpdates;
            AutoInstallUpdates = Properties.Settings.Default.AutoInstallUpdates;
            LastUpdateCheck = Properties.Settings.Default.LastUpdateCheck;
        }

        public void SaveSettings()
        {
            // Save settings to application settings
            Properties.Settings.Default.StartWithWindows = StartWithWindows;
            Properties.Settings.Default.StartMinimized = StartMinimized;
            Properties.Settings.Default.MinimizeToTray = MinimizeToTray;
            Properties.Settings.Default.MaxHistorySize = MaxHistorySize;
            Properties.Settings.Default.IgnoreDuplicates = IgnoreDuplicates;
            Properties.Settings.Default.TruncateLargeText = TruncateLargeText;
            Properties.Settings.Default.MaxTextLength = MaxTextLength;
            Properties.Settings.Default.IgnoreLargeImages = IgnoreLargeImages;
            Properties.Settings.Default.MaxImageSize = MaxImageSize;
            Properties.Settings.Default.CachingFolder = CachingFolder;
            Properties.Settings.Default.CachingFolderConfigured = CachingFolderConfigured;
            Properties.Settings.Default.CacheFiles = CacheFiles;
            Properties.Settings.Default.MaxFileCacheSize = MaxFileCacheSize;
            Properties.Settings.Default.CheckForUpdates = CheckForUpdates;
            Properties.Settings.Default.AutoInstallUpdates = AutoInstallUpdates;
            Properties.Settings.Default.LastUpdateCheck = LastUpdateCheck;

            Properties.Settings.Default.Save();

            // Update startup registry key if needed
            UpdateStartupRegistry();
        }

        private void UpdateStartupRegistry()
        {
            try
            {
                // Get the path to the executable
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var mainModule = process.MainModule;

                if (mainModule == null)
                {
                    MessageBox.Show("Failed to get executable path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string executablePath = mainModule.FileName;

                // Create or delete the registry key for startup
                Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (key == null)
                {
                    MessageBox.Show("Failed to open registry key", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (StartWithWindows)
                {
                    key.SetValue("ClipSage", executablePath);
                }
                else
                {
                    if (key.GetValue("ClipSage") != null)
                    {
                        key.DeleteValue("ClipSage");
                    }
                    // Also clean up old registry key if it exists
                    if (key.GetValue("ClipperMVP") != null)
                    {
                        key.DeleteValue("ClipperMVP");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update startup settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
        }
    }
}
