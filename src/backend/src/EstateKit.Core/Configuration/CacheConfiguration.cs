using Microsoft.Extensions.Options; // v9.0.0
using System.ComponentModel.DataAnnotations; // v9.0.0
using System.Collections.Generic;

namespace EstateKit.Core.Configuration
{
    /// <summary>
    /// Configuration settings for Redis cache service including connection details, 
    /// performance options, and retry policies with built-in validation.
    /// </summary>
    public class CacheConfiguration : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the Redis connection string.
        /// This should be a secure connection string with SSL enabled.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the Redis instance name for key prefixing.
        /// Used to isolate keys in a shared Redis instance.
        /// </summary>
        public string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the Redis database ID.
        /// Must be a non-negative integer.
        /// </summary>
        public int DatabaseId { get; set; }

        /// <summary>
        /// Gets or sets the default Time-To-Live (TTL) in minutes for cached items.
        /// Must be between 1 and 1440 minutes (24 hours).
        /// </summary>
        public int DefaultTTLMinutes { get; set; }

        /// <summary>
        /// Gets or sets whether compression is enabled for cached values.
        /// Compression can improve network performance for large values.
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// Gets or sets the number of retry attempts for failed operations.
        /// Must be between 0 and 10 attempts.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the delay between retry attempts in milliseconds.
        /// Must be between 50 and 5000 milliseconds.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheConfiguration"/> class
        /// with secure default values aligned with technical specifications.
        /// </summary>
        public CacheConfiguration()
        {
            // Set defaults according to technical specifications
            DefaultTTLMinutes = 15; // 15-minute TTL per technical spec
            DatabaseId = 0; // Default to database 0
            EnableCompression = true; // Enable compression by default for performance
            RetryCount = 3; // Default to 3 retry attempts
            RetryDelayMilliseconds = 100; // Default to 100ms retry delay
            ConnectionString = string.Empty; // Must be provided in configuration
            InstanceName = string.Empty; // Must be provided in configuration
        }

        /// <summary>
        /// Validates the configuration settings according to business rules and technical specifications.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>A collection of validation results.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var validationResults = new List<ValidationResult>();

            // Validate connection string
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                validationResults.Add(new ValidationResult(
                    "Redis connection string must be provided.",
                    new[] { nameof(ConnectionString) }
                ));
            }

            // Validate instance name
            if (string.IsNullOrWhiteSpace(InstanceName))
            {
                validationResults.Add(new ValidationResult(
                    "Redis instance name must be provided.",
                    new[] { nameof(InstanceName) }
                ));
            }

            // Validate database ID
            if (DatabaseId < 0)
            {
                validationResults.Add(new ValidationResult(
                    "Redis database ID must be non-negative.",
                    new[] { nameof(DatabaseId) }
                ));
            }

            // Validate TTL range (1 minute to 24 hours)
            if (DefaultTTLMinutes < 1 || DefaultTTLMinutes > 1440)
            {
                validationResults.Add(new ValidationResult(
                    "Default TTL must be between 1 and 1440 minutes.",
                    new[] { nameof(DefaultTTLMinutes) }
                ));
            }

            // Validate retry count
            if (RetryCount < 0 || RetryCount > 10)
            {
                validationResults.Add(new ValidationResult(
                    "Retry count must be between 0 and 10 attempts.",
                    new[] { nameof(RetryCount) }
                ));
            }

            // Validate retry delay
            if (RetryDelayMilliseconds < 50 || RetryDelayMilliseconds > 5000)
            {
                validationResults.Add(new ValidationResult(
                    "Retry delay must be between 50 and 5000 milliseconds.",
                    new[] { nameof(RetryDelayMilliseconds) }
                ));
            }

            return validationResults;
        }
    }
}