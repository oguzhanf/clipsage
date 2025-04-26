using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;

namespace Clipper.Core.Storage
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

        public async Task AddAsync(ClipboardEntry entry)
        {
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
                    return collection.FindAll().OrderByDescending(e => e.Timestamp).Take(limit).ToList();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving recent clipboard entries: {ex.Message}");
                // Return an empty list if there's an error
                return new List<ClipboardEntry>();
            }
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

    public class ClipboardService
    {
        private static readonly Lazy<ClipboardService> _instance = new(() => new ClipboardService());
        public static ClipboardService Instance => _instance.Value;

        public event EventHandler<ClipboardEntry>? ClipboardChanged;

        private ClipboardService()
        {
            // Hook into clipboard viewer chain (Win32 API)
            // AddClipboardFormatListener logic here
        }

        public void RaiseClipboardChanged(ClipboardEntry entry)
        {
            ClipboardChanged?.Invoke(this, entry);
        }

        public async Task SetAsync(ClipboardEntry entry)
        {
            // Logic to set clipboard content (text or image)
        }
    }
}