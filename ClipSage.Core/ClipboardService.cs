using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipSage.Core
{
    public class ClipboardService
    {
        private static readonly Lazy<ClipboardService> _instance = new(() => new ClipboardService());
        public static ClipboardService Instance => _instance.Value;

        public event EventHandler<ClipboardEntry>? ClipboardChanged;

        private ClipboardNotificationForm? _notificationForm;
        private Thread? _clipboardThread;
        private bool _isMonitoring = false;

        public bool IsMonitoring => _isMonitoring;

        private ClipboardService()
        {
            // Start monitoring by default
            StartMonitoring();
        }

        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            _clipboardThread = new Thread(() => {
                _notificationForm = new ClipboardNotificationForm(this);
                Application.Run(_notificationForm);
            });
            _clipboardThread.SetApartmentState(ApartmentState.STA);
            _clipboardThread.IsBackground = true;
            _clipboardThread.Start();

            _isMonitoring = true;
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            if (_notificationForm != null)
            {
                _notificationForm.BeginInvoke(new Action(() => {
                    _notificationForm.Close();
                }));
                _notificationForm = null;
            }

            _isMonitoring = false;
        }

        public void ToggleMonitoring()
        {
            if (_isMonitoring)
                StopMonitoring();
            else
                StartMonitoring();
        }

        public void OnClipboardChanged(ClipboardEntry entry)
        {
            ClipboardChanged?.Invoke(this, entry);
        }

        private class ClipboardNotificationForm : Form
        {
            private readonly ClipboardService _service;

            public ClipboardNotificationForm(ClipboardService service)
            {
                _service = service;

                // Make the form invisible and ensure it doesn't show in taskbar or alt-tab
                this.ShowInTaskbar = false;
                this.FormBorderStyle = FormBorderStyle.None;
                this.Opacity = 0;
                this.Size = new Size(0, 0);
                this.WindowState = FormWindowState.Minimized;

                // Set the form to be a tool window which further reduces its visibility
                this.ShowIcon = false;
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(-2000, -2000); // Position off-screen

                // Set window style to be a tool window
                // This prevents it from showing up in the alt+tab list
                SetToolWindow();

                NativeMethods.AddClipboardFormatListener(Handle);
            }

            // Set the window to be a tool window using Win32 API
            private void SetToolWindow()
            {
                // WS_EX_TOOLWINDOW = 0x00000080
                // This style prevents the window from appearing in the taskbar and alt+tab
                NativeMethods.SetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE,
                    NativeMethods.GetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE) | 0x00000080);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
                {
                    // Add a small delay to allow the source application to complete its clipboard operation
                    // This helps prevent clipboard contention with other applications
                    Task.Delay(75).ContinueWith(_ =>
                    {
                        try
                        {
                            // Use BeginInvoke to ensure clipboard access happens on the correct thread
                            this.BeginInvoke(new Action(() =>
                            {
                                var entry = GetClipboardEntry();
                                if (entry != null)
                                {
                                    _service.OnClipboardChanged(entry);
                                }

                                // Explicitly release the clipboard to ensure other applications can access it
                                NativeMethods.CloseClipboard();
                            }));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing clipboard update: {ex.Message}");
                        }
                    }, TaskScheduler.Default);
                }
                base.WndProc(ref m);
            }

            private ClipboardEntry? GetClipboardEntry()
            {
                try
                {
                    // Ensure we have proper access to the clipboard
                    // This helps prevent conflicts with other applications
                    if (!NativeMethods.OpenClipboard(this.Handle))
                    {
                        Console.WriteLine("Failed to open clipboard");
                        return null;
                    }

                    try
                    {
                        // Check for file paths first
                        if (Clipboard.ContainsFileDropList())
                        {
                            var fileDropList = Clipboard.GetFileDropList();
                            string[] filePaths = new string[fileDropList.Count];

                            for (int i = 0; i < fileDropList.Count; i++)
                            {
                                filePaths[i] = fileDropList[i] ?? string.Empty;
                            }

                            return new ClipboardEntry
                            {
                                Id = Guid.NewGuid(),
                                Timestamp = DateTime.UtcNow,
                                DataType = ClipboardDataType.FilePaths,
                                FilePaths = filePaths
                            };
                        }
                        // Check for text
                        else if (Clipboard.ContainsText())
                        {
                            // Windows Snipping Tool puts both text and image in clipboard
                            // If there's also an image, we'll handle it as an image to avoid duplication
                            if (Clipboard.ContainsImage())
                            {
                                string text = Clipboard.GetText();
                                // Check if this is a screenshot from Windows Snipping Tool
                                // The text is usually a file path to a temporary image
                                if (text.StartsWith("file:///") &&
                                    (text.EndsWith(".png") || text.EndsWith(".jpg") || text.EndsWith(".jpeg") ||
                                     text.EndsWith(".bmp") || text.EndsWith(".gif")))
                                {
                                    // This is likely a screenshot, so we'll handle it as an image
                                    using var image = Clipboard.GetImage();
                                    if (image != null)
                                    {
                                        using var stream = new System.IO.MemoryStream();
                                        image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                        return new ClipboardEntry
                                        {
                                            Id = Guid.NewGuid(),
                                            Timestamp = DateTime.UtcNow,
                                            DataType = ClipboardDataType.Image,
                                            ImageBytes = stream.ToArray()
                                        };
                                    }
                                    // If image is null, fall back to text
                                }
                            }

                            // Regular text
                            return new ClipboardEntry
                            {
                                Id = Guid.NewGuid(),
                                Timestamp = DateTime.UtcNow,
                                DataType = ClipboardDataType.Text,
                                PlainText = Clipboard.GetText()
                            };
                        }
                        // Check for image
                        else if (Clipboard.ContainsImage())
                        {
                            using var image = Clipboard.GetImage();
                            if (image != null)
                            {
                                using var stream = new System.IO.MemoryStream();
                                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                return new ClipboardEntry
                                {
                                    Id = Guid.NewGuid(),
                                    Timestamp = DateTime.UtcNow,
                                    DataType = ClipboardDataType.Image,
                                    ImageBytes = stream.ToArray()
                                };
                            }
                            // If image is null, return null
                        }
                    }
                    finally
                    {
                        // Always close the clipboard when done to ensure other applications can access it
                        NativeMethods.CloseClipboard();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting clipboard content: {ex.Message}");

                    // Make sure clipboard is closed even if an exception occurs
                    try { NativeMethods.CloseClipboard(); } catch { }
                }
                return null;
            }

            protected override void Dispose(bool disposing)
            {
                NativeMethods.RemoveClipboardFormatListener(Handle);
                base.Dispose(disposing);
            }
        }

        private static class NativeMethods
        {
            public const int WM_CLIPBOARDUPDATE = 0x031D;
            public const int GWL_EXSTYLE = -20;

            [DllImport("user32.dll")]
            public static extern bool AddClipboardFormatListener(IntPtr hwnd);

            [DllImport("user32.dll")]
            public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

            [DllImport("user32.dll")]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("user32.dll")]
            public static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [DllImport("user32.dll")]
            public static extern bool CloseClipboard();

            [DllImport("user32.dll")]
            public static extern bool EmptyClipboard();
        }
    }
}
