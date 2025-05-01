using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClipSage.Core.Logging;
using LiteDB;

namespace ClipSage.Core.Storage
{
    public class DatabaseConnectionManager : IDisposable
    {
        private static readonly Lazy<DatabaseConnectionManager> _instance = new(() => new DatabaseConnectionManager());
        public static DatabaseConnectionManager Instance => _instance.Value;

        private LiteDatabase _database;
        private string _databasePath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly int _maxRetries = 5;
        private readonly int _retryDelayMs = 200;
        private bool _disposed = false;

        private DatabaseConnectionManager()
        {
            // Private constructor for singleton pattern
        }

        public void Initialize(string databasePath)
        {
            if (_database != null)
            {
                // Already initialized
                return;
            }

            _databasePath = databasePath;

            try
            {
                // Create the directory if it doesn't exist
                string directory = Path.GetDirectoryName(databasePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool databaseExists = File.Exists(databasePath);

                // Configure LiteDB connection
                var connectionString = new ConnectionString
                {
                    Filename = databasePath,
                    Connection = ConnectionType.Shared, // Use shared connection mode for better concurrency
                    ReadOnly = false,
                    Upgrade = true, // This will upgrade the database if it's an older version
                    Collation = Collation.Default
                };

                // Open the database
                _database = new LiteDatabase(connectionString);

                // Create indexes for better performance
                var collection = _database.GetCollection<ClipboardEntry>("history");
                collection.EnsureIndex(x => x.Timestamp);

                // Log whether we're using an existing database or creating a new one
                if (databaseExists)
                {
                    Console.WriteLine($"Using existing database: {databasePath}");
                    Logger.Instance.Info($"Using existing database: {databasePath}");

                    // Verify the database structure
                    VerifyDatabaseStructure();
                }
                else
                {
                    Console.WriteLine($"Created new database: {databasePath}");
                    Logger.Instance.Info($"Created new database: {databasePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
                Logger.Instance.Error($"Error initializing database: {databasePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Verifies the database structure and performs any necessary migrations or repairs
        /// </summary>
        private void VerifyDatabaseStructure()
        {
            try
            {
                // Check if the history collection exists
                var collection = _database.GetCollection<ClipboardEntry>("history");

                // Check if we can read from the collection
                var count = collection.Count();
                Console.WriteLine($"Found {count} entries in existing database");
                Logger.Instance.Info($"Found {count} entries in existing database");

                // Additional verification could be added here if needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verifying database structure: {ex.Message}");
                Logger.Instance.Error("Error verifying database structure", ex);

                // We don't throw here - we'll continue with a potentially problematic database
                // rather than preventing the application from starting
            }
        }

        public async Task<T> ExecuteWithRetryAsync<T>(Func<LiteDatabase, T> operation)
        {
            if (_database == null)
            {
                throw new InvalidOperationException("Database not initialized. Call Initialize first.");
            }

            await _semaphore.WaitAsync();

            try
            {
                Exception lastException = null;

                for (int attempt = 0; attempt < _maxRetries; attempt++)
                {
                    try
                    {
                        return operation(_database);
                    }
                    catch (LiteException ex) when (IsFileLockException(ex))
                    {
                        lastException = ex;

                        // Wait before retrying
                        await Task.Delay(_retryDelayMs * (attempt + 1));

                        // If this is the last attempt, try to recover the connection
                        if (attempt == _maxRetries - 2)
                        {
                            try
                            {
                                // Try to close and reopen the database
                                _database?.Dispose();

                                // Reopen the database
                                var connectionString = new ConnectionString
                                {
                                    Filename = _databasePath,
                                    Connection = ConnectionType.Shared,
                                    ReadOnly = false,
                                    Upgrade = true
                                };

                                _database = new LiteDatabase(connectionString);
                            }
                            catch (Exception recoveryEx)
                            {
                                Console.WriteLine($"Error recovering database connection: {recoveryEx.Message}");
                                Logger.Instance.Error("Error recovering database connection", recoveryEx);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // For other exceptions, don't retry
                        throw;
                    }
                }

                // If we got here, all retries failed
                var errorMessage = $"Failed to execute database operation after {_maxRetries} attempts";
                Logger.Instance.Error(errorMessage, lastException);
                throw new Exception(errorMessage, lastException);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private bool IsFileLockException(Exception ex)
        {
            // Check if the exception is related to file locking
            return ex.Message.Contains("locked") ||
                   ex.Message.Contains("access") ||
                   ex.Message.Contains("sharing violation") ||
                   ex.Message.Contains("being used by another process");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _database?.Dispose();
                    _semaphore?.Dispose();
                }

                _database = null;
                _disposed = true;
            }
        }

        ~DatabaseConnectionManager()
        {
            Dispose(false);
        }
    }
}
