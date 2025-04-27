using System;
using Clipper.Core.Storage;
using Xunit;

namespace Clipper.Tests
{
    public class ClipboardEntryComparerTests
    {
        [Fact]
        public void AreEntriesEqual_ShouldDetectDuplicateText()
        {
            var entry1 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test Text"
            };

            var entry2 = new ClipboardEntry
            {
                Id = Guid.NewGuid(), // Different ID
                Timestamp = DateTime.UtcNow.AddMinutes(1), // Different timestamp
                DataType = ClipboardDataType.Text,
                PlainText = "Test Text" // Same text
            };

            var entry3 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Different Text"
            };

            Assert.True(ClipboardEntryComparer.AreEntriesEqual(entry1, entry2));
            Assert.False(ClipboardEntryComparer.AreEntriesEqual(entry1, entry3));
        }

        [Fact]
        public void AreEntriesEqual_ShouldDetectDuplicateImage()
        {
            var imageBytes1 = new byte[] { 1, 2, 3, 4, 5 };
            var imageBytes2 = new byte[] { 1, 2, 3, 4, 5 }; // Same content
            var imageBytes3 = new byte[] { 5, 4, 3, 2, 1 }; // Different content

            var entry1 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Image,
                ImageBytes = imageBytes1
            };

            var entry2 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMinutes(1),
                DataType = ClipboardDataType.Image,
                ImageBytes = imageBytes2
            };

            var entry3 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Image,
                ImageBytes = imageBytes3
            };

            Assert.True(ClipboardEntryComparer.AreEntriesEqual(entry1, entry2));
            Assert.False(ClipboardEntryComparer.AreEntriesEqual(entry1, entry3));
        }

        [Fact]
        public void AreEntriesEqual_ShouldDetectDuplicateFilePaths()
        {
            var paths1 = new string[] { "C:\\path1.txt", "C:\\path2.txt" };
            var paths2 = new string[] { "C:\\path2.txt", "C:\\path1.txt" }; // Same paths, different order
            var paths3 = new string[] { "C:\\path1.txt", "C:\\path3.txt" }; // Different paths

            var entry1 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.FilePaths,
                FilePaths = paths1
            };

            var entry2 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMinutes(1),
                DataType = ClipboardDataType.FilePaths,
                FilePaths = paths2
            };

            var entry3 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.FilePaths,
                FilePaths = paths3
            };

            Assert.True(ClipboardEntryComparer.AreEntriesEqual(entry1, entry2));
            Assert.False(ClipboardEntryComparer.AreEntriesEqual(entry1, entry3));
        }

        [Fact]
        public void AreEntriesEqual_WithNullEntries_ShouldReturnFalse()
        {
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test Text"
            };

            Assert.False(ClipboardEntryComparer.AreEntriesEqual(null!, entry));
            Assert.False(ClipboardEntryComparer.AreEntriesEqual(entry, null!));
            Assert.False(ClipboardEntryComparer.AreEntriesEqual(null!, null!));
        }

        [Fact]
        public void AreEntriesEqual_WithDifferentDataTypes_ShouldReturnFalse()
        {
            var textEntry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test Text"
            };

            var imageEntry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Image,
                ImageBytes = new byte[] { 1, 2, 3, 4, 5 }
            };

            Assert.False(ClipboardEntryComparer.AreEntriesEqual(textEntry, imageEntry));
        }

        [Fact]
        public void AreEntriesEqual_WithNullContent_ShouldReturnFalse()
        {
            var entry1 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = null
            };

            var entry2 = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test Text"
            };

            Assert.False(ClipboardEntryComparer.AreEntriesEqual(entry1, entry2));
        }
    }
}
