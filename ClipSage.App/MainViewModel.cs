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
using ClipSage.Core.Storage;

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

        private ClipboardDataType? _currentFilterType = null;
        public ClipboardDataType? CurrentFilterType
        {
            get => _currentFilterType;
            set
            {
                _currentFilterType = value;
                OnPropertyChanged();
                FilterEntries();
            }
        }

        private bool _isMonitoring;
        public bool IsMonitoring
        {
            get => _isMonitoring;
            private set
            {
                if (_isMonitoring != value)
                {
                    _isMonitoring = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MonitoringStatusText));
                }
            }
        }

        private string _updateStatusText = string.Empty;
        public string UpdateStatusText
        {
            get => _updateStatusText;
            set
            {
                if (_updateStatusText != value)
                {
                    _updateStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _eventStatusText = string.Empty;
        public string EventStatusText
        {
            get => _eventStatusText;
            set
            {
                if (_eventStatusText != value)
                {
                    _eventStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MonitoringStatusText => IsMonitoring ? "Monitoring: Active" : "Monitoring: Paused";

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

            // Initialize monitoring state
            IsMonitoring = _clipboardService.IsMonitoring;

            // Clean up duplicates and load recent entries
            CleanupAndLoadEntriesAsync();

            // Set up a timer to periodically clean up duplicates (every 30 minutes)
            _cleanupTimer = new System.Threading.Timer(
                async _ => await CleanupDuplicatesAsync(),
                null,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(30));
        }

        public void ToggleMonitoring()
        {
            _clipboardService.ToggleMonitoring();
            IsMonitoring = _clipboardService.IsMonitoring;
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
            // Start with all entries
            var filteredEntries = _allEntries.AsEnumerable();

            // Apply data type filter if one is selected
            if (CurrentFilterType.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.DataType == CurrentFilterType.Value);
            }

            // Apply text search filter if text is provided
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filteredEntries = filteredEntries.Where(e =>
                    e.DataType == ClipboardDataType.Text &&
                    e.PlainText != null &&
                    e.PlainText.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Update the UI collection
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

                    // Only add to visible collection if it matches the current filters
                    bool matchesTypeFilter = !CurrentFilterType.HasValue || storageEntry.DataType == CurrentFilterType.Value;
                    bool matchesTextFilter = string.IsNullOrWhiteSpace(SearchText) ||
                        (storageEntry.DataType == ClipboardDataType.Text &&
                         storageEntry.PlainText != null &&
                         storageEntry.PlainText.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                    if (matchesTypeFilter && matchesTextFilter)
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
                string message;

                switch (entry.DataType)
                {
                    case ClipSage.Core.Storage.ClipboardDataType.Text:
                        message = $"Captured: Text \"{GetTruncatedText(entry.PlainText, 40)}\"";
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.Image:
                        message = "Captured: Image";
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.FilePaths:
                        if (entry.FilePaths != null && entry.FilePaths.Length > 0)
                        {
                            message = entry.FilePaths.Length == 1
                                ? $"Captured: File \"{Path.GetFileName(entry.FilePaths[0])}\""
                                : $"Captured: {entry.FilePaths.Length} files";
                        }
                        else
                        {
                            message = "Captured: Files";
                        }
                        break;
                    default:
                        message = "Captured: Clipboard content";
                        break;
                }

                // Update the event status text
                EventStatusText = message;
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error showing notification: {ex.Message}");
                Logger.Instance.Error("Error showing notification", ex);

                // Update status bar with error
                EventStatusText = "Error capturing clipboard content";
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
                    EventStatusText = $"Copied to clipboard: Text \"{GetTruncatedText(entry.PlainText, 40)}\"";
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
                    EventStatusText = "Copied to clipboard: Image";
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

                        if (fileCollection.Count == 1)
                        {
                            string fileName = System.IO.Path.GetFileName(fileCollection[0]);
                            EventStatusText = $"Copied to clipboard: File \"{fileName}\"";
                        }
                        else
                        {
                            EventStatusText = $"Copied to clipboard: {fileCollection.Count} files";
                        }

                        if (!allFilesExist)
                        {
                            EventStatusText += " (some files used cached copies)";
                        }
                    }
                    else
                    {
                        EventStatusText = "Error: No files found to copy";
                        MessageBox.Show("None of the original files exist and no cached copies were found.",
                            "Files Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                EventStatusText = $"Error copying to clipboard: {ex.Message}";
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

                // Update status bar
                switch (entry.DataType)
                {
                    case ClipSage.Core.Storage.ClipboardDataType.Text:
                        EventStatusText = $"Deleted: Text \"{GetTruncatedText(entry.PlainText, 40)}\"";
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.Image:
                        EventStatusText = "Deleted: Image";
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.FilePaths:
                        if (entry.FilePaths != null && entry.FilePaths.Length > 0)
                        {
                            EventStatusText = entry.FilePaths.Length == 1
                                ? $"Deleted: File \"{Path.GetFileName(entry.FilePaths[0])}\""
                                : $"Deleted: {entry.FilePaths.Length} files";
                        }
                        else
                        {
                            EventStatusText = "Deleted: Files";
                        }
                        break;
                    default:
                        EventStatusText = "Deleted: Clipboard entry";
                        break;
                }
            }
            catch (Exception ex)
            {
                EventStatusText = $"Error deleting entry: {ex.Message}";
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

                // Update status bar
                string action = isPinned ? "Pinned" : "Unpinned";
                switch (entry.DataType)
                {
                    case ClipSage.Core.Storage.ClipboardDataType.Text:
                        EventStatusText = $"{action}: Text \"{GetTruncatedText(entry.PlainText, 40)}\"";
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.Image:
                        EventStatusText = $"{action}: Image";
                        break;
                    case ClipSage.Core.Storage.ClipboardDataType.FilePaths:
                        if (entry.FilePaths != null && entry.FilePaths.Length > 0)
                        {
                            EventStatusText = entry.FilePaths.Length == 1
                                ? $"{action}: File \"{Path.GetFileName(entry.FilePaths[0])}\""
                                : $"{action}: {entry.FilePaths.Length} files";
                        }
                        else
                        {
                            EventStatusText = $"{action}: Files";
                        }
                        break;
                    default:
                        EventStatusText = $"{action}: Clipboard entry";
                        break;
                }
            }
            catch (Exception ex)
            {
                EventStatusText = $"Error {(isPinned ? "pinning" : "unpinning")} entry: {ex.Message}";
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
