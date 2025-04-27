using System;
using System.IO;
using System.Threading.Tasks;
using Clipper.Core.Logging;

namespace Clipper.Core.Storage
{
    public class FileBasedClipboardStore
    {
        private readonly string _cacheFolderPath;
        private readonly string _textFolderPath;
        private readonly string _imageFolderPath;
        private readonly string _filePathsFolderPath;
        private readonly string _filesCacheFolderPath;

        public FileBasedClipboardStore(string cacheFolderPath)
        {
            _cacheFolderPath = cacheFolderPath;
            _textFolderPath = Path.Combine(_cacheFolderPath, "Text");
            _imageFolderPath = Path.Combine(_cacheFolderPath, "Images");
            _filePathsFolderPath = Path.Combine(_cacheFolderPath, "FilePaths");
            _filesCacheFolderPath = Path.Combine(_cacheFolderPath, "Files");

            // Create directories if they don't exist
            Directory.CreateDirectory(_cacheFolderPath);
            Directory.CreateDirectory(_textFolderPath);
            Directory.CreateDirectory(_imageFolderPath);
            Directory.CreateDirectory(_filePathsFolderPath);
            Directory.CreateDirectory(_filesCacheFolderPath);
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
                case ClipboardDataType.FilePaths:
                    await SaveFilePathsEntryAsync(entry);
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

        private async Task SaveFilePathsEntryAsync(ClipboardEntry entry)
        {
            if (entry.FilePaths == null || entry.FilePaths.Length == 0)
                return;

            // Save the file paths to a text file
            string filePathsFile = Path.Combine(_filePathsFolderPath, $"{entry.Id}.txt");
            await File.WriteAllLinesAsync(filePathsFile, entry.FilePaths);

            // Create a metadata file with timestamp
            string metadataPath = Path.Combine(_filePathsFolderPath, $"{entry.Id}.meta");
            await File.WriteAllTextAsync(metadataPath, entry.Timestamp.ToString("o"));

            // The caching of actual files will be handled by the application layer
            // since it has access to the settings
        }

        public async Task<ClipboardEntry> LoadClipboardEntryAsync(Guid id, ClipboardDataType dataType)
        {
            switch (dataType)
            {
                case ClipboardDataType.Text:
                    return await LoadTextEntryAsync(id);
                case ClipboardDataType.Image:
                    return await LoadImageEntryAsync(id);
                case ClipboardDataType.FilePaths:
                    return await LoadFilePathsEntryAsync(id);
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

        private async Task<ClipboardEntry> LoadFilePathsEntryAsync(Guid id)
        {
            string filePath = Path.Combine(_filePathsFolderPath, $"{id}.txt");
            string metadataPath = Path.Combine(_filePathsFolderPath, $"{id}.meta");

            if (!File.Exists(filePath) || !File.Exists(metadataPath))
                return null;

            string[] filePaths = await File.ReadAllLinesAsync(filePath);
            string timestampStr = await File.ReadAllTextAsync(metadataPath);
            DateTime timestamp = DateTime.Parse(timestampStr);

            return new ClipboardEntry
            {
                Id = id,
                Timestamp = timestamp,
                DataType = ClipboardDataType.FilePaths,
                FilePaths = filePaths
            };
        }

        public void CleanupOldEntries(int maxEntries)
        {
            // Implement cleanup logic to remove old entries when the number exceeds maxEntries
            // This would involve listing files, sorting by timestamp, and deleting the oldest ones
        }
    }
}
