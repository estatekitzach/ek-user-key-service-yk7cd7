namespace EstateKit.Core.Constants
{
    /// <summary>
    /// Static class containing standardized error codes used throughout the EstateKit Personal Information API 
    /// for consistent error handling and client communication. Each error code maps to specific error scenarios 
    /// with corresponding HTTP status codes and recovery actions.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// Error code KEY001 (HTTP 404) indicating requested encryption key was not found in the system.
        /// Recovery Action: Generate new key or verify key identifier.
        /// </summary>
        public const string KEY_NOT_FOUND = "KEY001";

        /// <summary>
        /// Error code KEY002 (HTTP 409) indicating key rotation operation is currently in progress.
        /// Recovery Action: Retry request after a brief delay.
        /// </summary>
        public const string KEY_ROTATION_IN_PROGRESS = "KEY002";

        /// <summary>
        /// Error code ENC001 (HTTP 400) indicating invalid format of input data for encryption/decryption.
        /// Recovery Action: Validate input format against API specifications.
        /// </summary>
        public const string INVALID_INPUT_FORMAT = "ENC001";

        /// <summary>
        /// Error code AUTH001 (HTTP 401) indicating invalid or expired OAuth authentication token.
        /// Recovery Action: Refresh authentication token or re-authenticate.
        /// </summary>
        public const string INVALID_OAUTH_TOKEN = "AUTH001";

        /// <summary>
        /// Error code SYS001 (HTTP 500) indicating AWS KMS service error or unavailability.
        /// Recovery Action: Contact system support or retry after service restoration.
        /// </summary>
        public const string KMS_SERVICE_ERROR = "SYS001";

        // Private constructor to prevent instantiation of the static error codes class
        private ErrorCodes()
        {
            // Private constructor to enforce static class usage
        }
    }
}