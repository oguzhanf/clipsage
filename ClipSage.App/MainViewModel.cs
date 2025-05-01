using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;
using ClipSage.App.Properties;
using ClipSage.Core.Logging;

namespace ClipSage.App
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ClipSage.Core.ClipboardService _clipboardService;
        private readonly ClipSage.Core.Storage.HistoryStore _historyStore;

        // Store the last clipboard content for duplicate detection
        private string? _lastTextContent = null;
        private byte[]? _lastImageContent = null;
        private string[]? _lastFilePaths = null;

        private ObservableCollection<ClipboardEntryViewModel> _allEntries = new();
        public ObservableCollection<ClipboardEntryViewModel> ClipboardEntries { get; set; } = new();

        private ClipboardEntryViewModel? _selectedEntry;
        public ClipboardEntryViewModel? SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                _selectedEntry = value;
                OnPropertyChanged();
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterEntries();
            }
        }

        private System.Threading.Timer _cleanupTimer;

        public MainViewModel()
        {
            _clipboardService = ClipSage.Core.ClipboardService.Instance;

            // Use the configured cache folder if available
            string cacheFolderPath = Properties.Settings.Default.CachingFolder;
            if (Properties.Settings.Default.CachingFolderConfigured && !string.IsNullOrEmpty(cacheFolderPath))
            {
                _historyStore = new ClipSage.Core.Storage.HistoryStore(cacheFolderPath);
            }
            else
            {
                _historyStore = new ClipSage.Core.Storage.HistoryStore();
            }

            // Subscribe to clipboard changes
            _clipboardService.ClipboardChanged += OnClipboardChanged;

            // Clean up duplicates and load recent entries
            CleanupAndLoadEntriesAsync();

            // Set up a timer to periodically clean up duplicates (every 30 minutes)
            _cleanupTimer = new System.Threading.Timer(
                async _ => await CleanupDuplicatesAsync(),
                null,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(30));
        }

        private async void LoadRecentEntriesAsync()
        {
            try
            {
                // The HistoryStore.GetRecentAsync method now filters out duplicates
                var entries = await _historyStore.GetRecentAsync(50);
                _allEntries.Clear();
                ClipboardEntries.Clear();

                foreach (var entry in entries)
                {
                    var viewModel = new ClipboardEntryViewModel(entry);
                    _allEntries.Add(viewModel);
                    ClipboardEntries.Add(viewModel);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading clipboard history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Instance.Error("Error loading clipboard history", ex);
            }
        }

        private void FilterEntries()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Show all entries
                ClipboardEntries.Clear();
                foreach (var entry in _allEntries)
                {
                    ClipboardEntries.Add(entry);
                }
                return;
            }

            // Filter entries based on search text
            var filteredEntries = _allEntries.Where(e =>
                e.DataType == ClipSage.Core.Storage.ClipboardDataType.Text &&
                e.PlainText != null &&
                e.PlainText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            ClipboardEntries.Clear();
            foreach (var entry in filteredEntries)
            {
                ClipboardEntries.Add(entry);
            }
        }

        private async void OnClipboardChanged(object? sender, ClipSage.Core.ClipboardEntry entry)
        {
            try
            {
                // Apply input sanitization based on settings
                if (!ShouldProcessClipboardEntry(entry))
                {
                    return;
                }

                // Sanitize the entry if needed
                SanitizeClipboardEntry(entry);

                // Save to database
                // Convert Core.ClipboardEntry to Storage.ClipboardEntry
                var storageEntry = new ClipSage.Core.Storage.ClipboardEntry
                {
                    Id = entry.Id,
                    Timestamp = entry.Timestamp,
                    DataType = (ClipSage.Core.Storage.ClipboardDataType)entry.DataType,
                    PlainText = entry.PlainText,
                    ImageBytes = entry.ImageBytes,
                    FilePaths = entry.FilePaths
                };

                // Check for consecutive duplicates
                if (Properties.Settings.Default.IgnoreDuplicates && IsConsecutiveDuplicate(storageEntry))
                {
                    Console.WriteLine("Ignoring consecutive duplicate clipboard entry");
                    Logger.Instance.Debug("Ignoring consecutive duplicate clipboard entry");
                    return;
                }

                await _historyStore.AddAsync(storageEntry);

                // Handle file caching if needed
                if (storageEntry.DataType == ClipSage.Core.Storage.ClipboardDataType.FilePaths &&
                    storageEntry.FilePaths != null &&
                    storageEntry.FilePaths.Length > 0 &&
                    Properties.Settings.Default.CacheFiles)
                {
                    await CacheFilesAsync(storageEntry);
                }

                // Add to UI collection (on UI thread)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var viewModel = new ClipboardEntryViewModel(storageEntry);
                    _allEntries.Insert(0, viewModel);

                    // Only add to visible collection if it matches the current filter
                    if (string.IsNullOrWhiteSpace(SearchText) ||
                        (storageEntry.DataType == ClipSage.Core.Storage.ClipboardDataType.Text &&
                         storageEntry.PlainText != null &&
                         storageEntry.PlainText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                    {
                        ClipboardEntries.Insert(0, viewModel);
                    }

                    // Show notification
                    ShowClipboardNotification(storageEntry);

                    // Trim history if needed
                    TrimHistoryIfNeeded();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing clipboard change: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Instance.Error("Error processing clipboard change", ex);
            }
        }

        private bool ShouldProcessClipboardEntry(ClipSage.Core.ClipboardEntry entry)
        {
            // Check if we should ignore large text
            if (entry.DataType == ClipSage.Core.ClipboardDataType.Text &&
                Properties.Settings.Default.TruncateLargeText)
            {
                if (entry.PlainText != null &&
                    entry.PlainText.Length > Properties.Settings.Default.MaxTextLength * 1024)
                {
                    // Text is too large, but we'll truncate it rather than ignore it
                    return true;
                }
            }

            // Check if we should ignore large images
            if (entry.DataType == ClipSage.Core.ClipboardDataType.Image &&
                Properties.Settings.Default.IgnoreLargeImages)
            {
                if (entry.ImageBytes != null &&
                    entry.ImageBytes.Length > Properties.Settings.Default.MaxImageSize * 1024 * 1024)
                {
                    // Image is too large, ignore it
                    return false;
                }
            }

            return true;
        }

        private void SanitizeClipboardEntry(ClipSage.Core.ClipboardEntry entry)
        {
            // Truncate large text if needed
            if (entry.DataType == ClipSage.Core.ClipboardDataType.Text &&
                Properties.Settings.Default.TruncateLargeText)
            {
                if (entry.PlainText != null)
                {
                    int maxLength = Properties.Settings.Default.MaxTextLength * 1024;
                    if (entry.PlainText.Length > maxLength)
                    {
                        entry.PlainText = entry.PlainText.Substring(0, maxLength) +
                            $"\n\n[Text truncated at {maxLength / 1024}KB]";
                    }
                }
            }
        }

        private bool IsConsecutiveDuplicate(ClipSage.Core.Storage.ClipboardEntry entry)
        {
            // Check if this entry is a duplicate of the most recent entry
            bool isDuplicate = false;

            switch (entry.DataType)
            {
                case ClipSage.Core.Storage.ClipboardDataType.Text:
                    // For text entries, compare with the last text content
                    if (!string.IsNullOrEmpty(entry.PlainText) && entry.PlainText == _lastTextContent)
                    {
                        isDuplicate = true;
                    }
                    // Update the last text content
                    _lastTextContent = entry.PlainText;
                    break;

                case ClipSage.Core.Storage.ClipboardDataType.Image:
                    // For images, compare with the last image content
                    if (entry.ImageBytes != null && _lastImageContent != null &&
                        entry.ImageBytes.Length == _lastImageContent.Length)
                    {
                        bool imagesMatch = true;
                        for (int i = 0; i < entry.ImageBytes.Length; i++)
                        {
                            if (entry.ImageBytes[i] != _lastImageContent[i])
                            {
                                imagesMatch = false;
                                break;
                            }
                        }

                        if (imagesMatch)
                        {
                            isDuplicate = true;
                        }
                    }
                    // Update the last image content
                    _lastImageContent = entry.ImageBytes;
                    break;

                case ClipSage.Core.Storage.ClipboardDataType.FilePaths:
                    // For file paths, compare with the last file paths
                    if (entry.FilePaths != null && _lastFilePaths != null &&
                        entry.FilePaths.Length == _lastFilePaths.Length)
                    {
                        // Sort both arrays for consistent comparison
                        var sortedNewPaths = entry.FilePaths.OrderBy(p => p).ToArray();
                        var sortedLastPaths = _lastFilePaths.OrderBy(p => p).ToArray();

                        bool pathsMatch = true;
                        for (int i = 0; i < sortedNewPaths.Length; i++)
                        {
                            if (sortedNewPaths[i] != sortedLastPaths[i])
                            {
                                pathsMatch = false;
                                break;
                            }
                        }

                        if (pathsMatch)
                        {
                            isDuplicate = true;
                        }
                    }
                    // Update the last file paths
                    _lastFilePaths = entry.FilePaths;
                    break;
            }

            return isDuplicate;
        }

        private void TrimHistoryIfNeeded()
        {
            // Check if we need to trim the history
            int maxSize = Properties.Settings.Default.MaxHistorySize;
            if (_allEntries.Count > maxSize)
            {
                // Remove oldest entries
                while (_allEntries.Count > maxSize)
                {
                    var lastEntry = _allEntries.Last();
                    _allEntries.Remove(lastEntry);

                    if (ClipboardEntries.Contains(lastEntry))
                    {
                        ClipboardEntries.Remove(lastEntry);
                    }
                }
            }
        }

        private void ShowClipboardNotification(ClipSage.Core.Storage.ClipboardEntry entry)
        {
            try
            {
                // Get the main window
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    // Access the tray icon through reflection since it's a private field
                    var trayIconField = mainWindow.GetType().GetField("_trayIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var trayIcon = trayIconField?.GetValue(mainWindow) as Hardcodet.Wpf.TaskbarNotification.TaskbarIcon;

                    if (trayIcon != null)
                    {
                        string title = "Clipboard Captured";
                        string message;

                        switch (entry.DataType)
                        {
                            case ClipSage.Core.Storage.ClipboardDataType.Text:
                                message = GetTruncatedText(entry.PlainText, 40);
                                break;
                            case ClipSage.Core.Storage.ClipboardDataType.Image:
                                message = "[Image copied]";
                                break;
                            case ClipSage.Core.Storage.ClipboardDataType.FilePaths:
                                if (entry.FilePaths != null && entry.FilePaths.Length > 0)
                                {
                                    message = entry.FilePaths.Length == 1
                                        ? $"[File copied: {Path.GetFileName(entry.FilePaths[0])}]"
                                        : $"[{entry.FilePaths.Length} files copied]";
                                }
                                else
                                {
                                    message = "[Files copied]";
                                }
                                break;
                            default:
                                message = "[Clipboard content captured]";
                                break;
                        }

                        trayIcon.ShowBalloonTip(title, message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error showing notification: {ex.Message}");
                Logger.Instance.Error("Error showing notification", ex);
            }
        }

        private string GetTruncatedText(string? text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
        }

        public Task CopyToClipboardAsync(ClipboardEntryViewModel entry)
        {
            try
            {
                // Update our last content trackers before copying to prevent duplicates
                switch (entry.DataType)
                {
                    case ClipSage.Core.Storage.ClipboardDataType.Text:
                        _lastTextContent = entry.PlainText;
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.Image:
                        _lastImageContent = entry.ImageBytes;
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.FilePaths:
                        _lastFilePaths = entry.FilePaths;
                        break;
                }

                if (entry.DataType == ClipSage.Core.Storage.ClipboardDataType.Text)
                {
                    Clipboard.SetText(entry.PlainText ?? string.Empty);
                }
                else if (entry.DataType == ClipSage.Core.Storage.ClipboardDataType.Image && entry.ImageBytes != null)
                {
                    using var stream = new System.IO.MemoryStream(entry.ImageBytes);
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    Clipboard.SetImage(bitmap);
                }
                else if (entry.DataType == ClipSage.Core.Storage.ClipboardDataType.FilePaths && entry.FilePaths != null && entry.FilePaths.Length > 0)
                {
                    // Create a StringCollection for the file paths
                    var fileCollection = new System.Collections.Specialized.StringCollection();

                    // Check if all files exist
                    bool allFilesExist = true;
                    foreach (string path in entry.FilePaths)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            fileCollection.Add(path);
                        }
                        else
                        {
                            allFilesExist = false;

                            // If we're caching files, check if we have a cached copy
                            if (Properties.Settings.Default.CacheFiles)
                            {
                                string fileName = System.IO.Path.GetFileName(path);
                                string cachedFilePath = System.IO.Path.Combine(
                                    Properties.Settings.Default.CachingFolder,
                                    "Files",
                                    entry.Id.ToString(),
                                    fileName);

                                if (System.IO.File.Exists(cachedFilePath))
                                {
                                    fileCollection.Add(cachedFilePath);
                                }
                            }
                        }
                    }

                    if (fileCollection.Count > 0)
                    {
                        Clipboard.SetFileDropList(fileCollection);
                    }
                    else
                    {
                        MessageBox.Show("None of the original files exist and no cached copies were found.",
                            "Files Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    if (!allFilesExist && fileCollection.Count > 0)
                    {
                        MessageBox.Show("Some of the original files could not be found. Available files have been copied to the clipboard.",
                            "Some Files Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Instance.Error("Error copying to clipboard", ex);
            }

            // Return a completed task since this method doesn't actually perform any async operations
            return Task.CompletedTask;
        }

        public async Task DeleteEntryAsync(ClipboardEntryViewModel entry)
        {
            try
            {
                await _historyStore.DeleteAsync(entry.Id);
                ClipboardEntries.Remove(entry);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Instance.Error("Error deleting entry", ex);
            }
        }

        public async Task PinEntryAsync(ClipboardEntryViewModel entry, bool isPinned)
        {
            try
            {
                await _historyStore.PinAsync(entry.Id, isPinned);
                // Refresh the list
                LoadRecentEntriesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pinning entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Instance.Error("Error pinning entry", ex);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task CacheFilesAsync(ClipSage.Core.Storage.ClipboardEntry entry)
        {
            try
            {
                // Check if FilePaths is null
                if (entry.FilePaths == null)
                    return;

                // Calculate total size of files
                long totalSize = 0;
                foreach (string path in entry.FilePaths)
                {
                    if (File.Exists(path))
                    {
                        FileInfo fileInfo = new FileInfo(path);
                        totalSize += fileInfo.Length;
                    }
                }

                // Convert max size from MB to bytes
                long maxSizeBytes = (long)Properties.Settings.Default.MaxFileCacheSize * 1024 * 1024;

                // Only cache if total size is less than the max
                if (totalSize <= maxSizeBytes)
                {
                    // Create a folder for this entry in the cache folder
                    string cacheFolder = Properties.Settings.Default.CachingFolder;
                    string filesCacheFolder = Path.Combine(cacheFolder, "Files");
                    string entryFolder = Path.Combine(filesCacheFolder, entry.Id.ToString());

                    Directory.CreateDirectory(filesCacheFolder);
                    Directory.CreateDirectory(entryFolder);

                    // Copy each file
                    if (entry.FilePaths != null)
                    {
                        foreach (string path in entry.FilePaths)
                        {
                            if (File.Exists(path))
                            {
                                string fileName = Path.GetFileName(path);
                                string destPath = Path.Combine(entryFolder, fileName);
                                await Task.Run(() => File.Copy(path, destPath, true));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching files: {ex.Message}");
                Logger.Instance.Error("Error caching files", ex);
                // We don't want to throw the exception here as it would disrupt the application flow
                // Just log it and continue
            }
        }

        private async void CleanupAndLoadEntriesAsync()
        {
            try
            {
                // First clean up duplicates
                await CleanupDuplicatesAsync();

                // Then load entries
                await Task.Run(() => LoadRecentEntriesAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup and load: {ex.Message}");
                Logger.Instance.Error("Error during cleanup and load", ex);
            }
        }

        private async Task CleanupDuplicatesAsync()
        {
            try
            {
                // Clean up duplicates in the database
                int removedCount = await _historyStore.CleanupDuplicatesAsync();

                if (removedCount > 0)
                {
                    Console.WriteLine($"Removed {removedCount} duplicate entries from the database");
                    Logger.Instance.Info($"Removed {removedCount} duplicate entries from the database");

                    // Reload the entries if we removed any duplicates
                    await Task.Run(() => LoadRecentEntriesAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up duplicates: {ex.Message}");
                Logger.Instance.Error("Error cleaning up duplicates", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                // Log disposal
                Logger.Instance.Info("Disposing MainViewModel");

                // Unsubscribe from events
                _clipboardService.ClipboardChanged -= OnClipboardChanged;

                // Dispose the timer
                _cleanupTimer?.Dispose();

                // Reset content trackers
                _lastTextContent = null;
                _lastImageContent = null;
                _lastFilePaths = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing MainViewModel: {ex.Message}");
                Logger.Instance.Error("Error disposing MainViewModel", ex);
            }
        }
    }
}
