using System;
using System.ComponentModel;
using Clipper.App;
using Xunit;

namespace Clipper.Tests
{
    public class SettingsViewModelTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Act
            var viewModel = new SettingsViewModel();

            // Assert
            Assert.NotNull(viewModel);
            // Check default values match what's expected
            Assert.False(viewModel.StartWithWindows);
            Assert.False(viewModel.StartMinimized);
            Assert.True(viewModel.MinimizeToTray);
            Assert.Equal(100, viewModel.MaxHistorySize);
            Assert.True(viewModel.IgnoreDuplicates);
            Assert.True(viewModel.TruncateLargeText);
            Assert.Equal(100, viewModel.MaxTextLength);
            Assert.True(viewModel.IgnoreLargeImages);
            Assert.Equal(5, viewModel.MaxImageSize);
            Assert.NotNull(viewModel.CachingFolder);
        }

        [Fact]
        public void PropertyChanged_ShouldRaiseEvent()
        {
            // Arrange
            var viewModel = new SettingsViewModel();
            var propertyChangedRaised = false;
            var propertyName = string.Empty;

            viewModel.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
                propertyName = e.PropertyName;
            };

            // Act
            viewModel.StartWithWindows = true;

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal(nameof(SettingsViewModel.StartWithWindows), propertyName);
        }

        // Note: We can't directly test the SaveSettings method because the Settings class is internal
        // and not accessible from the test project. Instead, we'll test that the ViewModel properties
        // can be set and retrieved correctly.
        [Fact]
        public void SettingsProperties_ShouldBeSetAndRetrieved()
        {
            // Arrange
            var viewModel = new SettingsViewModel();

            // Act - Set some values
            viewModel.StartWithWindows = true;
            viewModel.StartMinimized = true;
            viewModel.MinimizeToTray = false;
            viewModel.MaxHistorySize = 200;
            viewModel.IgnoreDuplicates = false;
            viewModel.TruncateLargeText = false;
            viewModel.MaxTextLength = 200;
            viewModel.IgnoreLargeImages = false;
            viewModel.MaxImageSize = 10;
            viewModel.CachingFolder = @"C:\TestFolder";
            viewModel.CachingFolderConfigured = true;
            viewModel.CacheFiles = true;
            viewModel.MaxFileCacheSize = 100;

            // Assert - Check that the properties were updated
            Assert.True(viewModel.StartWithWindows);
            Assert.True(viewModel.StartMinimized);
            Assert.False(viewModel.MinimizeToTray);
            Assert.Equal(200, viewModel.MaxHistorySize);
            Assert.False(viewModel.IgnoreDuplicates);
            Assert.False(viewModel.TruncateLargeText);
            Assert.Equal(200, viewModel.MaxTextLength);
            Assert.False(viewModel.IgnoreLargeImages);
            Assert.Equal(10, viewModel.MaxImageSize);
            Assert.Equal(@"C:\TestFolder", viewModel.CachingFolder);
            Assert.True(viewModel.CachingFolderConfigured);
            Assert.True(viewModel.CacheFiles);
            Assert.Equal(100, viewModel.MaxFileCacheSize);
        }
    }
}
