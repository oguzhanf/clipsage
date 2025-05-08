using System;
using System.Threading;
using System.Threading.Tasks;
using ClipSage.Core;
using Xunit;

namespace ClipSage.Tests
{
    public class ClipboardServiceTests
    {
        [Fact]
        public void Instance_ShouldReturnSingletonInstance()
        {
            // Act
            var instance1 = ClipboardService.Instance;
            var instance2 = ClipboardService.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void OnClipboardChanged_ShouldRaiseEvent()
        {
            // Arrange
            var service = ClipboardService.Instance;
            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text"
            };

            ClipboardEntry? receivedEntry = null;
            var eventRaised = new ManualResetEvent(false);

            service.ClipboardChanged += (sender, e) =>
            {
                receivedEntry = e;
                eventRaised.Set();
            };

            // Act
            service.OnClipboardChanged(entry);

            // Assert
            bool wasSignaled = eventRaised.WaitOne(TimeSpan.FromSeconds(1));
            Assert.True(wasSignaled, "Event should be raised");
            Assert.NotNull(receivedEntry);
            Assert.Equal(entry.Id, receivedEntry.Id);
            Assert.Equal(entry.PlainText, receivedEntry.PlainText);
            Assert.Equal(entry.DataType, receivedEntry.DataType);
        }

        [Fact]
        public void OnClipboardChanged_WithNoSubscribers_ShouldNotThrow()
        {
            // Arrange
            var service = ClipboardService.Instance;

            // Remove all event handlers
            var field = typeof(ClipboardService).GetField("ClipboardChanged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (field != null)
            {
                field.SetValue(service, null);
            }

            var entry = new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = ClipboardDataType.Text,
                PlainText = "Test text"
            };

            // Act & Assert
            var exception = Record.Exception(() => service.OnClipboardChanged(entry));
            Assert.Null(exception);
        }
    }
}
