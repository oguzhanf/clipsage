using System;

namespace ClipSage.Core
{
    /// <summary>
    /// Represents a clipboard entry
    /// </summary>
    public class ClipboardEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entry
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the timestamp when the entry was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the type of data in the entry
        /// </summary>
        public ClipboardDataType DataType { get; set; }

        /// <summary>
        /// Gets or sets the plain text content (for text entries)
        /// </summary>
        public string? PlainText { get; set; }

        /// <summary>
        /// Gets or sets the image bytes (for image entries)
        /// </summary>
        public byte[]? ImageBytes { get; set; }

        /// <summary>
        /// Gets or sets the file paths (for file entries)
        /// </summary>
        public string[]? FilePaths { get; set; }
    }
}
