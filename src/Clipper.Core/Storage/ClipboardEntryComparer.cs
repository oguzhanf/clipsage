using System;
using System.Linq;

namespace Clipper.Core.Storage
{
    /// <summary>
    /// Utility class for comparing clipboard entries to detect duplicates
    /// </summary>
    public static class ClipboardEntryComparer
    {
        /// <summary>
        /// Determines if two clipboard entries have the same content
        /// </summary>
        /// <param name="entry1">First clipboard entry</param>
        /// <param name="entry2">Second clipboard entry</param>
        /// <returns>True if the entries have the same content, false otherwise</returns>
        public static bool AreEntriesEqual(ClipboardEntry entry1, ClipboardEntry entry2)
        {
            // If entries are null or have different types, they're not equal
            if (entry1 == null || entry2 == null || entry1.DataType != entry2.DataType)
                return false;

            // Compare based on data type
            switch (entry1.DataType)
            {
                case ClipboardDataType.Text:
                    return AreTextEntriesEqual(entry1, entry2);
                case ClipboardDataType.Image:
                    return AreImageEntriesEqual(entry1, entry2);
                case ClipboardDataType.FilePaths:
                    return AreFilePathEntriesEqual(entry1, entry2);
                default:
                    return false;
            }
        }

        private static bool AreTextEntriesEqual(ClipboardEntry entry1, ClipboardEntry entry2)
        {
            // Compare text content
            return entry1.PlainText == entry2.PlainText;
        }

        private static bool AreImageEntriesEqual(ClipboardEntry entry1, ClipboardEntry entry2)
        {
            // For images, compare the byte arrays
            if (entry1.ImageBytes == null || entry2.ImageBytes == null)
                return false;

            if (entry1.ImageBytes.Length != entry2.ImageBytes.Length)
                return false;

            // Simple byte-by-byte comparison
            for (int i = 0; i < entry1.ImageBytes.Length; i++)
            {
                if (entry1.ImageBytes[i] != entry2.ImageBytes[i])
                    return false;
            }

            return true;
        }

        private static bool AreFilePathEntriesEqual(ClipboardEntry entry1, ClipboardEntry entry2)
        {
            // For file paths, compare the arrays
            if (entry1.FilePaths == null || entry2.FilePaths == null)
                return false;

            if (entry1.FilePaths.Length != entry2.FilePaths.Length)
                return false;

            // Sort the arrays to ensure consistent comparison
            var paths1 = entry1.FilePaths.OrderBy(p => p).ToArray();
            var paths2 = entry2.FilePaths.OrderBy(p => p).ToArray();

            // Compare each path
            for (int i = 0; i < paths1.Length; i++)
            {
                if (paths1[i] != paths2[i])
                    return false;
            }

            return true;
        }
    }
}
