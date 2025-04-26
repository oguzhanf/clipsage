using System;
using System.Linq;
using System.Threading.Tasks;
using Clipper.Core;
using Clipper.Core.Storage;
using LiteDB;
using Xunit;

namespace Clipper.Tests
{
    public class HistoryStoreTests
    {
        private HistoryStore CreateInMemoryStore()
        {
            return new HistoryStore();
        }

        [Fact]
        public async Task AddAsync_ShouldAddEntry()
        {
            var store = CreateInMemoryStore();
            var entry = new Clipper.Core.Storage.ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = Clipper.Core.Storage.ClipboardDataType.Text,
                PlainText = "Test Text"
            };

            await store.AddAsync(entry);
            var recent = await store.GetRecentAsync(1);

            Assert.Single(recent);
            Assert.Equal(entry.Id, recent.First().Id);
        }

        [Fact]
        public async Task GetRecentAsync_ShouldReturnLimitedEntries()
        {
            var store = CreateInMemoryStore();

            for (int i = 0; i < 10; i++)
            {
                await store.AddAsync(new Clipper.Core.Storage.ClipboardEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow.AddSeconds(i),
                    DataType = Clipper.Core.Storage.ClipboardDataType.Text,
                    PlainText = $"Entry {i}"
                });
            }

            var recent = await store.GetRecentAsync(5);

            Assert.Equal(5, recent.Count);
            Assert.Equal("Entry 9", recent.First().PlainText);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveEntry()
        {
            var store = CreateInMemoryStore();
            var entry = new Clipper.Core.Storage.ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = Clipper.Core.Storage.ClipboardDataType.Text,
                PlainText = "To Be Deleted"
            };

            await store.AddAsync(entry);
            await store.DeleteAsync(entry.Id);
            var recent = await store.GetRecentAsync(1);

            Assert.Empty(recent);
        }

        [Fact]
        public async Task PinAsync_ShouldUpdateTimestamp()
        {
            var store = CreateInMemoryStore();
            var entry = new Clipper.Core.Storage.ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DataType = Clipper.Core.Storage.ClipboardDataType.Text,
                PlainText = "To Be Pinned"
            };

            await store.AddAsync(entry);
            await store.PinAsync(entry.Id, true);
            var recent = await store.GetRecentAsync(1);

            Assert.Equal(DateTime.MaxValue, recent.First().Timestamp);
        }
    }
}
