using System; // v9.0.0
using System.Text.Json.Serialization; // v9.0.0

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Data Transfer Object for encapsulating key rotation response data with standardized messaging and serialization support.
    /// Provides a consistent contract for communicating key rotation results through the API.
    /// </summary>
    public class RotateKeyResponseDto
    {
        /// <summary>
        /// Indicates whether the key rotation operation was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; private set; }

        /// <summary>
        /// The unique identifier of the user whose key was rotated.
        /// </summary>
        [JsonPropertyName("userId")]
        public long UserId { get; private set; }

        /// <summary>
        /// UTC timestamp when the key rotation was performed.
        /// </summary>
        [JsonPropertyName("rotationTimestamp")]
        public DateTime RotationTimestamp { get; private set; }

        /// <summary>
        /// The reason for key rotation (e.g., "scheduled", "on-demand", "emergency").
        /// </summary>
        [JsonPropertyName("rotationReason")]
        public string RotationReason { get; private set; }

        /// <summary>
        /// Human-readable message describing the rotation result.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; private set; }

        /// <summary>
        /// API response version for backward compatibility support.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; private set; }

        /// <summary>
        /// Initializes a new instance of the RotateKeyResponseDto class.
        /// </summary>
        /// <param name="success">Indicates if the rotation was successful</param>
        /// <param name="userId">The ID of the user whose key was rotated</param>
        /// <param name="rotationReason">The reason for performing the rotation</param>
        /// <exception cref="ArgumentException">Thrown when rotation reason is invalid</exception>
        public RotateKeyResponseDto(bool success, long userId, string rotationReason)
        {
            if (!ValidateRotationReason(rotationReason))
            {
                throw new ArgumentException("Invalid rotation reason provided", nameof(rotationReason));
            }

            Success = success;
            UserId = userId;
            RotationTimestamp = DateTime.UtcNow;
            RotationReason = rotationReason;
            Version = "1.0";
            Message = GenerateStandardMessage(success, rotationReason);
        }

        /// <summary>
        /// Validates the rotation reason against predefined acceptable values.
        /// </summary>
        /// <param name="reason">The rotation reason to validate</param>
        /// <returns>True if the reason is valid, false otherwise</returns>
        private static bool ValidateRotationReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            var validReasons = new[]
            {
                "scheduled",      // 90-day automated rotation
                "on-demand",      // Manual API-triggered rotation
                "emergency",      // Security incident response
                "compliance",     // Regulatory requirement
                "key-compromise", // Suspected or confirmed compromise
                "system-update"   // System maintenance or upgrade
            };

            return Array.Exists(validReasons, r => r.Equals(reason, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Generates a standardized message based on rotation success and reason.
        /// </summary>
        /// <param name="success">Whether the rotation was successful</param>
        /// <param name="reason">The reason for rotation</param>
        /// <returns>A formatted message string</returns>
        private static string GenerateStandardMessage(bool success, string reason)
        {
            if (success)
            {
                return reason.ToLowerInvariant() switch
                {
                    "scheduled" => "Scheduled key rotation completed successfully",
                    "emergency" => "Emergency key rotation executed successfully - immediate action required",
                    "key-compromise" => "Security key rotation completed - please update all dependent systems",
                    "compliance" => "Compliance-driven key rotation completed successfully",
                    "on-demand" => "Manual key rotation completed as requested",
                    "system-update" => "System maintenance key rotation completed successfully",
                    _ => $"Key rotation completed successfully: {reason}"
                };
            }
            
            return reason.ToLowerInvariant() switch
            {
                "scheduled" => "Scheduled key rotation failed - manual intervention required",
                "emergency" => "CRITICAL: Emergency key rotation failed - immediate action required",
                "key-compromise" => "SECURITY ALERT: Key rotation failed - system may be compromised",
                "compliance" => "Compliance key rotation failed - regulatory requirements not met",
                "on-demand" => "Manual key rotation request failed - please retry or contact support",
                "system-update" => "System maintenance key rotation failed - maintenance incomplete",
                _ => $"Key rotation failed: {reason}"
            };
        }
    }
}