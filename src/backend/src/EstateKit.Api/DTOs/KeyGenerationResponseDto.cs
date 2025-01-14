using System;
using System.Text.Json.Serialization;

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Data Transfer Object (DTO) for key generation response data in the EstateKit Personal Information API.
    /// Encapsulates the result of asymmetric key generation operations including success status,
    /// key metadata, and rotation schedule information.
    /// </summary>
    public class KeyGenerationResponseDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether the key generation operation was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user for whom the key was generated.
        /// </summary>
        [JsonPropertyName("userId")]
        public long UserId { get; set; }

        /// <summary>
        /// Gets or sets the version number of the generated key.
        /// Used for tracking key rotation and versioning history.
        /// </summary>
        [JsonPropertyName("keyVersion")]
        public long KeyVersion { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the key was generated.
        /// </summary>
        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the next key rotation is scheduled.
        /// Based on the 90-day rotation policy specified in technical requirements.
        /// </summary>
        [JsonPropertyName("nextRotationDue")]
        public DateTime NextRotationDue { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyGenerationResponseDto"/> class
        /// with default values and calculated rotation schedule.
        /// </summary>
        public KeyGenerationResponseDto()
        {
            Success = false;
            UserId = 0;
            KeyVersion = 0;
            GeneratedAt = DateTime.UtcNow;
            NextRotationDue = GeneratedAt.AddDays(90); // 90-day rotation schedule per requirements
        }
    }
}