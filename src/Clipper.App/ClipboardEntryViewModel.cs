using System;

namespace Clipper.App
{
    // This is a wrapper class to use the ClipboardEntry from Clipper.Core.Storage
    public class ClipboardEntryViewModel
    {
        private readonly Clipper.Core.Storage.ClipboardEntry _entry;

        public ClipboardEntryViewModel(Clipper.Core.Storage.ClipboardEntry entry)
        {
            _entry = entry;
        }

        public Guid Id => _entry.Id;
        public DateTime Timestamp => _entry.Timestamp;
        public Clipper.Core.Storage.ClipboardDataType DataType => _entry.DataType;
        public string? PlainText => _entry.PlainText;
        public byte[]? ImageBytes => _entry.ImageBytes;

        public string[] FilePaths => _entry.FilePaths;

        public string DisplayText
        {
            get
            {
                if (DataType == Clipper.Core.Storage.ClipboardDataType.Text && !string.IsNullOrEmpty(PlainText))
                {
                    // Truncate long text for display
                    var text = PlainText;
                    if (text.Length > 40)
                    {
                        text = text.Substring(0, 37) + "...";
                    }
                    return text;
                }
                else if (DataType == Clipper.Core.Storage.ClipboardDataType.Image)
                {
                    return "[Image]";
                }
                else if (DataType == Clipper.Core.Storage.ClipboardDataType.FilePaths && FilePaths != null && FilePaths.Length > 0)
                {
                    if (FilePaths.Length == 1)
                    {
                        string fileName = System.IO.Path.GetFileName(FilePaths[0]);
                        return $"[File: {fileName}]";
                    }
                    else
                    {
                        return $"[{FilePaths.Length} files]";
                    }
                }
                return "[Empty]";
            }
        }
    }
}
