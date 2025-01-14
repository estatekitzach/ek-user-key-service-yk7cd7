using Microsoft.Extensions.Configuration; // v9.0.0
using System;
using System.Text.RegularExpressions;

namespace EstateKit.Core.Configuration
{
    /// <summary>
    /// Configuration settings for AWS services used by the EstateKit Personal Information API.
    /// Provides centralized configuration for AWS KMS, Cognito, and other AWS services.
    /// </summary>
    public class AwsConfiguration
    {
        /// <summary>
        /// AWS Region where services are deployed (e.g., us-east-1)
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// AWS KMS Key ID used for encryption operations
        /// Format: arn:aws:kms:{region}:{account}:key/{key-id} or key/{key-id}
        /// </summary>
        public string KmsKeyId { get; set; }

        /// <summary>
        /// Indicates whether to use IAM role-based authentication instead of explicit credentials
        /// </summary>
        public bool UseIamRole { get; set; }

        /// <summary>
        /// Cognito User Pool ID for authentication
        /// Format: {region}_{poolId}
        /// </summary>
        public string CognitoUserPoolId { get; set; }

        /// <summary>
        /// Cognito App Client ID for the application
        /// </summary>
        public string CognitoAppClientId { get; set; }

        /// <summary>
        /// Optional custom endpoint URL for AWS KMS service
        /// Used primarily for testing with LocalStack
        /// </summary>
        public string KmsEndpoint { get; set; }

        /// <summary>
        /// Optional custom endpoint URL for AWS Cognito service
        /// Used primarily for testing with LocalStack
        /// </summary>
        public string CognitoEndpoint { get; set; }

        /// <summary>
        /// Indicates whether to use LocalStack for local development instead of real AWS services
        /// </summary>
        public bool UseLocalstack { get; set; }

        /// <summary>
        /// Timeout in seconds for AWS service requests
        /// Default: 30 seconds
        /// </summary>
        public int RequestTimeout { get; set; }

        /// <summary>
        /// Initializes a new instance of the AwsConfiguration class with default values
        /// </summary>
        public AwsConfiguration()
        {
            // Set default values as per technical specifications
            Region = "us-east-1"; // Primary region specified in infrastructure design
            UseIamRole = true;    // Default to IAM role authentication for security
            RequestTimeout = 30;   // Default timeout as per technical specifications
            UseLocalstack = false; // Default to real AWS services
        }

        /// <summary>
        /// Validates the AWS configuration settings to ensure all required values are properly set
        /// </summary>
        /// <returns>True if the configuration is valid, otherwise false</returns>
        public bool Validate()
        {
            // Region is required
            if (string.IsNullOrWhiteSpace(Region))
            {
                throw new ArgumentException("AWS Region must be specified");
            }

            // Validate KMS Key ID format if provided
            if (!string.IsNullOrWhiteSpace(KmsKeyId))
            {
                var kmsKeyPattern = @"^(arn:aws:kms:[a-z0-9-]+:\d{12}:key/[a-f0-9-]{36}|key/[a-f0-9-]{36})$";
                if (!Regex.IsMatch(KmsKeyId, kmsKeyPattern, RegexOptions.IgnoreCase))
                {
                    throw new ArgumentException("Invalid KMS Key ID format");
                }
            }

            // Validate Cognito settings if not using IAM role
            if (!UseIamRole)
            {
                if (string.IsNullOrWhiteSpace(CognitoUserPoolId))
                {
                    throw new ArgumentException("Cognito User Pool ID is required when not using IAM role");
                }

                if (string.IsNullOrWhiteSpace(CognitoAppClientId))
                {
                    throw new ArgumentException("Cognito App Client ID is required when not using IAM role");
                }

                // Validate User Pool ID format
                var userPoolPattern = @"^[a-z0-9-]+_[a-zA-Z0-9]+$";
                if (!Regex.IsMatch(CognitoUserPoolId, userPoolPattern))
                {
                    throw new ArgumentException("Invalid Cognito User Pool ID format");
                }
            }

            // Validate custom endpoints if provided
            if (!string.IsNullOrWhiteSpace(KmsEndpoint))
            {
                if (!Uri.TryCreate(KmsEndpoint, UriKind.Absolute, out _))
                {
                    throw new ArgumentException("Invalid KMS endpoint URL");
                }
            }

            if (!string.IsNullOrWhiteSpace(CognitoEndpoint))
            {
                if (!Uri.TryCreate(CognitoEndpoint, UriKind.Absolute, out _))
                {
                    throw new ArgumentException("Invalid Cognito endpoint URL");
                }
            }

            // Validate request timeout
            if (RequestTimeout <= 0)
            {
                throw new ArgumentException("Request timeout must be greater than 0 seconds");
            }

            return true;
        }
    }
}