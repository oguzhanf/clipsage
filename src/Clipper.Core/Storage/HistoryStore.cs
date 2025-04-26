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

        public HistoryStore()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var clipperPath = Path.Combine(appDataPath, "Clipper");
            Directory.CreateDirectory(clipperPath);
            _databasePath = Path.Combine(clipperPath, "history.db");
        }

        public async Task AddAsync(ClipboardEntry entry)
        {
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
            await Task.Run(() =>
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<ClipboardEntry>("history");
                collection.Delete(id);
            });
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