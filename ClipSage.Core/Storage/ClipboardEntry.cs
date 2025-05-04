using System;

namespace ClipSage.Core.Storage
{
    /// <summary>
    /// Represents a clipboard entry
    /// </summary>
    [Serializable]
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

        /// <summary>
        /// Gets or sets the source file for this entry (used for multi-system support)
        /// </summary>
        public string? SourceFile { get; set; }

        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        public ClipboardEntry()
        {
        }

        /// <summary>
        /// Creates a deep copy of this clipboard entry
        /// </summary>
        /// <returns>A new clipboard entry with the same values</returns>
        public ClipboardEntry Clone()
        {
            var clone = new ClipboardEntry
            {
                Id = this.Id,
                Timestamp = this.Timestamp,
                DataType = this.DataType,
                PlainText = this.PlainText,
                SourceFile = this.SourceFile
            };

            // Deep copy image bytes
            if (this.ImageBytes != null)
            {
                clone.ImageBytes = new byte[this.ImageBytes.Length];
                Array.Copy(this.ImageBytes, clone.ImageBytes, this.ImageBytes.Length);
            }

            // Deep copy file paths
            if (this.FilePaths != null)
            {
                clone.FilePaths = new string[this.FilePaths.Length];
                Array.Copy(this.FilePaths, clone.FilePaths, this.FilePaths.Length);
            }

            return clone;
        }
    }

    /// <summary>
    /// Defines the types of data that can be stored in a clipboard entry
    /// </summary>
    public enum ClipboardDataType
    {
        /// <summary>
        /// Plain text content
        /// </summary>
        Text,

        /// <summary>
        /// Image content
        /// </summary>
        Image,

        /// <summary>
        /// File paths
        /// </summary>
        FilePaths
    }
}
