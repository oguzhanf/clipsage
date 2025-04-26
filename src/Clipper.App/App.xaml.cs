using System;
using System.Configuration;
using System.Data;
using System.Windows;
using Clipper.App.Properties;

namespace Clipper.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if we should start minimized
            // For now, we'll just use hardcoded values
            bool startMinimized = false;
            bool minimizeToTray = true;

            if (startMinimized)
            {
                MainWindow.WindowState = WindowState.Minimized;

                if (minimizeToTray)
                {
                    // Hide the main window
                    MainWindow.Hide();
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Save any application settings
            // We'll implement this later

            base.OnExit(e);
        }
    }
}

