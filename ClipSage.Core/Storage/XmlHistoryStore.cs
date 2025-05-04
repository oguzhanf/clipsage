using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using ClipSage.Core.Logging;
using System.Text;

namespace ClipSage.Core.Storage
{
    /// <summary>
    /// Stores clipboard history in XML files, with one file per computer
    /// </summary>
    public class XmlHistoryStore : IHistoryStore, IDisposable
    {
        private const int MaxHistorySize = 500;
        private readonly string _cacheFolderPath;
        private readonly string _historyFolderPath;
        private readonly string _localHistoryFilePath;
        private readonly string _computerName;
        private readonly FileBasedClipboardStore _fileStore;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly object _historyLock = new object();
        private List<ClipboardEntry> _inMemoryHistory = new List<ClipboardEntry>();
        private bool _isInitialized = false;
        private bool _isReloading = false;

        /// <summary>
        /// Event raised when the history is updated from an external source
        /// </summary>
        public event EventHandler<EventArgs> HistoryExternallyUpdated;

        /// <summary>
        /// Creates a new instance of the XmlHistoryStore with the default cache folder
        /// </summary>
        public XmlHistoryStore()
        {
            // Default to AppData if no cache folder is configured
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var clipperPath = Path.Combine(appDataPath, "ClipSage");
            Directory.CreateDirectory(clipperPath);
            _cacheFolderPath = clipperPath;
            _historyFolderPath = Path.Combine(_cacheFolderPath, "History");
            _computerName = Environment.MachineName;
            _localHistoryFilePath = Path.Combine(_historyFolderPath, $"history-{_computerName}.xml");
            _fileStore = new FileBasedClipboardStore(_cacheFolderPath);

            // Create the history folder if it doesn't exist
            Directory.CreateDirectory(_historyFolderPath);

            // Initialize the file watcher
            _fileWatcher = new FileSystemWatcher(_historyFolderPath)
            {
                Filter = "*.xml",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnHistoryFileChanged;
            _fileWatcher.Created += OnHistoryFileChanged;

            // Initialize the history
            InitializeHistory();
        }

        /// <summary>
        /// Creates a new instance of the XmlHistoryStore with the specified cache folder
        /// </summary>
        /// <param name="cacheFolderPath">The path to the cache folder</param>
        public XmlHistoryStore(string cacheFolderPath)
        {
            if (string.IsNullOrEmpty(cacheFolderPath))
                throw new ArgumentNullException(nameof(cacheFolderPath));

            _cacheFolderPath = cacheFolderPath;
            _historyFolderPath = Path.Combine(_cacheFolderPath, "History");
            _computerName = Environment.MachineName;
            _localHistoryFilePath = Path.Combine(_historyFolderPath, $"history-{_computerName}.xml");
            _fileStore = new FileBasedClipboardStore(_cacheFolderPath);

            // Create the history folder if it doesn't exist
            Directory.CreateDirectory(_historyFolderPath);

            // Initialize the file watcher
            _fileWatcher = new FileSystemWatcher(_historyFolderPath)
            {
                Filter = "*.xml",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnHistoryFileChanged;
            _fileWatcher.Created += OnHistoryFileChanged;

            // Initialize the history
            InitializeHistory();
        }

        /// <summary>
        /// Initializes the history by loading all XML files in the history folder
        /// </summary>
        private void InitializeHistory()
        {
            try
            {
                lock (_historyLock)
                {
                    _inMemoryHistory.Clear();

                    // Get all XML files in the history folder
                    var historyFiles = Directory.GetFiles(_historyFolderPath, "*.xml");

                    foreach (var file in historyFiles)
                    {
                        try
                        {
                            var entries = LoadHistoryFromFile(file);
                            if (entries != null)
                            {
                                _inMemoryHistory.AddRange(entries);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error($"Error loading history from file {file}", ex);
                        }
                    }

                    // Sort by timestamp (newest first)
                    _inMemoryHistory = _inMemoryHistory
                        .OrderByDescending(e => e.Timestamp)
                        .ToList();

                    _isInitialized = true;
                    Logger.Instance.Info($"Initialized history with {_inMemoryHistory.Count} entries from {historyFiles.Length} files");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error initializing history", ex);
            }
        }

        /// <summary>
        /// Handles changes to history files
        /// </summary>
        private void OnHistoryFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Avoid reloading if we're currently writing to the file
                if (_isReloading)
                    return;

                // Ignore changes to the local history file
                if (string.Equals(e.FullPath, _localHistoryFilePath, StringComparison.OrdinalIgnoreCase))
                    return;

                Logger.Instance.Info($"Detected change in history file: {e.FullPath}, {e.ChangeType}");

                // Wait a moment to ensure the file is not locked
                Task.Delay(500).ContinueWith(_ =>
                {
                    try
                    {
                        _isReloading = true;

                        // Reload the history
                        lock (_historyLock)
                        {
                            // Load the changed file
                            var entries = LoadHistoryFromFile(e.FullPath);

                            if (entries != null)
                            {
                                // Remove existing entries from this file
                                var fileName = Path.GetFileName(e.FullPath);
                                _inMemoryHistory.RemoveAll(entry =>
                                    entry.SourceFile != null &&
                                    entry.SourceFile.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                                // Add the new entries
                                _inMemoryHistory.AddRange(entries);

                                // Sort by timestamp (newest first)
                                _inMemoryHistory = _inMemoryHistory
                                    .OrderByDescending(entry => entry.Timestamp)
                                    .ToList();

                                Logger.Instance.Info($"Reloaded history from {e.FullPath}, now have {_inMemoryHistory.Count} entries");

                                // Notify listeners that the history has been updated
                                HistoryExternallyUpdated?.Invoke(this, EventArgs.Empty);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error($"Error handling history file change for {e.FullPath}", ex);
                    }
                    finally
                    {
                        _isReloading = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"Error in OnHistoryFileChanged for {e.FullPath}", ex);
            }
        }

        /// <summary>
        /// Loads history entries from an XML file
        /// </summary>
        /// <param name="filePath">The path to the XML file</param>
        /// <returns>A list of clipboard entries, or null if the file could not be loaded</returns>
        private List<ClipboardEntry> LoadHistoryFromFile(string filePath)
        {
            try
            {
                // Wait for the file to be available
                int retryCount = 0;
                while (IsFileLocked(filePath) && retryCount < 5)
                {
                    Task.Delay(100).Wait();
                    retryCount++;
                }

                if (IsFileLocked(filePath))
                {
                    Logger.Instance.Error($"File {filePath} is locked, cannot load history");
                    return null;
                }

                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fileStream.Length == 0)
                    {
                        Logger.Instance.Info($"File {filePath} is empty");
                        return new List<ClipboardEntry>();
                    }

                    var serializer = new XmlSerializer(typeof(List<ClipboardEntry>));
                    var entries = (List<ClipboardEntry>)serializer.Deserialize(fileStream);

                    // Set the source file for each entry
                    string fileName = Path.GetFileName(filePath);
                    foreach (var entry in entries)
                    {
                        entry.SourceFile = fileName;
                    }

                    return entries;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"Error loading history from file {filePath}", ex);
                return null;
            }
        }

        /// <summary>
        /// Checks if a file is locked
        /// </summary>
        /// <param name="filePath">The path to the file</param>
        /// <returns>True if the file is locked, false otherwise</returns>
        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Saves the local history to the XML file
        /// </summary>
        private async Task SaveLocalHistoryAsync()
        {
            try
            {
                // Get only entries from this computer
                var localEntries = _inMemoryHistory
                    .Where(e => string.IsNullOrEmpty(e.SourceFile) ||
                           e.SourceFile.Equals($"history-{_computerName}.xml", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(e => e.Timestamp)
                    .Take(MaxHistorySize)
                    .ToList();

                // Set the source file for each entry
                foreach (var entry in localEntries)
                {
                    entry.SourceFile = $"history-{_computerName}.xml";
                }

                // Create a temporary file
                string tempFilePath = _localHistoryFilePath + ".tmp";

                // Serialize to the temporary file
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Use UTF-8 encoding with BOM
                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        Encoding = new UTF8Encoding(true)
                    };

                    using (var writer = XmlWriter.Create(fileStream, settings))
                    {
                        var serializer = new XmlSerializer(typeof(List<ClipboardEntry>));
                        serializer.Serialize(writer, localEntries);
                    }
                }

                // Replace the original file with the temporary file
                if (File.Exists(_localHistoryFilePath))
                {
                    File.Delete(_localHistoryFilePath);
                }
                File.Move(tempFilePath, _localHistoryFilePath);

                Logger.Instance.Info($"Saved {localEntries.Count} entries to local history file");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error saving local history", ex);
            }
        }

        /// <summary>
        /// Checks if an entry is a duplicate of an existing entry
        /// </summary>
        /// <param name="entry">The entry to check</param>
        /// <returns>True if the entry is a duplicate, false otherwise</returns>
        public async Task<bool> IsDuplicateAsync(ClipboardEntry entry)
        {
            try
            {
                // Wait for initialization to complete
                while (!_isInitialized)
                {
                    await Task.Delay(100);
                }

                lock (_historyLock)
                {
                    // First, filter by data type to reduce the number of entries to check
                    var sameTypeEntries = _inMemoryHistory.Where(e => e.DataType == entry.DataType).ToList();

                    // For text entries, we can do a more efficient query
                    if (entry.DataType == ClipboardDataType.Text && !string.IsNullOrEmpty(entry.PlainText))
                    {
                        // Check if there's an exact text match
                        var textMatch = sameTypeEntries.FirstOrDefault(e => e.PlainText == entry.PlainText);
                        if (textMatch != null)
                        {
                            return true;
                        }
                    }
                    else if (entry.DataType == ClipboardDataType.Image && entry.ImageBytes != null && entry.ImageBytes.Length > 0)
                    {
                        // For images, we need to compare the bytes
                        // This is a simple comparison and might not be perfect for all image formats
                        foreach (var existingEntry in sameTypeEntries)
                        {
                            if (existingEntry.ImageBytes != null &&
                                existingEntry.ImageBytes.Length == entry.ImageBytes.Length)
                            {
                                bool isMatch = true;
                                for (int i = 0; i < entry.ImageBytes.Length; i++)
                                {
                                    if (existingEntry.ImageBytes[i] != entry.ImageBytes[i])
                                    {
                                        isMatch = false;
                                        break;
                                    }
                                }

                                if (isMatch)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    else if (entry.DataType == ClipboardDataType.FilePaths && entry.FilePaths != null && entry.FilePaths.Length > 0)
                    {
                        // For file paths, we need to compare the arrays
                        foreach (var existingEntry in sameTypeEntries)
                        {
                            if (existingEntry.FilePaths != null &&
                                existingEntry.FilePaths.Length == entry.FilePaths.Length)
                            {
                                bool isMatch = true;
                                for (int i = 0; i < entry.FilePaths.Length; i++)
                                {
                                    if (existingEntry.FilePaths[i] != entry.FilePaths[i])
                                    {
                                        isMatch = false;
                                        break;
                                    }
                                }

                                if (isMatch)
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error checking for duplicate", ex);
                return false;
            }
        }

        /// <summary>
        /// Adds a new entry to the history
        /// </summary>
        /// <param name="entry">The entry to add</param>
        public async Task AddAsync(ClipboardEntry entry)
        {
            // Check for duplicates before adding
            if (await IsDuplicateAsync(entry))
            {
                Logger.Instance.Debug("Duplicate entry detected, skipping...");
                return;
            }

            try
            {
                // Set the source file for the entry
                entry.SourceFile = $"history-{_computerName}.xml";

                // Add to in-memory history
                lock (_historyLock)
                {
                    _inMemoryHistory.Add(entry);

                    // Sort by timestamp (newest first)
                    _inMemoryHistory = _inMemoryHistory
                        .OrderByDescending(e => e.Timestamp)
                        .ToList();
                }

                // Save to XML file
                await SaveLocalHistoryAsync();

                // Save to file system
                try
                {
                    await _fileStore.SaveClipboardEntryAsync(entry);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("Error saving clipboard entry to file", ex);
                    // We don't want to throw the exception here as it would disrupt the application flow
                    // Just log it and continue
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error adding entry to history", ex);
            }
        }

        /// <summary>
        /// Gets the most recent entries from the history
        /// </summary>
        /// <param name="limit">The maximum number of entries to return</param>
        /// <returns>A list of clipboard entries</returns>
        public async Task<List<ClipboardEntry>> GetRecentAsync(int limit)
        {
            try
            {
                // Wait for initialization to complete
                while (!_isInitialized)
                {
                    await Task.Delay(100);
                }

                lock (_historyLock)
                {
                    // Get all entries sorted by timestamp (newest first)
                    var allEntries = _inMemoryHistory
                        .OrderByDescending(e => e.Timestamp)
                        .ToList();

                    // Use a more efficient approach to filter duplicates
                    var uniqueEntries = new List<ClipboardEntry>();

                    // Use dictionaries to track seen content by type
                    var seenTextContent = new HashSet<string>();
                    var seenImageHashes = new HashSet<string>();
                    var seenFilePathSets = new HashSet<string>();

                    foreach (var entry in allEntries)
                    {
                        bool isDuplicate = false;

                        if (entry.DataType == ClipboardDataType.Text && !string.IsNullOrEmpty(entry.PlainText))
                        {
                            // For text entries, use the text content as the key
                            if (seenTextContent.Contains(entry.PlainText))
                            {
                                isDuplicate = true;
                            }
                            else
                            {
                                seenTextContent.Add(entry.PlainText);
                            }
                        }
                        else if (entry.DataType == ClipboardDataType.Image && entry.ImageBytes != null)
                        {
                            // For image entries, use a simple hash of the image bytes
                            string hash = ComputeHash(entry.ImageBytes);
                            if (seenImageHashes.Contains(hash))
                            {
                                isDuplicate = true;
                            }
                            else
                            {
                                seenImageHashes.Add(hash);
                            }
                        }
                        else if (entry.DataType == ClipboardDataType.FilePaths && entry.FilePaths != null)
                        {
                            // For file path entries, use a hash of the sorted file paths
                            string pathsHash = string.Join("|", entry.FilePaths.OrderBy(p => p));
                            if (seenFilePathSets.Contains(pathsHash))
                            {
                                isDuplicate = true;
                            }
                            else
                            {
                                seenFilePathSets.Add(pathsHash);
                            }
                        }

                        if (!isDuplicate)
                        {
                            uniqueEntries.Add(entry);

                            // Stop once we have enough entries
                            if (uniqueEntries.Count >= limit)
                            {
                                break;
                            }
                        }
                    }

                    return uniqueEntries;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error getting recent entries", ex);
                return new List<ClipboardEntry>();
            }
        }

        /// <summary>
        /// Deletes an entry from the history
        /// </summary>
        /// <param name="id">The ID of the entry to delete</param>
        public async Task DeleteAsync(Guid id)
        {
            try
            {
                // Get the entry type before deleting
                ClipboardDataType dataType = ClipboardDataType.Text; // Default
                bool entryFound = false;
                string sourceFile = null;

                lock (_historyLock)
                {
                    var entry = _inMemoryHistory.FirstOrDefault(e => e.Id == id);
                    if (entry != null)
                    {
                        dataType = entry.DataType;
                        sourceFile = entry.SourceFile;
                        entryFound = true;

                        // Remove from in-memory history
                        _inMemoryHistory.Remove(entry);
                    }
                }

                // Only update the local history file if the entry was from this computer
                if (entryFound && (string.IsNullOrEmpty(sourceFile) ||
                                  sourceFile.Equals($"history-{_computerName}.xml", StringComparison.OrdinalIgnoreCase)))
                {
                    await SaveLocalHistoryAsync();
                }

                // Delete from file system
                if (entryFound)
                {
                    try
                    {
                        await _fileStore.DeleteClipboardEntryAsync(id, dataType);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("Error deleting clipboard entry from file", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error deleting entry", ex);
            }
        }

        /// <summary>
        /// Pins or unpins an entry in the history
        /// </summary>
        /// <param name="id">The ID of the entry to pin/unpin</param>
        /// <param name="isPinned">True to pin, false to unpin</param>
        public async Task PinAsync(Guid id, bool isPinned)
        {
            try
            {
                bool entryFound = false;
                string sourceFile = null;

                lock (_historyLock)
                {
                    var entry = _inMemoryHistory.FirstOrDefault(e => e.Id == id);
                    if (entry != null)
                    {
                        entry.Timestamp = isPinned ? DateTime.MaxValue : DateTime.UtcNow;
                        sourceFile = entry.SourceFile;
                        entryFound = true;

                        // Re-sort the history
                        _inMemoryHistory = _inMemoryHistory
                            .OrderByDescending(e => e.Timestamp)
                            .ToList();
                    }
                }

                // Only update the local history file if the entry was from this computer
                if (entryFound && (string.IsNullOrEmpty(sourceFile) ||
                                  sourceFile.Equals($"history-{_computerName}.xml", StringComparison.OrdinalIgnoreCase)))
                {
                    await SaveLocalHistoryAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error pinning entry", ex);
            }
        }

        /// <summary>
        /// Removes duplicate entries from the history
        /// </summary>
        /// <returns>The number of duplicates removed</returns>
        public async Task<int> CleanupDuplicatesAsync()
        {
            try
            {
                int removedCount = 0;

                lock (_historyLock)
                {
                    var allEntries = _inMemoryHistory
                        .OrderByDescending(e => e.Timestamp)
                        .ToList();

                    // Track unique content
                    var seenTextContent = new Dictionary<string, Guid>();
                    var seenImageHashes = new Dictionary<string, Guid>();
                    var seenFilePathSets = new Dictionary<string, Guid>();

                    // Track entries to keep
                    var entriesToKeep = new List<ClipboardEntry>();

                    foreach (var entry in allEntries)
                    {
                        bool isDuplicate = false;

                        if (entry.DataType == ClipboardDataType.Text && !string.IsNullOrEmpty(entry.PlainText))
                        {
                            if (seenTextContent.ContainsKey(entry.PlainText))
                            {
                                isDuplicate = true;
                            }
                            else
                            {
                                seenTextContent[entry.PlainText] = entry.Id;
                            }
                        }
                        else if (entry.DataType == ClipboardDataType.Image && entry.ImageBytes != null)
                        {
                            string hash = ComputeHash(entry.ImageBytes);
                            if (seenImageHashes.ContainsKey(hash))
                            {
                                isDuplicate = true;
                            }
                            else
                            {
                                seenImageHashes[hash] = entry.Id;
                            }
                        }
                        else if (entry.DataType == ClipboardDataType.FilePaths && entry.FilePaths != null)
                        {
                            string pathsHash = string.Join("|", entry.FilePaths.OrderBy(p => p));
                            if (seenFilePathSets.ContainsKey(pathsHash))
                            {
                                isDuplicate = true;
                            }
                            else
                            {
                                seenFilePathSets[pathsHash] = entry.Id;
                            }
                        }

                        if (!isDuplicate)
                        {
                            entriesToKeep.Add(entry);
                        }
                        else
                        {
                            removedCount++;
                        }
                    }

                    // Update the in-memory history
                    _inMemoryHistory = entriesToKeep;
                }

                // Save the local history
                await SaveLocalHistoryAsync();

                return removedCount;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error cleaning up duplicates", ex);
                return 0;
            }
        }

        /// <summary>
        /// Computes a simple hash of a byte array
        /// </summary>
        /// <param name="bytes">The byte array to hash</param>
        /// <returns>A string representation of the hash</returns>
        private string ComputeHash(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            // Use a simple hash algorithm for performance
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < bytes.Length; i++)
                {
                    hash = (hash ^ bytes[i]) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;

                return hash.ToString("X");
            }
        }

        /// <summary>
        /// Disposes the file watcher
        /// </summary>
        public void Dispose()
        {
            _fileWatcher.Changed -= OnHistoryFileChanged;
            _fileWatcher.Created -= OnHistoryFileChanged;
            _fileWatcher.Dispose();
        }
    }
}
