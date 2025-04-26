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

        public HistoryStore()
        {
            // Default to AppData if no cache folder is configured
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var clipperPath = Path.Combine(appDataPath, "Clipper");
            Directory.CreateDirectory(clipperPath);
            _databasePath = Path.Combine(clipperPath, "history.db");
            _cacheFolderPath = clipperPath;
            _fileStore = new FileBasedClipboardStore(_cacheFolderPath);
        }

        public HistoryStore(string cacheFolderPath)
        {
            if (string.IsNullOrEmpty(cacheFolderPath))
                throw new ArgumentNullException(nameof(cacheFolderPath));

            _cacheFolderPath = cacheFolderPath;
            Directory.CreateDirectory(_cacheFolderPath);
            _databasePath = Path.Combine(_cacheFolderPath, "history.db");
            _fileStore = new FileBasedClipboardStore(_cacheFolderPath);
        }

        public async Task AddAsync(ClipboardEntry entry)
        {
            // Save to database
            await Task.Run(() =>
            {
                using var db = new LiteDatabase(_databasePath);
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
            });

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
            return await Task.Run(() =>
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<ClipboardEntry>("history");
                return collection.FindAll().OrderByDescending(e => e.Timestamp).Take(limit).ToList();
            });
        }

        public async Task DeleteAsync(Guid id)
        {
            // Get the entry type before deleting
            ClipboardDataType dataType = ClipboardDataType.Text; // Default
            bool entryFound = false;

            await Task.Run(() =>
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<ClipboardEntry>("history");
                var entry = collection.FindById(id);
                if (entry != null)
                {
                    dataType = entry.DataType;
                    entryFound = true;
                }
                collection.Delete(id);
            });

            // Delete from file system
            if (entryFound)
            {
                try
                {
                    // Delete the files associated with this entry
                    string folder = dataType == ClipboardDataType.Text ? "Text" : "Images";
                    string filePath = Path.Combine(_cacheFolderPath, folder, $"{id}.{(dataType == ClipboardDataType.Text ? "txt" : "png")}");
                    string metadataPath = Path.Combine(_cacheFolderPath, folder, $"{id}.meta");

                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    if (File.Exists(metadataPath))
                        File.Delete(metadataPath);
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
            await Task.Run(() =>
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<ClipboardEntry>("history");
                var entry = collection.FindById(id);
                if (entry != null)
                {
                    entry.Timestamp = isPinned ? DateTime.MaxValue : DateTime.UtcNow;
                    collection.Update(entry);
                }
            });
        }
    }

    public class ClipboardEntry
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public ClipboardDataType DataType { get; set; }
        public string? PlainText { get; set; }
        public byte[]? ImageBytes { get; set; }
    }

    public enum ClipboardDataType
    {
        Text,
        Image
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