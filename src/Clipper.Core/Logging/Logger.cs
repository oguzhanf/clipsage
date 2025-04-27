using System;
using System.IO;
using System.Net;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Clipper.Core.Logging
{
    public class Logger
    {
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        public static Logger Instance => _instance.Value;

        private NLog.Logger? _logger;
        private string? _logFolderPath;
        private bool _isInitialized = false;
        private readonly string _machineName;

        private Logger()
        {
            // Get the machine name for logging
            _machineName = Environment.MachineName;
        }

        /// <summary>
        /// Initializes the logger with the specified cache folder path.
        /// </summary>
        /// <param name="cacheFolderPath">The path to the cache folder.</param>
        public void Initialize(string cacheFolderPath)
        {
            if (_isInitialized)
                return;

            if (string.IsNullOrEmpty(cacheFolderPath))
                return;

            try
            {
                // Create Logs subfolder
                _logFolderPath = Path.Combine(cacheFolderPath, "Logs");
                Directory.CreateDirectory(_logFolderPath);

                // Configure NLog
                var config = new LoggingConfiguration();

                // Create file target with daily archiving
                var fileTarget = new FileTarget("file")
                {
                    FileName = Path.Combine(_logFolderPath, "clipper-${shortdate}.log"),
                    Layout = "${longdate} [${level:uppercase=true}] [${machinename}] ${message} ${exception:format=tostring}",
                    ArchiveFileName = Path.Combine(_logFolderPath, "archive", "clipper-{#}.log"),
                    ArchiveEvery = FileArchivePeriod.Day,
                    ArchiveNumbering = ArchiveNumberingMode.Date,
                    MaxArchiveFiles = 30, // Keep logs for 30 days
                    ArchiveDateFormat = "yyyy-MM-dd",
                    CreateDirs = true
                };

                config.AddTarget(fileTarget);
                config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);

                // Apply config
                LogManager.Configuration = config;

                // Get logger
                _logger = LogManager.GetCurrentClassLogger();
                _isInitialized = true;

                // Log initialization
                Info($"Logger initialized. Machine: {_machineName}, Log folder: {_logFolderPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Info(string message)
        {
            if (!_isInitialized || _logger == null)
                return;

            _logger.Info(message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Warning(string message)
        {
            if (!_isInitialized || _logger == null)
                return;

            _logger.Warn(message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception associated with the error.</param>
        public void Error(string message, Exception? exception = null)
        {
            if (!_isInitialized || _logger == null)
                return;

            if (exception != null)
                _logger.Error(exception, message);
            else
                _logger.Error(message);
        }

        /// <summary>
        /// Logs a fatal error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception associated with the fatal error.</param>
        public void Fatal(string message, Exception? exception = null)
        {
            if (!_isInitialized || _logger == null)
                return;

            if (exception != null)
                _logger.Fatal(exception, message);
            else
                _logger.Fatal(message);
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Debug(string message)
        {
            if (!_isInitialized || _logger == null)
                return;

            _logger.Debug(message);
        }

        /// <summary>
        /// Shuts down the logger.
        /// </summary>
        public void Shutdown()
        {
            if (_isInitialized)
            {
                LogManager.Shutdown();
                _isInitialized = false;
                _logger = null;
                _logFolderPath = null;
            }
        }

        /// <summary>
        /// For testing purposes only - resets the logger state
        /// </summary>
        internal void Reset()
        {
            Shutdown();
        }
    }
}
