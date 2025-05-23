using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipSage.Core.Logging;

namespace ClipSage.App
{
    // This is a wrapper class to use the ClipboardEntry from ClipSage.Core.Storage
    public class ClipboardEntryViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ClipSage.Core.Storage.ClipboardEntry _entry;
        private BitmapImage? _thumbnailImage;
        private bool _thumbnailGenerated = false;
        private bool _isHighlighted = false;
        private Timer? _highlightTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ClipboardEntryViewModel(ClipSage.Core.Storage.ClipboardEntry entry)
        {
            _entry = entry;
        }

        public Guid Id => _entry.Id;
        public DateTime Timestamp => _entry.Timestamp;
        public ClipSage.Core.Storage.ClipboardDataType DataType => _entry.DataType;
        public string? PlainText => _entry.PlainText;
        public byte[]? ImageBytes => _entry.ImageBytes;
        public string[]? FilePaths => _entry.FilePaths;
        public string? SourceFile => _entry.SourceFile;
        public string? ComputerName => _entry.ComputerName;

        /// <summary>
        /// Gets a formatted version of the computer name
        /// </summary>
        public string FormattedComputerName
        {
            get
            {
                if (string.IsNullOrEmpty(ComputerName))
                    return "Unknown Computer";

                // If it's the local computer, show "This Computer"
                if (ComputerName == Environment.MachineName)
                    return "This Computer";

                // Otherwise, show the actual computer name
                return ComputerName;
            }
        }

        /// <summary>
        /// Gets a human-readable string representing the age of this item
        /// </summary>
        public string ItemAge
        {
            get
            {
                var timeSpan = DateTime.UtcNow - Timestamp;

                if (timeSpan.TotalDays >= 1)
                {
                    int days = (int)timeSpan.TotalDays;
                    int hours = timeSpan.Hours;

                    if (hours > 0)
                        return $"{days} day{(days > 1 ? "s" : "")}, {hours} hour{(hours > 1 ? "s" : "")} ago";
                    else
                        return $"{days} day{(days > 1 ? "s" : "")} ago";
                }
                else if (timeSpan.TotalHours >= 1)
                {
                    int hours = (int)timeSpan.TotalHours;
                    int minutes = timeSpan.Minutes;

                    if (minutes > 0)
                        return $"{hours} hour{(hours > 1 ? "s" : "")}, {minutes} minute{(minutes > 1 ? "s" : "")} ago";
                    else
                        return $"{hours} hour{(hours > 1 ? "s" : "")} ago";
                }
                else if (timeSpan.TotalMinutes >= 1)
                {
                    int minutes = (int)timeSpan.TotalMinutes;
                    return $"{minutes} minute{(minutes > 1 ? "s" : "")} ago";
                }
                else
                {
                    return "Just now";
                }
            }
        }

        /// <summary>
        /// Gets a string indicating whether this item is from the local computer or another computer
        /// </summary>
        public string SourceComputer
        {
            get
            {
                if (string.IsNullOrEmpty(ComputerName))
                    return "Unknown Source";

                if (ComputerName == Environment.MachineName)
                    return "From This Computer";

                return $"From {ComputerName}";
            }
        }

        /// <summary>
        /// Gets a string describing the type of content
        /// </summary>
        public string ContentTypeDescription
        {
            get
            {
                return DataType switch
                {
                    ClipSage.Core.Storage.ClipboardDataType.Text => "Text Content",
                    ClipSage.Core.Storage.ClipboardDataType.Image => "Image Content",
                    ClipSage.Core.Storage.ClipboardDataType.FilePaths => FilePaths != null && FilePaths.Length > 1
                        ? $"{FilePaths.Length} Files"
                        : "File Path",
                    _ => "Unknown Content"
                };
            }
        }

        public ImageSource? ThumbnailImage
        {
            get
            {
                // Return cached thumbnail if already generated
                if (_thumbnailGenerated)
                {
                    return _thumbnailImage;
                }

                _thumbnailGenerated = true;

                try
                {
                    if (DataType == ClipSage.Core.Storage.ClipboardDataType.Image && ImageBytes != null && ImageBytes.Length > 0)
                    {
                        // Generate thumbnail from image bytes
                        using var stream = new MemoryStream(ImageBytes);
                        _thumbnailImage = new BitmapImage();
                        _thumbnailImage.BeginInit();
                        _thumbnailImage.StreamSource = stream;
                        _thumbnailImage.DecodePixelWidth = 64;  // Set width to 64px, height will scale proportionally
                        _thumbnailImage.CacheOption = BitmapCacheOption.OnLoad;
                        _thumbnailImage.EndInit();
                        _thumbnailImage.Freeze(); // Make it thread-safe
                        return _thumbnailImage;
                    }
                    else
                    {
                        // Return default icon based on data type
                        var resourceKey = DataType switch
                        {
                            ClipSage.Core.Storage.ClipboardDataType.Text => "TextDocumentIcon",
                            ClipSage.Core.Storage.ClipboardDataType.Image => "ImageIcon",
                            ClipSage.Core.Storage.ClipboardDataType.FilePaths => "FolderIcon",
                            _ => "UnknownIcon"
                        };

                        // Get the resource from the application resources
                        if (Application.Current != null)
                        {
                            return Application.Current.Resources[resourceKey] as DrawingImage;
                        }
                        else
                        {
                            // For unit testing when Application.Current is null
                            return null;
                        }
                    }
                }
                catch (Exception)
                {
                    // If there's an error, return a default icon
                    if (Application.Current != null)
                    {
                        return Application.Current.Resources["UnknownIcon"] as DrawingImage;
                    }
                    else
                    {
                        // For unit testing when Application.Current is null
                        return null;
                    }
                }
            }
        }

        public string DisplayText
        {
            get
            {
                if (DataType == ClipSage.Core.Storage.ClipboardDataType.Text && !string.IsNullOrEmpty(PlainText))
                {
                    // Truncate long text for display
                    var text = PlainText;
                    if (text.Length > 40)
                    {
                        text = text.Substring(0, 37) + "...";
                    }
                    return text;
                }
                else if (DataType == ClipSage.Core.Storage.ClipboardDataType.Image)
                {
                    return "[Image]";
                }
                else if (DataType == ClipSage.Core.Storage.ClipboardDataType.FilePaths && FilePaths != null && FilePaths.Length > 0)
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

        /// <summary>
        /// Gets or sets whether this entry is highlighted in the UI
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            private set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Highlights this entry in the UI for 5 seconds
        /// </summary>
        public void Highlight()
        {
            // If already highlighted, don't restart the timer
            if (IsHighlighted)
                return;

            // Set the highlighted state
            IsHighlighted = true;
            Logger.Instance.Debug($"Highlighted entry from {ComputerName}: {DisplayText}");

            // Start a timer to clear the highlight after 5 seconds
            _highlightTimer?.Dispose();
            _highlightTimer = new Timer(ClearHighlight, null, 5000, Timeout.Infinite);
        }

        /// <summary>
        /// Clears the highlight from this entry
        /// </summary>
        private void ClearHighlight(object? state)
        {
            // Use the dispatcher to update the UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsHighlighted = false;
                Logger.Instance.Debug($"Cleared highlight from entry: {DisplayText}");
            });

            // Dispose the timer
            _highlightTimer?.Dispose();
            _highlightTimer = null;
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            // Clean up resources
            _thumbnailImage = null;
            _highlightTimer?.Dispose();
            _highlightTimer = null;
        }
    }
}
