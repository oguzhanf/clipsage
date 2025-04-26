using System;
using System.IO;
using System.Threading.Tasks;

namespace Clipper.Core.Storage
{
    public class FileBasedClipboardStore
    {
        private readonly string _cacheFolderPath;
        private readonly string _textFolderPath;
        private readonly string _imageFolderPath;

        public FileBasedClipboardStore(string cacheFolderPath)
        {
            _cacheFolderPath = cacheFolderPath;
            _textFolderPath = Path.Combine(_cacheFolderPath, "Text");
            _imageFolderPath = Path.Combine(_cacheFolderPath, "Images");

            // Create directories if they don't exist
            Directory.CreateDirectory(_cacheFolderPath);
            Directory.CreateDirectory(_textFolderPath);
            Directory.CreateDirectory(_imageFolderPath);
        }

        public async Task SaveClipboardEntryAsync(ClipboardEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            switch (entry.DataType)
            {
                case ClipboardDataType.Text:
                    await SaveTextEntryAsync(entry);
                    break;
                case ClipboardDataType.Image:
                    await SaveImageEntryAsync(entry);
                    break;
                default:
                    throw new ArgumentException($"Unsupported clipboard data type: {entry.DataType}");
            }
        }

        private async Task SaveTextEntryAsync(ClipboardEntry entry)
        {
            if (string.IsNullOrEmpty(entry.PlainText))
                return;

            string filePath = Path.Combine(_textFolderPath, $"{entry.Id}.txt");
            await File.WriteAllTextAsync(filePath, entry.PlainText);

            // Create a metadata file with timestamp
            string metadataPath = Path.Combine(_textFolderPath, $"{entry.Id}.meta");
            await File.WriteAllTextAsync(metadataPath, entry.Timestamp.ToString("o"));
        }

        private async Task SaveImageEntryAsync(ClipboardEntry entry)
        {
            if (entry.ImageBytes == null || entry.ImageBytes.Length == 0)
                return;

            string filePath = Path.Combine(_imageFolderPath, $"{entry.Id}.png");
            await File.WriteAllBytesAsync(filePath, entry.ImageBytes);

            // Create a metadata file with timestamp
            string metadataPath = Path.Combine(_imageFolderPath, $"{entry.Id}.meta");
            await File.WriteAllTextAsync(metadataPath, entry.Timestamp.ToString("o"));
        }

        public async Task<ClipboardEntry> LoadClipboardEntryAsync(Guid id, ClipboardDataType dataType)
        {
            switch (dataType)
            {
                case ClipboardDataType.Text:
                    return await LoadTextEntryAsync(id);
                case ClipboardDataType.Image:
                    return await LoadImageEntryAsync(id);
                default:
                    throw new ArgumentException($"Unsupported clipboard data type: {dataType}");
            }
        }

        private async Task<ClipboardEntry> LoadTextEntryAsync(Guid id)
        {
            string filePath = Path.Combine(_textFolderPath, $"{id}.txt");
            string metadataPath = Path.Combine(_textFolderPath, $"{id}.meta");

            if (!File.Exists(filePath) || !File.Exists(metadataPath))
                return null;

            string text = await File.ReadAllTextAsync(filePath);
            string timestampStr = await File.ReadAllTextAsync(metadataPath);
            DateTime timestamp = DateTime.Parse(timestampStr);

            return new ClipboardEntry
            {
                Id = id,
                Timestamp = timestamp,
                DataType = ClipboardDataType.Text,
                PlainText = text
            };
        }

        private async Task<ClipboardEntry> LoadImageEntryAsync(Guid id)
        {
            string filePath = Path.Combine(_imageFolderPath, $"{id}.png");
            string metadataPath = Path.Combine(_imageFolderPath, $"{id}.meta");

            if (!File.Exists(filePath) || !File.Exists(metadataPath))
                return null;

            byte[] imageBytes = await File.ReadAllBytesAsync(filePath);
            string timestampStr = await File.ReadAllTextAsync(metadataPath);
            DateTime timestamp = DateTime.Parse(timestampStr);

            return new ClipboardEntry
            {
                Id = id,
                Timestamp = timestamp,
                DataType = ClipboardDataType.Image,
                ImageBytes = imageBytes
            };
        }

        public void CleanupOldEntries(int maxEntries)
        {
            // Implement cleanup logic to remove old entries when the number exceeds maxEntries
            // This would involve listing files, sorting by timestamp, and deleting the oldest ones
        }
    }
}
