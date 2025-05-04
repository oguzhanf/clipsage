using System;
using System.IO;
using System.Threading.Tasks;
using ClipSage.Core.Logging;

namespace ClipSage.Core.Storage
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

            // Check if each directory exists before creating it
            // This preserves any existing content in these folders
            if (!Directory.Exists(_textFolderPath))
                Directory.CreateDirectory(_textFolderPath);

            if (!Directory.Exists(_imageFolderPath))
                Directory.CreateDirectory(_imageFolderPath);

            if (!Directory.Exists(_filePathsFolderPath))
                Directory.CreateDirectory(_filePathsFolderPath);

            if (!Directory.Exists(_filesCacheFolderPath))
                Directory.CreateDirectory(_filesCacheFolderPath);

            // Log whether we're using existing folders or creating new ones
            LogFolderStatus();
        }

        /// <summary>
        /// Logs the status of the cache folders
        /// </summary>
        private void LogFolderStatus()
        {
            try
            {
                // Count files in each folder to determine if they existed before
                int textFiles = Directory.Exists(_textFolderPath) ? Directory.GetFiles(_textFolderPath).Length : 0;
                int imageFiles = Directory.Exists(_imageFolderPath) ? Directory.GetFiles(_imageFolderPath).Length : 0;
                int filePathsFiles = Directory.Exists(_filePathsFolderPath) ? Directory.GetFiles(_filePathsFolderPath).Length : 0;
                int cachedFiles = Directory.Exists(_filesCacheFolderPath) ?
                    Directory.GetDirectories(_filesCacheFolderPath).Length : 0;

                // Log the counts
                Console.WriteLine($"Cache folder status: Text={textFiles}, Images={imageFiles}, FilePaths={filePathsFiles}, CachedFiles={cachedFiles}");
                Logger.Instance.Info($"Cache folder status: Text={textFiles}, Images={imageFiles}, FilePaths={filePathsFiles}, CachedFiles={cachedFiles}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging folder status: {ex.Message}");
                Logger.Instance.Error("Error logging folder status", ex);
            }
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

        /// <summary>
        /// Deletes a clipboard entry from the file system
        /// </summary>
        /// <param name="id">The ID of the entry to delete</param>
        /// <param name="dataType">The type of data in the entry</param>
        public async Task DeleteClipboardEntryAsync(Guid id, ClipboardDataType dataType)
        {
            try
            {
                // Delete the files associated with this entry
                string folder;
                string extension;

                switch (dataType)
                {
                    case ClipboardDataType.Text:
                        folder = _textFolderPath;
                        extension = "txt";
                        break;
                    case ClipboardDataType.Image:
                        folder = _imageFolderPath;
                        extension = "png";
                        break;
                    case ClipboardDataType.FilePaths:
                        folder = _filePathsFolderPath;
                        extension = "txt";
                        break;
                    default:
                        folder = _textFolderPath;
                        extension = "txt";
                        break;
                }

                string filePath = Path.Combine(folder, $"{id}.{extension}");
                string metadataPath = Path.Combine(folder, $"{id}.meta");

                if (File.Exists(filePath))
                    File.Delete(filePath);

                if (File.Exists(metadataPath))
                    File.Delete(metadataPath);

                // If this was a file paths entry, also delete any cached files
                if (dataType == ClipboardDataType.FilePaths)
                {
                    string cacheFolder = Path.Combine(_filesCacheFolderPath, id.ToString());
                    if (Directory.Exists(cacheFolder))
                    {
                        Directory.Delete(cacheFolder, true);
                    }
                }

                await Task.CompletedTask; // To make this method async
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"Error deleting clipboard entry file for ID {id}", ex);
                throw;
            }
        }
    }
}
