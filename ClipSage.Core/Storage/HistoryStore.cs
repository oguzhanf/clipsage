using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClipSage.Core.Logging;
using LiteDB;

namespace ClipSage.Core.Storage
{
    public class HistoryStore
    {
        private const int MaxHistorySize = 500;
        private readonly string _databasePath;
        private readonly FileBasedClipboardStore _fileStore;
        private readonly string _cacheFolderPath;
        private readonly DatabaseConnectionManager _dbManager;

        public HistoryStore()
        {
            // Default to AppData if no cache folder is configured
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var clipperPath = Path.Combine(appDataPath, "Clipper");
            Directory.CreateDirectory(clipperPath);
            _databasePath = Path.Combine(clipperPath, "history.db");
            _cacheFolderPath = clipperPath;
            _fileStore = new FileBasedClipboardStore(_cacheFolderPath);

            // Initialize the database connection manager
            _dbManager = DatabaseConnectionManager.Instance;
            _dbManager.Initialize(_databasePath);
        }

        public HistoryStore(string cacheFolderPath)
        {
            if (string.IsNullOrEmpty(cacheFolderPath))
                throw new ArgumentNullException(nameof(cacheFolderPath));

            _cacheFolderPath = cacheFolderPath;
            Directory.CreateDirectory(_cacheFolderPath);
            _databasePath = Path.Combine(_cacheFolderPath, "history.db");
            _fileStore = new FileBasedClipboardStore(_cacheFolderPath);

            // Initialize the database connection manager
            _dbManager = DatabaseConnectionManager.Instance;
            _dbManager.Initialize(_databasePath);
        }

        public async Task<bool> IsDuplicateAsync(ClipboardEntry entry)
        {
            try
            {
                return await _dbManager.ExecuteWithRetryAsync(db =>
                {
                    var collection = db.GetCollection<ClipboardEntry>("history");

                    // First, filter by data type to reduce the number of entries to check
                    var sameTypeEntries = collection.Find(e => e.DataType == entry.DataType).ToList();

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
                    // For other types, we need to do a full comparison
                    else
                    {
                        // Check all entries of the same type
                        foreach (var existingEntry in sameTypeEntries)
                        {
                            if (ClipboardEntryComparer.AreEntriesEqual(entry, existingEntry))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for duplicate entries: {ex.Message}");
                Logger.Instance.Error("Error checking for duplicate entries", ex);
                return false; // If there's an error, assume it's not a duplicate
            }
        }

        public async Task AddAsync(ClipboardEntry entry)
        {
            // Check for duplicates before adding
            if (await IsDuplicateAsync(entry))
            {
                Console.WriteLine("Duplicate entry detected, skipping...");
                Logger.Instance.Debug("Duplicate entry detected, skipping...");
                return;
            }

            // Save to database using the connection manager with retry logic
            try
            {
                await _dbManager.ExecuteWithRetryAsync(db =>
                {
                    var collection = db.GetCollection<ClipboardEntry>("history");
                    collection.Insert(entry);

                    if (collection.Count() > MaxHistorySize)
                    {
                        var oldest = collection.FindAll().OrderBy(e => e.Timestamp).Take(collection.Count() - MaxHistorySize);
                        foreach (var item in oldest)
                        {
                            collection.Delete(item.Id);
                        }
                    }

                    return true;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving clipboard entry to database: {ex.Message}");
                Logger.Instance.Error("Error saving clipboard entry to database", ex);
                // Log the error but continue to try saving to the file system
            }

            // Save to file system
            try
            {
                await _fileStore.SaveClipboardEntryAsync(entry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving clipboard entry to file: {ex.Message}");
                Logger.Instance.Error("Error saving clipboard entry to file", ex);
                // We don't want to throw the exception here as it would disrupt the application flow
                // Just log it and continue
            }
        }

        public async Task<List<ClipboardEntry>> GetRecentAsync(int limit)
        {
            try
            {
                return await _dbManager.ExecuteWithRetryAsync(db =>
                {
                    var collection = db.GetCollection<ClipboardEntry>("history");

                    // Get all entries sorted by timestamp (newest first)
                    var allEntries = collection.FindAll().OrderByDescending(e => e.Timestamp).ToList();

                    // Use a more efficient approach to filter duplicates
                    var uniqueEntries = new List<ClipboardEntry>();

                    // Use dictionaries to track seen content by type
                    var seenTextContent = new HashSet<string>();
                    var seenImageHashes = new HashSet<string>();
                    var seenFilePathSets = new HashSet<string>();

                    foreach (var entry in allEntries)
                    {
                        bool isDuplicate = false;

                        switch (entry.DataType)
                        {
                            case ClipboardDataType.Text:
                                // For text, we can use a simple string comparison
                                if (!string.IsNullOrEmpty(entry.PlainText))
                                {
                                    isDuplicate = !seenTextContent.Add(entry.PlainText);
                                }
                                break;

                            case ClipboardDataType.Image:
                                // For images, create a simple hash of the first few bytes
                                if (entry.ImageBytes != null && entry.ImageBytes.Length > 0)
                                {
                                    // Create a simple hash from the image bytes
                                    string imageHash = ComputeSimpleHash(entry.ImageBytes);
                                    isDuplicate = !seenImageHashes.Add(imageHash);
                                }
                                break;

                            case ClipboardDataType.FilePaths:
                                // For file paths, create a hash of the sorted paths
                                if (entry.FilePaths != null && entry.FilePaths.Length > 0)
                                {
                                    string pathsHash = string.Join("|", entry.FilePaths.OrderBy(p => p));
                                    isDuplicate = !seenFilePathSets.Add(pathsHash);
                                }
                                break;
                        }

                        if (!isDuplicate)
                        {
                            uniqueEntries.Add(entry);

                            // Stop once we have enough entries
                            if (uniqueEntries.Count >= limit)
                                break;
                        }
                    }

                    return uniqueEntries;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving recent clipboard entries: {ex.Message}");
                Logger.Instance.Error("Error retrieving recent clipboard entries", ex);
                // Return an empty list if there's an error
                return new List<ClipboardEntry>();
            }
        }

        // Helper method to compute a simple hash for image bytes
        private string ComputeSimpleHash(byte[] data)
        {
            // Use a simple hash algorithm for quick comparison
            // This is not cryptographically secure but is sufficient for duplicate detection
            if (data == null || data.Length == 0)
                return string.Empty;

            // Use at most 1024 bytes for the hash to keep it fast
            int bytesToUse = Math.Min(data.Length, 1024);

            // Simple hash algorithm
            uint hash = 0;
            for (int i = 0; i < bytesToUse; i++)
            {
                hash += data[i];
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }

            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);

            return hash.ToString();
        }

        public async Task DeleteAsync(Guid id)
        {
            // Get the entry type before deleting
            ClipboardDataType dataType = ClipboardDataType.Text; // Default
            bool entryFound = false;

            try
            {
                await _dbManager.ExecuteWithRetryAsync(db =>
                {
                    var collection = db.GetCollection<ClipboardEntry>("history");
                    var entry = collection.FindById(id);
                    if (entry != null)
                    {
                        dataType = entry.DataType;
                        entryFound = true;
                    }
                    return collection.Delete(id);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting clipboard entry from database: {ex.Message}");
                Logger.Instance.Error("Error deleting clipboard entry from database", ex);
                // Continue to try deleting from the file system
            }

            // Delete from file system
            if (entryFound)
            {
                try
                {
                    // Delete the files associated with this entry
                    string folder;
                    string extension;

                    switch (dataType)
                    {
                        case ClipboardDataType.Text:
                            folder = "Text";
                            extension = "txt";
                            break;
                        case ClipboardDataType.Image:
                            folder = "Images";
                            extension = "png";
                            break;
                        case ClipboardDataType.FilePaths:
                            folder = "FilePaths";
                            extension = "txt";
                            break;
                        default:
                            folder = "Text";
                            extension = "txt";
                            break;
                    }

                    string filePath = Path.Combine(_cacheFolderPath, folder, $"{id}.{extension}");
                    string metadataPath = Path.Combine(_cacheFolderPath, folder, $"{id}.meta");

                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    if (File.Exists(metadataPath))
                        File.Delete(metadataPath);

                    // If this was a file paths entry, also delete any cached files
                    if (dataType == ClipboardDataType.FilePaths)
                    {
                        string cacheFolder = Path.Combine(_cacheFolderPath, "Files", id.ToString());
                        if (Directory.Exists(cacheFolder))
                        {
                            Directory.Delete(cacheFolder, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting clipboard entry file: {ex.Message}");
                    Logger.Instance.Error("Error deleting clipboard entry file", ex);
                    // We don't want to throw the exception here as it would disrupt the application flow
                    // Just log it and continue
                }
            }
        }

        public async Task PinAsync(Guid id, bool isPinned)
        {
            try
            {
                await _dbManager.ExecuteWithRetryAsync(db =>
                {
                    var collection = db.GetCollection<ClipboardEntry>("history");
                    var entry = collection.FindById(id);
                    if (entry != null)
                    {
                        entry.Timestamp = isPinned ? DateTime.MaxValue : DateTime.UtcNow;
                        return collection.Update(entry);
                    }
                    return false;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pinning clipboard entry: {ex.Message}");
                Logger.Instance.Error("Error pinning clipboard entry", ex);
            }
        }

        /// <summary>
        /// Removes duplicate entries from the database
        /// </summary>
        /// <returns>The number of duplicates removed</returns>
        public async Task<int> CleanupDuplicatesAsync()
        {
            try
            {
                return await _dbManager.ExecuteWithRetryAsync(db =>
                {
                    var collection = db.GetCollection<ClipboardEntry>("history");
                    var allEntries = collection.FindAll().OrderByDescending(e => e.Timestamp).ToList();

                    // Track unique content
                    var seenTextContent = new Dictionary<string, Guid>();
                    var seenImageHashes = new Dictionary<string, Guid>();
                    var seenFilePathSets = new Dictionary<string, Guid>();

                    // Track entries to delete
                    var duplicatesToRemove = new List<Guid>();

                    foreach (var entry in allEntries)
                    {
                        bool isDuplicate = false;

                        switch (entry.DataType)
                        {
                            case ClipboardDataType.Text:
                                if (!string.IsNullOrEmpty(entry.PlainText))
                                {
                                    if (seenTextContent.TryGetValue(entry.PlainText, out Guid existingId))
                                    {
                                        // This is a duplicate
                                        isDuplicate = true;
                                        duplicatesToRemove.Add(entry.Id);
                                    }
                                    else
                                    {
                                        // First time seeing this content
                                        seenTextContent[entry.PlainText] = entry.Id;
                                    }
                                }
                                break;

                            case ClipboardDataType.Image:
                                if (entry.ImageBytes != null && entry.ImageBytes.Length > 0)
                                {
                                    string imageHash = ComputeSimpleHash(entry.ImageBytes);
                                    if (seenImageHashes.TryGetValue(imageHash, out Guid existingId))
                                    {
                                        // This is a duplicate
                                        isDuplicate = true;
                                        duplicatesToRemove.Add(entry.Id);
                                    }
                                    else
                                    {
                                        // First time seeing this content
                                        seenImageHashes[imageHash] = entry.Id;
                                    }
                                }
                                break;

                            case ClipboardDataType.FilePaths:
                                if (entry.FilePaths != null && entry.FilePaths.Length > 0)
                                {
                                    string pathsHash = string.Join("|", entry.FilePaths.OrderBy(p => p));
                                    if (seenFilePathSets.TryGetValue(pathsHash, out Guid existingId))
                                    {
                                        // This is a duplicate
                                        isDuplicate = true;
                                        duplicatesToRemove.Add(entry.Id);
                                    }
                                    else
                                    {
                                        // First time seeing this content
                                        seenFilePathSets[pathsHash] = entry.Id;
                                    }
                                }
                                break;
                        }
                    }

                    // Delete all duplicates
                    int removedCount = 0;
                    foreach (var id in duplicatesToRemove)
                    {
                        if (collection.Delete(id))
                        {
                            removedCount++;
                        }
                    }

                    return removedCount;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up duplicate entries: {ex.Message}");
                Logger.Instance.Error("Error cleaning up duplicate entries", ex);
                return 0;
            }
        }
    }

    public class ClipboardEntry
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public ClipboardDataType DataType { get; set; }
        public string? PlainText { get; set; }
        public byte[]? ImageBytes { get; set; }
        public string[]? FilePaths { get; set; }
    }

    public enum ClipboardDataType
    {
        Text,
        Image,
        FilePaths
    }


}