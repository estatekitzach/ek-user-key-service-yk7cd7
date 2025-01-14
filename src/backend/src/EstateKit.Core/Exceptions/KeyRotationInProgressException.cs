using System;
using EstateKit.Core.Constants;

namespace EstateKit.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when attempting to rotate a user's encryption key while another rotation 
    /// operation is already in progress for the same user. This exception is thread-safe and 
    /// serializable for distributed scenarios.
    /// </summary>
    [Serializable]
    public class KeyRotationInProgressException : Exception
    {
        /// <summary>
        /// Gets the standardized error code for key rotation in progress scenarios.
        /// This property is thread-safe and immutable after initialization.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets the ID of the user for whom the key rotation operation was attempted.
        /// This property is thread-safe and immutable after initialization.
        /// </summary>
        public long UserId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyRotationInProgressException"/> class
        /// with thread-safe property initialization.
        /// </summary>
        /// <param name="userId">The ID of the user for whom key rotation was attempted.</param>
        public KeyRotationInProgressException(long userId) 
            : base(GetErrorMessage(userId))
        {
            ErrorCode = ErrorCodes.KEY_ROTATION_IN_PROGRESS;
            UserId = userId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyRotationInProgressException"/> class
        /// with serialization support.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        protected KeyRotationInProgressException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) 
            : base(info, context)
        {
            ErrorCode = info.GetString(nameof(ErrorCode));
            UserId = info.GetInt64(nameof(UserId));
        }

        /// <summary>
        /// When overridden in a derived class, sets the SerializationInfo with information about the exception.
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        public override void GetObjectData(
            System.Runtime.Serialization.SerializationInfo info, 
            System.Runtime.Serialization.StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue(nameof(ErrorCode), ErrorCode);
            info.AddValue(nameof(UserId), UserId);
            base.GetObjectData(info, context);
        }

        /// <summary>
        /// Generates a formatted error message for the exception using string interpolation
        /// for optimal performance.
        /// </summary>
        /// <param name="userId">The ID of the user for whom key rotation was attempted.</param>
        /// <returns>A thread-safe formatted error message.</returns>
        private static string GetErrorMessage(long userId)
        {
            return $"Key rotation operation is already in progress for user ID: {userId}. Please retry after the current operation completes.";
        }
    }
}