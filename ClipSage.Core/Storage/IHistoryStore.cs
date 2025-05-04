using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClipSage.Core.Storage
{
    /// <summary>
    /// Interface for clipboard history storage implementations
    /// </summary>
    public interface IHistoryStore
    {
        /// <summary>
        /// Adds a new entry to the history
        /// </summary>
        /// <param name="entry">The entry to add</param>
        Task AddAsync(ClipboardEntry entry);

        /// <summary>
        /// Gets the most recent entries from the history
        /// </summary>
        /// <param name="limit">The maximum number of entries to return</param>
        /// <returns>A list of clipboard entries</returns>
        Task<List<ClipboardEntry>> GetRecentAsync(int limit);

        /// <summary>
        /// Deletes an entry from the history
        /// </summary>
        /// <param name="id">The ID of the entry to delete</param>
        Task DeleteAsync(Guid id);

        /// <summary>
        /// Pins or unpins an entry in the history
        /// </summary>
        /// <param name="id">The ID of the entry to pin/unpin</param>
        /// <param name="isPinned">True to pin, false to unpin</param>
        Task PinAsync(Guid id, bool isPinned);

        /// <summary>
        /// Removes duplicate entries from the history
        /// </summary>
        /// <returns>The number of duplicates removed</returns>
        Task<int> CleanupDuplicatesAsync();
    }
}
