using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipSage.Core
{
    /// <summary>
    /// System-wide clipboard monitor.
    ///
    /// Design notes (read before editing — these are non-obvious):
    ///
    /// - We register a Vista+ AddClipboardFormatListener on a hidden message-only
    ///   form running on its own STA thread. The chain-style WM_DRAWCLIPBOARD model
    ///   is intentionally avoided (broken-chain risk).
    ///
    /// - Every <see cref="GetClipboardEntry"/> takes a SINGLE clipboard snapshot via
    ///   Clipboard.GetDataObject(). Earlier versions made 3–5 separate OpenClipboard
    ///   calls per event (one per Contains* and Get* call), which serialized cross-app
    ///   clipboard access and caused other apps' paste to fail under contention.
    ///
    /// - <see cref="MarkSelfWrite"/> is the way an in-app "copy" should announce its
    ///   own write. The next WM_CLIPBOARDUPDATE within <see cref="SelfWriteGraceMs"/>
    ///   is silently dropped. This replaces the previous Stop/Start-on-Copy dance,
    ///   which tore down and recreated the entire listener thread for every click.
    ///
    /// - Bursts of WM_CLIPBOARDUPDATE within <see cref="DebounceMs"/> are coalesced:
    ///   only the final state is read. This both reduces clipboard contention and
    ///   avoids capturing intermediate states from clipboard-chain apps.
    /// </summary>
    public class ClipboardService
    {
        // How long after a self-write to ignore incoming updates.
        private const int SelfWriteGraceMs = 700;

        // Coalesce updates that arrive within this window.
        private const int DebounceMs = 90;

        private static readonly Lazy<ClipboardService> _instance = new(() => new ClipboardService());
        public static ClipboardService Instance => _instance.Value;

        public event EventHandler<ClipboardEntry>? ClipboardChanged;

        private readonly object _lifecycleLock = new();
        private ClipboardNotificationForm? _notificationForm;
        private Thread? _clipboardThread;
        private volatile bool _isMonitoring;
        private long _selfWriteUtcTicks;

        public bool IsMonitoring => _isMonitoring;

        private ClipboardService()
        {
            StartMonitoring();
        }

        public void StartMonitoring()
        {
            lock (_lifecycleLock)
            {
                if (_isMonitoring)
                    return;

                var ready = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    var form = new ClipboardNotificationForm(this);
                    _notificationForm = form;
                    ready.Set();
                    Application.Run(form);
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Name = "ClipSage.ClipboardListener";
                thread.Start();

                // Block until the form's handle exists; otherwise a concurrent
                // StopMonitoring could try to BeginInvoke on a still-null form.
                ready.Wait();

                _clipboardThread = thread;
                _isMonitoring = true;
            }
        }

        public void StopMonitoring()
        {
            Thread? threadToJoin;
            ClipboardNotificationForm? form;

            lock (_lifecycleLock)
            {
                if (!_isMonitoring)
                    return;

                form = _notificationForm;
                threadToJoin = _clipboardThread;
                _notificationForm = null;
                _clipboardThread = null;
                _isMonitoring = false;
            }

            if (form != null && !form.IsDisposed)
            {
                try
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        try { form.Close(); } catch { /* already gone */ }
                    }));
                }
                catch { /* message loop already torn down */ }
            }

            // Wait for Application.Run to return so a follow-up StartMonitoring
            // doesn't race against the previous thread's listener registration.
            threadToJoin?.Join(500);
        }

        public void ToggleMonitoring()
        {
            if (_isMonitoring) StopMonitoring();
            else StartMonitoring();
        }

        /// <summary>
        /// Call this immediately before writing to the clipboard from inside the app
        /// (e.g., the "copy" button). The next clipboard-update notification that
        /// arrives within ~700 ms will be recognized as our own write and dropped,
        /// so monitoring doesn't need to be stopped.
        /// </summary>
        public void MarkSelfWrite()
        {
            Interlocked.Exchange(ref _selfWriteUtcTicks, DateTime.UtcNow.Ticks);
        }

        private bool ConsumeSelfWriteIfFresh()
        {
            var stamp = Interlocked.Read(ref _selfWriteUtcTicks);
            if (stamp == 0) return false;

            var ageMs = (DateTime.UtcNow - new DateTime(stamp, DateTimeKind.Utc)).TotalMilliseconds;
            if (ageMs < 0 || ageMs > SelfWriteGraceMs) return false;

            // Clear so subsequent external changes aren't accidentally muted.
            Interlocked.Exchange(ref _selfWriteUtcTicks, 0);
            return true;
        }

        public void OnClipboardChanged(ClipboardEntry entry)
        {
            ClipboardChanged?.Invoke(this, entry);
        }

        private sealed class ClipboardNotificationForm : Form
        {
            private const int WM_CLIPBOARDUPDATE = 0x031D;

            private readonly ClipboardService _service;
            private readonly System.Windows.Forms.Timer _debounceTimer;

            public ClipboardNotificationForm(ClipboardService service)
            {
                _service = service;

                // Hidden message-only form: invisible, no taskbar, no alt-tab.
                // ShowInTaskbar=false already excludes us from the taskbar; the
                // legacy SetWindowLong(WS_EX_TOOLWINDOW) dance is unnecessary.
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.None;
                Opacity = 0;
                Size = new Size(0, 0);
                WindowState = FormWindowState.Minimized;
                ShowIcon = false;
                StartPosition = FormStartPosition.Manual;
                Location = new Point(-2000, -2000);

                NativeMethods.AddClipboardFormatListener(Handle);

                _debounceTimer = new System.Windows.Forms.Timer { Interval = DebounceMs };
                _debounceTimer.Tick += OnDebounceTick;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_CLIPBOARDUPDATE)
                {
                    // If this update is from our own SetText/SetImage/etc., drop it
                    // without ever opening the clipboard.
                    if (_service.ConsumeSelfWriteIfFresh())
                    {
                        base.WndProc(ref m);
                        return;
                    }

                    // Restart the debounce timer; only the *last* update in a burst
                    // will trigger a clipboard read. This collapses rapid-fire
                    // notifications (e.g., from clipboard-chain managers) into one
                    // capture and keeps us off the clipboard most of the time.
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                }
                base.WndProc(ref m);
            }

            private void OnDebounceTick(object? sender, EventArgs e)
            {
                _debounceTimer.Stop();
                try
                {
                    var entry = ReadClipboardSnapshot();
                    if (entry != null)
                        _service.OnClipboardChanged(entry);
                }
                catch
                {
                    // Swallow read failures — a missed clip is better than a crash
                    // or holding the clipboard open during a retry storm.
                }
            }

            /// <summary>
            /// Take a single snapshot of the clipboard via GetDataObject and decide
            /// the entry type from it. This holds the clipboard open ONCE per event
            /// instead of 3–5 times like the previous implementation.
            /// </summary>
            private static ClipboardEntry? ReadClipboardSnapshot()
            {
                IDataObject? data;
                try
                {
                    data = Clipboard.GetDataObject();
                }
                catch (ExternalException)
                {
                    // CLIPBRD_E_CANT_OPEN — another app has the clipboard. Skip.
                    return null;
                }
                if (data == null) return null;

                bool hasFile  = data.GetDataPresent(DataFormats.FileDrop, false);
                bool hasText  = data.GetDataPresent(DataFormats.UnicodeText, false)
                             || data.GetDataPresent(DataFormats.Text, false);
                bool hasImage = data.GetDataPresent(DataFormats.Bitmap, false)
                             || data.GetDataPresent(DataFormats.Dib, false);

                if (hasFile)
                {
                    if (data.GetData(DataFormats.FileDrop, false) is string[] paths && paths.Length > 0)
                    {
                        return new ClipboardEntry
                        {
                            Id = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow,
                            DataType = ClipboardDataType.FilePaths,
                            FilePaths = paths
                        };
                    }
                }

                if (hasText)
                {
                    var text = (data.GetData(DataFormats.UnicodeText, false)
                             ?? data.GetData(DataFormats.Text, false)) as string;

                    // Snipping Tool quirk: it publishes both text (a file:/// URI to a
                    // temp PNG) AND image data. Prefer the image in that case.
                    if (hasImage && text != null && IsSnippingToolImageUri(text))
                    {
                        var image = TryReadImageBytes(data);
                        if (image != null)
                        {
                            return new ClipboardEntry
                            {
                                Id = Guid.NewGuid(),
                                Timestamp = DateTime.UtcNow,
                                DataType = ClipboardDataType.Image,
                                ImageBytes = image
                            };
                        }
                    }

                    if (text != null)
                    {
                        return new ClipboardEntry
                        {
                            Id = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow,
                            DataType = ClipboardDataType.Text,
                            PlainText = text
                        };
                    }
                }

                if (hasImage)
                {
                    var image = TryReadImageBytes(data);
                    if (image != null)
                    {
                        return new ClipboardEntry
                        {
                            Id = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow,
                            DataType = ClipboardDataType.Image,
                            ImageBytes = image
                        };
                    }
                }

                return null;
            }

            private static bool IsSnippingToolImageUri(string text)
            {
                if (!text.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)) return false;
                var lower = text.AsSpan().Trim();
                return lower.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || lower.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || lower.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                    || lower.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                    || lower.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
            }

            private static byte[]? TryReadImageBytes(IDataObject data)
            {
                try
                {
                    if (data.GetData(DataFormats.Bitmap, false) is Image img)
                    {
                        using var stream = new System.IO.MemoryStream();
                        img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        return stream.ToArray();
                    }
                }
                catch
                {
                    // Some clipboard publishers expose unstable image data.
                }
                return null;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { _debounceTimer.Stop(); _debounceTimer.Dispose(); } catch { }
                    try { NativeMethods.RemoveClipboardFormatListener(Handle); } catch { }
                }
                base.Dispose(disposing);
            }
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool AddClipboardFormatListener(IntPtr hwnd);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        }
    }
}
