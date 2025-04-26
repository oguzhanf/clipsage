using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;
using Clipper.App.Properties;

namespace Clipper.App
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Clipper.Core.ClipboardService _clipboardService;
        private readonly Clipper.Core.Storage.HistoryStore _historyStore;

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

        public MainViewModel()
        {
            _clipboardService = Clipper.Core.ClipboardService.Instance;

            // Use the configured cache folder if available
            string cacheFolderPath = Properties.Settings.Default.CachingFolder;
            if (Properties.Settings.Default.CachingFolderConfigured && !string.IsNullOrEmpty(cacheFolderPath))
            {
                _historyStore = new Clipper.Core.Storage.HistoryStore(cacheFolderPath);
            }
            else
            {
                _historyStore = new Clipper.Core.Storage.HistoryStore();
            }

            // Subscribe to clipboard changes
            _clipboardService.ClipboardChanged += OnClipboardChanged;

            // Load recent entries
            LoadRecentEntriesAsync();
        }

        private async void LoadRecentEntriesAsync()
        {
            try
            {
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
                e.DataType == Clipper.Core.Storage.ClipboardDataType.Text &&
                e.PlainText != null &&
                e.PlainText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            ClipboardEntries.Clear();
            foreach (var entry in filteredEntries)
            {
                ClipboardEntries.Add(entry);
            }
        }

        private async void OnClipboardChanged(object? sender, Clipper.Core.ClipboardEntry entry)
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
                var storageEntry = new Clipper.Core.Storage.ClipboardEntry
                {
                    Id = entry.Id,
                    Timestamp = entry.Timestamp,
                    DataType = (Clipper.Core.Storage.ClipboardDataType)entry.DataType,
                    PlainText = entry.PlainText,
                    ImageBytes = entry.ImageBytes,
                    FilePaths = entry.FilePaths
                };

                // Check for duplicates if enabled
                if (Properties.Settings.Default.IgnoreDuplicates && IsDuplicate(storageEntry))
                {
                    return;
                }

                await _historyStore.AddAsync(storageEntry);

                // Handle file caching if needed
                if (storageEntry.DataType == Clipper.Core.Storage.ClipboardDataType.FilePaths &&
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
                        (storageEntry.DataType == Clipper.Core.Storage.ClipboardDataType.Text &&
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
            }
        }

        private bool ShouldProcessClipboardEntry(Clipper.Core.ClipboardEntry entry)
        {
            // Check if we should ignore large text
            if (entry.DataType == Clipper.Core.ClipboardDataType.Text &&
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
            if (entry.DataType == Clipper.Core.ClipboardDataType.Image &&
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

        private void SanitizeClipboardEntry(Clipper.Core.ClipboardEntry entry)
        {
            // Truncate large text if needed
            if (entry.DataType == Clipper.Core.ClipboardDataType.Text &&
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

        private bool IsDuplicate(Clipper.Core.Storage.ClipboardEntry entry)
        {
            // Check if this entry is a duplicate of the most recent entry
            if (_allEntries.Count == 0)
                return false;

            var mostRecent = _allEntries[0];

            if (entry.DataType != mostRecent.DataType)
                return false;

            if (entry.DataType == Clipper.Core.Storage.ClipboardDataType.Text)
            {
                return entry.PlainText == mostRecent.PlainText;
            }
            else if (entry.DataType == Clipper.Core.Storage.ClipboardDataType.Image)
            {
                // For images, we'll compare the byte arrays
                if (entry.ImageBytes == null || mostRecent.ImageBytes == null)
                    return false;

                if (entry.ImageBytes.Length != mostRecent.ImageBytes.Length)
                    return false;

                // Simple byte-by-byte comparison
                for (int i = 0; i < entry.ImageBytes.Length; i++)
                {
                    if (entry.ImageBytes[i] != mostRecent.ImageBytes[i])
                        return false;
                }

                return true;
            }

            return false;
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

        private void ShowClipboardNotification(Clipper.Core.Storage.ClipboardEntry entry)
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
                            case Clipper.Core.Storage.ClipboardDataType.Text:
                                message = GetTruncatedText(entry.PlainText, 40);
                                break;
                            case Clipper.Core.Storage.ClipboardDataType.Image:
                                message = "[Image copied]";
                                break;
                            case Clipper.Core.Storage.ClipboardDataType.FilePaths:
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
                // Just log to console if there's an error
                Console.WriteLine($"Error showing notification: {ex.Message}");
            }
        }

        private string GetTruncatedText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
        }

        public async Task CopyToClipboardAsync(ClipboardEntryViewModel entry)
        {
            try
            {
                if (entry.DataType == Clipper.Core.Storage.ClipboardDataType.Text)
                {
                    Clipboard.SetText(entry.PlainText ?? string.Empty);
                }
                else if (entry.DataType == Clipper.Core.Storage.ClipboardDataType.Image && entry.ImageBytes != null)
                {
                    using var stream = new System.IO.MemoryStream(entry.ImageBytes);
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    Clipboard.SetImage(bitmap);
                }
                else if (entry.DataType == Clipper.Core.Storage.ClipboardDataType.FilePaths && entry.FilePaths != null && entry.FilePaths.Length > 0)
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
            }
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
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task CacheFilesAsync(Clipper.Core.Storage.ClipboardEntry entry)
        {
            try
            {
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching files: {ex.Message}");
                // We don't want to throw the exception here as it would disrupt the application flow
                // Just log it and continue
            }
        }

        public void Dispose()
        {
            // Unsubscribe from events
            _clipboardService.ClipboardChanged -= OnClipboardChanged;
        }
    }
}
