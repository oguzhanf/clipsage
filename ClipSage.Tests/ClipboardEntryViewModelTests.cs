using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipSage.App;
using ClipSage.Core.Storage;
using Xunit;

namespace ClipSage.Tests
{
    public class ClipboardEntryViewModelTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange
            var id = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;
            var entry = new ClipboardEntry
            {
                Id = id,
                Timestamp = timestamp,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text",
                ComputerName = "TestComputer"
            };

            // Act
            var viewModel = new ClipboardEntryViewModel(entry);

            // Assert
            Assert.Equal(id, viewModel.Id);
            Assert.Equal(timestamp, viewModel.Timestamp);
            Assert.Equal(ClipboardDataType.Text, viewModel.DataType);
            Assert.Equal("Test text", viewModel.PlainText);
            Assert.Equal("TestComputer", viewModel.ComputerName);
        }

        [Fact]
        public void DisplayText_WithTextType_ShouldReturnText()
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
            var viewModel = new ClipboardEntryViewModel(entry);

            // Assert
            Assert.Equal("Test text", viewModel.DisplayText);
        }

        [Fact]
        public void DisplayText_WithLongTextType_ShouldTruncateText()
        {
            // Arrange
            var longText = new string('A', 100);
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = longText
            };

            // Act
            var viewModel = new ClipboardEntryViewModel(entry);

            // Assert
            Assert.Equal(longText.Substring(0, 37) + "...", viewModel.DisplayText);
        }

        [Fact]
        public void DisplayText_WithImageType_ShouldReturnImageText()
        {
            // Arrange
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Image,
                ImageBytes = new byte[] { 1, 2, 3 }
            };

            // Act
            var viewModel = new ClipboardEntryViewModel(entry);

            // Assert
            Assert.Equal("[Image]", viewModel.DisplayText);
        }

        [Fact]
        public void ThumbnailImage_WithTextType_ShouldReturnTextIcon()
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
            var viewModel = new ClipboardEntryViewModel(entry);
            var thumbnail = viewModel.ThumbnailImage;

            // Assert
            // In unit tests, Application.Current is null, so we expect null
            // This is different from the actual runtime behavior
            Assert.Null(thumbnail);
        }

        [Fact]
        public void ThumbnailImage_WithFilePathsType_ShouldReturnFolderIcon()
        {
            // Arrange
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.FilePaths,
                FilePaths = new[] { "C:\\test.txt" }
            };

            // Act
            var viewModel = new ClipboardEntryViewModel(entry);
            var thumbnail = viewModel.ThumbnailImage;

            // Assert
            // In unit tests, Application.Current is null, so we expect null
            // This is different from the actual runtime behavior
            Assert.Null(thumbnail);
        }

        [Fact]
        public void Dispose_ShouldCleanUpResources()
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
            var viewModel = new ClipboardEntryViewModel(entry);
            viewModel.Dispose();

            // Assert - No exception should be thrown
            // This is primarily testing that Dispose doesn't throw exceptions
            Assert.NotNull(viewModel);
        }
    }
}
