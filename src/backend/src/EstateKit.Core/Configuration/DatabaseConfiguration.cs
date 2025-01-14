using Microsoft.Extensions.Configuration; // v9.0.0

namespace EstateKit.Core.Configuration
{
    /// <summary>
    /// Encapsulates secure configuration settings for PostgreSQL database connection and behavior,
    /// implementing GDPR and SOC 2 compliant defaults for the EstateKit encryption service.
    /// </summary>
    public class DatabaseConfiguration
    {
        /// <summary>
        /// Gets or sets the database connection string. This should be securely injected 
        /// from environment-specific configuration sources.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the database schema name used for table isolation.
        /// Default: "estatekit"
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// Gets or sets the command timeout in seconds for database operations.
        /// Default: 30 seconds
        /// </summary>
        public int CommandTimeout { get; set; }

        /// <summary>
        /// Gets or sets whether automatic retry on transient failures is enabled.
        /// Default: true for production resilience
        /// </summary>
        public bool EnableRetryOnFailure { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed operations.
        /// Default: 3 attempts
        /// </summary>
        public int MaxRetryCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum delay between retries in seconds.
        /// Default: 30 seconds
        /// </summary>
        public int MaxRetryDelay { get; set; }

        /// <summary>
        /// Gets or sets whether sensitive data logging is enabled.
        /// Default: false for GDPR compliance
        /// </summary>
        public bool EnableSensitiveDataLogging { get; set; }

        /// <summary>
        /// Gets or sets whether detailed error messages are enabled.
        /// Default: false for production security
        /// </summary>
        public bool EnableDetailedErrors { get; set; }

        /// <summary>
        /// Initializes a new instance of the DatabaseConfiguration class with secure,
        /// production-ready default values compliant with GDPR and SOC 2 requirements.
        /// </summary>
        public DatabaseConfiguration()
        {
            // Initialize with secure default values
            ConnectionString = string.Empty;  // Must be provided via secure configuration
            Schema = "estatekit";            // Dedicated schema for security isolation
            CommandTimeout = 30;             // 30 second timeout for balanced operation
            EnableRetryOnFailure = true;     // Enable resilient operations
            MaxRetryCount = 3;               // 3 retry attempts for fault tolerance
            MaxRetryDelay = 30;              // 30 second maximum retry delay
            EnableSensitiveDataLogging = false;  // Disabled for GDPR compliance
            EnableDetailedErrors = false;     // Disabled for production security
        }
    }
}