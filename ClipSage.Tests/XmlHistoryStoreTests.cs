using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClipSage.Core.Storage;
using Xunit;

namespace ClipSage.Tests
{
    public class XmlHistoryStoreTests : IDisposable
    {
        private readonly string _testCacheFolder;
        private readonly XmlHistoryStore _store;

        public XmlHistoryStoreTests()
        {
            // Create a temporary folder for testing
            _testCacheFolder = Path.Combine(Path.GetTempPath(), "ClipSageTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testCacheFolder);
            _store = new XmlHistoryStore(_testCacheFolder);
        }

        [Fact]
        public async Task AddAsync_ShouldAddEntry()
        {
            // Arrange
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text"
            };

            // Act
            await _store.AddAsync(entry);

            // Assert
            var entries = await _store.GetRecentAsync(10);
            Assert.Single(entries);
            Assert.Equal(entry.Id, entries[0].Id);
            Assert.Equal(entry.PlainText, entries[0].PlainText);
        }

        [Fact]
        public async Task GetRecentAsync_ShouldReturnLimitedEntries()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                var entry = new ClipboardEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow.AddMinutes(-i), // Older entries have earlier timestamps
                    DataType = ClipboardDataType.Text,
                    PlainText = $"Test text {i}"
                };
                await _store.AddAsync(entry);
            }

            // Act
            var entries = await _store.GetRecentAsync(5);

            // Assert
            Assert.Equal(5, entries.Count);
            // Entries should be sorted by timestamp (newest first)
            for (int i = 0; i < entries.Count - 1; i++)
            {
                Assert.True(entries[i].Timestamp >= entries[i + 1].Timestamp);
            }
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveEntry()
        {
            // Arrange
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text"
            };
            await _store.AddAsync(entry);

            // Act
            await _store.DeleteAsync(entry.Id);

            // Assert
            var entries = await _store.GetRecentAsync(10);
            Assert.Empty(entries);
        }

        [Fact]
        public async Task PinAsync_ShouldUpdateTimestamp()
        {
            // Arrange
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text"
            };
            await _store.AddAsync(entry);

            // Act
            await _store.PinAsync(entry.Id, true);

            // Assert
            var entries = await _store.GetRecentAsync(10);
            Assert.Single(entries);
            Assert.Equal(DateTime.MaxValue, entries[0].Timestamp);
        }

        [Fact]
        public async Task IsDuplicateAsync_ShouldDetectDuplicateText()
        {
            // Arrange
            var entry1 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text"
            };
            await _store.AddAsync(entry1);

            var entry2 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text" // Same text as entry1
            };

            // Act & Assert
            Assert.True(await _store.IsDuplicateAsync(entry2));
        }

        [Fact]
        public async Task CleanupDuplicatesAsync_ShouldRemoveDuplicates()
        {
            // Arrange
            var entry1 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text"
            };
            await _store.AddAsync(entry1);

            // Add a duplicate entry with a different ID
            var entry2 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMinutes(1), // Newer timestamp
                DataType = ClipboardDataType.Text,
                PlainText = "Test text" // Same text as entry1
            };

            // Bypass duplicate detection to add the duplicate
            var privateStore = _store.GetType().GetField("_inMemoryHistory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (privateStore != null)
            {
                var inMemoryHistory = (System.Collections.Generic.List<ClipboardEntry>?)privateStore.GetValue(_store);
                if (inMemoryHistory != null)
                {
                    inMemoryHistory.Add(entry2);
                }
            }

            // Act
            int removedCount = await _store.CleanupDuplicatesAsync();

            // Assert
            Assert.Equal(1, removedCount);
            var entries = await _store.GetRecentAsync(10);
            Assert.Single(entries);
            // The newer entry should be kept
            Assert.Equal(entry2.Id, entries[0].Id);
        }

        public void Dispose()
        {
            // Clean up the test folder
            if (_store is IDisposable disposable)
            {
                disposable.Dispose();
            }

            try
            {
                if (Directory.Exists(_testCacheFolder))
                {
                    Directory.Delete(_testCacheFolder, true);
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
