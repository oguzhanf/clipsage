using System;
using System.IO;
using System.Threading.Tasks;
using Clipper.Core.Storage;
using Xunit;

namespace Clipper.Tests
{
    public class FileBasedClipboardStoreTests
    {
        [Fact]
        public async Task SaveClipboardEntryAsync_TextEntry_ShouldSaveToFile()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), "ClipperTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            try
            {
                var store = new FileBasedClipboardStore(tempPath);
                var entry = new ClipboardEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    DataType = ClipboardDataType.Text,
                    PlainText = "Test text content"
                };

                // Act
                await store.SaveClipboardEntryAsync(entry);

                // Assert
                string textFilePath = Path.Combine(tempPath, "Text", $"{entry.Id}.txt");
                string metadataPath = Path.Combine(tempPath, "Text", $"{entry.Id}.meta");

                Assert.True(File.Exists(textFilePath), "Text file should exist");
                Assert.True(File.Exists(metadataPath), "Metadata file should exist");

                string savedContent = await File.ReadAllTextAsync(textFilePath);
                Assert.Equal(entry.PlainText, savedContent);
            }
            finally
            {
                // Cleanup
                try
                {
                    Directory.Delete(tempPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task SaveClipboardEntryAsync_ImageEntry_ShouldSaveToFile()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), "ClipperTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            try
            {
                var store = new FileBasedClipboardStore(tempPath);
                var imageBytes = new byte[] { 1, 2, 3, 4, 5 };
                var entry = new ClipboardEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    DataType = ClipboardDataType.Image,
                    ImageBytes = imageBytes
                };

                // Act
                await store.SaveClipboardEntryAsync(entry);

                // Assert
                string imageFilePath = Path.Combine(tempPath, "Images", $"{entry.Id}.png");
                string metadataPath = Path.Combine(tempPath, "Images", $"{entry.Id}.meta");

                Assert.True(File.Exists(imageFilePath), "Image file should exist");
                Assert.True(File.Exists(metadataPath), "Metadata file should exist");

                byte[] savedContent = await File.ReadAllBytesAsync(imageFilePath);
                Assert.Equal(imageBytes, savedContent);
            }
            finally
            {
                // Cleanup
                try
                {
                    Directory.Delete(tempPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task SaveClipboardEntryAsync_FilePathsEntry_ShouldSaveToFile()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), "ClipperTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            try
            {
                var store = new FileBasedClipboardStore(tempPath);
                var filePaths = new string[] { "C:\\path1.txt", "C:\\path2.txt" };
                var entry = new ClipboardEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    DataType = ClipboardDataType.FilePaths,
                    FilePaths = filePaths
                };

                // Act
                await store.SaveClipboardEntryAsync(entry);

                // Assert
                string filePathsFilePath = Path.Combine(tempPath, "FilePaths", $"{entry.Id}.txt");
                string metadataPath = Path.Combine(tempPath, "FilePaths", $"{entry.Id}.meta");

                Assert.True(File.Exists(filePathsFilePath), "FilePaths file should exist");
                Assert.True(File.Exists(metadataPath), "Metadata file should exist");

                string[] savedContent = await File.ReadAllLinesAsync(filePathsFilePath);
                Assert.Equal(filePaths, savedContent);
            }
            finally
            {
                // Cleanup
                try
                {
                    Directory.Delete(tempPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void Constructor_WithNullPath_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FileBasedClipboardStore(null!));
        }

        [Fact]
        public void Constructor_ShouldCreateFolderStructure()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), "ClipperTest_" + Guid.NewGuid().ToString());

            try
            {
                // Act
                var store = new FileBasedClipboardStore(tempPath);

                // Assert
                Assert.True(Directory.Exists(tempPath), "Base directory should exist");
                Assert.True(Directory.Exists(Path.Combine(tempPath, "Text")), "Text directory should exist");
                Assert.True(Directory.Exists(Path.Combine(tempPath, "Images")), "Images directory should exist");
                Assert.True(Directory.Exists(Path.Combine(tempPath, "FilePaths")), "FilePaths directory should exist");
            }
            finally
            {
                // Cleanup
                try
                {
                    Directory.Delete(tempPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
