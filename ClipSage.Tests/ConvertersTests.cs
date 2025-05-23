using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ClipSage.App.Converters;
using ClipSage.Core;
using Xunit;

namespace ClipSage.Tests
{
    public class ConvertersTests
    {
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        [Fact]
        public void StringToVisibilityConverter_EmptyString_ShouldReturnVisible()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            var result = converter.Convert(string.Empty, typeof(Visibility), parameter: null, _culture);

            // Assert
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void StringToVisibilityConverter_NullString_ShouldReturnVisible()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            var result = converter.Convert(null, typeof(Visibility), parameter: null, _culture);

            // Assert
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void StringToVisibilityConverter_NonEmptyString_ShouldReturnCollapsed()
        {
            // Arrange
            var converter = new StringToVisibilityConverter();

            // Act
            var result = converter.Convert("Test", typeof(Visibility), parameter: null, _culture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void NullToBooleanConverter_NullValue_ShouldReturnFalse()
        {
            // Arrange
            var converter = new NullToBooleanConverter();

            // Act
            var result = converter.Convert(null, typeof(bool), parameter: null, _culture);

            // Assert
            Assert.False((bool)result);
        }

        [Fact]
        public void NullToBooleanConverter_NonNullValue_ShouldReturnTrue()
        {
            // Arrange
            var converter = new NullToBooleanConverter();

            // Act
            var result = converter.Convert("Test", typeof(bool), parameter: null, _culture);

            // Assert
            Assert.True((bool)result);
        }

        [Fact]
        public void DataTypeToColorConverter_TextType_ShouldReturnBrush()
        {
            // Arrange
            var converter = new DataTypeToColorConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.Text, typeof(Brush), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
        }

        [Fact]
        public void DataTypeToColorConverter_ImageType_ShouldReturnBrush()
        {
            // Arrange
            var converter = new DataTypeToColorConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.Image, typeof(Brush), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
        }

        [Fact]
        public void DataTypeToColorConverter_FilePathsType_ShouldReturnBrush()
        {
            // Arrange
            var converter = new DataTypeToColorConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.FilePaths, typeof(Brush), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
        }

        [Fact]
        public void DataTypeToIconConverter_TextType_ShouldReturnDrawingImage()
        {
            // Arrange
            var converter = new DataTypeToIconConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.Text, typeof(DrawingImage), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DrawingImage>(result);
        }

        [Fact]
        public void DataTypeToIconConverter_ImageType_ShouldReturnDrawingImage()
        {
            // Arrange
            var converter = new DataTypeToIconConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.Image, typeof(DrawingImage), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DrawingImage>(result);
        }

        [Fact]
        public void DataTypeToIconConverter_FilePathsType_ShouldReturnDrawingImage()
        {
            // Arrange
            var converter = new DataTypeToIconConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.FilePaths, typeof(DrawingImage), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DrawingImage>(result);
        }
        [Fact]
        public void DataTypeToLabelConverter_TextType_ShouldReturnExpectedLabel()
        {
            // Arrange
            var converter = new DataTypeToLabelConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.Text, typeof(string), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<string>(result);
            Assert.Equal("TXT", result);
        }

        [Fact]
        public void DataTypeToLabelConverter_ImageType_ShouldReturnExpectedLabel()
        {
            // Arrange
            var converter = new DataTypeToLabelConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.Image, typeof(string), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<string>(result);
            Assert.Equal("IMG", result);
        }

        [Fact]
        public void DataTypeToLabelConverter_FilePathsType_ShouldReturnExpectedLabel()
        {
            // Arrange
            var converter = new DataTypeToLabelConverter();

            // Act
            var result = converter.Convert(ClipboardDataType.FilePaths, typeof(string), parameter: null, _culture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<string>(result);
            Assert.Equal("FILE", result);
        }
    }
}
