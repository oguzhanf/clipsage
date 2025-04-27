using System;
using System.Windows;
using MahApps.Metro.Controls;

namespace Clipper.App
{
    /// <summary>
    /// Interaction logic for ProgressDialog.xaml
    /// </summary>
    public partial class ProgressDialog : MetroWindow
    {
        private bool _isCancelled = false;

        public ProgressDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the message displayed in the dialog
        /// </summary>
        public string Message
        {
            get => MessageTextBlock.Text;
            set => MessageTextBlock.Text = value;
        }

        /// <summary>
        /// Gets or sets the progress value (0.0 to 1.0)
        /// </summary>
        public double Progress
        {
            get => ProgressBar.Value;
            set => ProgressBar.Value = value;
        }

        /// <summary>
        /// Gets or sets whether the progress bar is indeterminate
        /// </summary>
        public bool IsIndeterminate
        {
            get => ProgressBar.IsIndeterminate;
            set => ProgressBar.IsIndeterminate = value;
        }

        /// <summary>
        /// Gets whether the operation was cancelled
        /// </summary>
        public bool IsCancelled => _isCancelled;

        /// <summary>
        /// Event raised when the cancel button is clicked
        /// </summary>
        public event EventHandler? Cancelled;

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _isCancelled = true;
            Cancelled?.Invoke(this, EventArgs.Empty);
            Close();
        }
    }
}
