using System;
using System.ComponentModel.DataAnnotations; // v9.0.0

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Represents a request for batch encryption of string arrays using user-specific encryption keys
    /// </summary>
    [Serializable]
    public class EncryptRequestDto
    {
        /// <summary>
        /// Unique identifier of the user whose public key will be used for encryption
        /// </summary>
        [Required(ErrorMessage = "User ID is required for encryption operations")]
        [Range(1, long.MaxValue, ErrorMessage = "User ID must be a positive number")]
        public long UserId { get; set; }

        /// <summary>
        /// Array of strings to be encrypted. Each string will be individually encrypted using the user's public key
        /// </summary>
        [Required(ErrorMessage = "Data array is required for encryption")]
        [MinLength(1, ErrorMessage = "At least one string must be provided for encryption")]
        [MaxLength(1000, ErrorMessage = "Maximum of 1000 strings can be encrypted in a single request")]
        public string[] Data { get; set; }
    }
}