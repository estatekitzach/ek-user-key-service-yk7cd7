using System;
using EstateKit.Core.Constants;

namespace EstateKit.Core.Exceptions
{
    /// <summary>
    /// Custom exception thrown when an encryption key cannot be found in the system.
    /// Provides detailed error context and standardized error response generation for API communication.
    /// </summary>
    [Serializable]
    public class KeyNotFoundException : Exception
    {
        /// <summary>
        /// Gets the standardized error code for key not found scenarios.
        /// </summary>
        public string ErrorCode { get; private set; }

        /// <summary>
        /// Gets the user ID associated with the missing key.
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// Gets the HTTP status code (404) for key not found responses.
        /// </summary>
        public int HttpStatusCode { get; private set; }

        /// <summary>
        /// Gets the correlation ID for request tracking and monitoring.
        /// </summary>
        public string CorrelationId { get; private set; }

        /// <summary>
        /// Gets the UTC timestamp when the exception occurred.
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyNotFoundException"/> class with error context.
        /// </summary>
        /// <param name="userId">The ID of the user whose key was not found.</param>
        /// <param name="message">The error message describing the key not found scenario.</param>
        /// <param name="correlationId">The correlation ID for request tracking.</param>
        public KeyNotFoundException(string userId, string message, string correlationId) 
            : base(message)
        {
            ErrorCode = ErrorCodes.KEY_NOT_FOUND;
            UserId = userId;
            HttpStatusCode = 404;
            CorrelationId = correlationId;
            Timestamp = DateTime.UtcNow;
            
            LogException();
        }

        /// <summary>
        /// Creates a standardized error response object for API communication.
        /// </summary>
        /// <returns>An object containing complete error context for client communication.</returns>
        public object GetErrorResponse()
        {
            return new
            {
                ErrorCode = this.ErrorCode,
                Message = this.Message,
                StatusCode = this.HttpStatusCode,
                Timestamp = this.Timestamp,
                CorrelationId = this.CorrelationId,
                UserId = this.UserId,
                RecoveryAction = "Generate new key or verify key identifier"
            };
        }

        /// <summary>
        /// Logs exception details to monitoring system for tracking and alerting.
        /// </summary>
        private void LogException()
        {
            // Create structured log entry with complete error context
            var logEntry = new
            {
                EventType = "Exception",
                ExceptionType = this.GetType().Name,
                ErrorCode = this.ErrorCode,
                Message = this.Message,
                UserId = this.UserId,
                CorrelationId = this.CorrelationId,
                Timestamp = this.Timestamp,
                StackTrace = this.StackTrace
            };

            // Note: Actual logging implementation would be injected via dependency injection
            // This is a placeholder for the logging framework integration
            // Logger.LogError(logEntry);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyNotFoundException"/> class with serialization support.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        protected KeyNotFoundException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
            ErrorCode = info.GetString(nameof(ErrorCode));
            UserId = info.GetString(nameof(UserId));
            HttpStatusCode = info.GetInt32(nameof(HttpStatusCode));
            CorrelationId = info.GetString(nameof(CorrelationId));
            Timestamp = info.GetDateTime(nameof(Timestamp));
        }

        /// <summary>
        /// Gets object data for serialization.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorCode), ErrorCode);
            info.AddValue(nameof(UserId), UserId);
            info.AddValue(nameof(HttpStatusCode), HttpStatusCode);
            info.AddValue(nameof(CorrelationId), CorrelationId);
            info.AddValue(nameof(Timestamp), Timestamp);
        }
    }
}