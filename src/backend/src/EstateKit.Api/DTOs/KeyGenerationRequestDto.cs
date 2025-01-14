using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Data transfer object for key generation requests.
    /// </summary>
    /// <remarks>
    /// Used to validate and transfer user identification data for generating new encryption key pairs.
    /// Implements security measures and input validation.
    /// </remarks>
    [JsonSerializable(typeof(KeyGenerationRequestDto))]
    public class KeyGenerationRequestDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the user requesting key generation.
        /// </summary>
        /// <remarks>
        /// Must be a positive non-zero value.
        /// </remarks>
        [Required(ErrorMessage = "User identifier is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "Invalid user identifier.")]
        [JsonPropertyName("userId")]
        public long UserId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyGenerationRequestDto"/> class.
        /// </summary>
        public KeyGenerationRequestDto()
        {
            // Default constructor required for model binding
        }
    }
}