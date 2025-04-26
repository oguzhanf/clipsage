using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Clipper.Core
{
    public class ClipboardService
    {
        private static readonly Lazy<ClipboardService> _instance = new(() => new ClipboardService());
        public static ClipboardService Instance => _instance.Value;

        public event EventHandler<ClipboardEntry> ClipboardChanged;

        private ClipboardService()
        {
            var thread = new Thread(() => {
                Application.Run(new ClipboardNotificationForm(this));
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
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
                    var entry = GetClipboardEntry();
                    if (entry != null)
                    {
                        _service.OnClipboardChanged(entry);
                    }
                }
                base.WndProc(ref m);
            }

            private ClipboardEntry GetClipboardEntry()
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        return new ClipboardEntry
                        {
                            Id = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow,
                            DataType = ClipboardDataType.Text,
                            PlainText = Clipboard.GetText()
                        };
                    }
                    else if (Clipboard.ContainsImage())
                    {
                        using var image = Clipboard.GetImage();
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
                }
                catch (Exception ex)
                {
                    // Log or handle error
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
        }
    }

    public class ClipboardEntry
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public ClipboardDataType DataType { get; set; }
        public string PlainText { get; set; }
        public byte[] ImageBytes { get; set; }
    }

    public enum ClipboardDataType
    {
        Text,
        Image
    }
}
