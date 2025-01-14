using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Data transfer object representing a request to decrypt an array of encrypted strings for a specific user.
    /// Implements validation for request integrity and security controls.
    /// </summary>
    [JsonSerializable(typeof(DecryptRequestDto))]
    public class DecryptRequestDto
    {
        /// <summary>
        /// Unique identifier of the user requesting decryption.
        /// Must be a positive number greater than zero.
        /// </summary>
        [Required(ErrorMessage = "User ID is required")]
        [Range(1, long.MaxValue, ErrorMessage = "User ID must be a positive number")]
        public long UserId { get; set; }

        /// <summary>
        /// Array of Base64 encoded encrypted strings to be decrypted.
        /// Maximum array size is 1000 elements.
        /// Each element must be a valid Base64 encoded string.
        /// </summary>
        [Required(ErrorMessage = "Encrypted data array is required")]
        [MaxLength(1000, ErrorMessage = "Maximum array size exceeded")]
        [RegularExpression(@"^[A-Za-z0-9+/]*={0,3}$", ErrorMessage = "Invalid Base64 format")]
        public string[] EncryptedData { get; set; }

        /// <summary>
        /// Validates that the encrypted data array is not null or empty and contains valid Base64 strings.
        /// </summary>
        /// <returns>True if validation passes, false otherwise.</returns>
        public bool ValidateEncryptedData()
        {
            if (EncryptedData == null || !EncryptedData.Any())
            {
                return false;
            }

            return EncryptedData.All(data => 
                !string.IsNullOrWhiteSpace(data) && 
                IsBase64String(data));
        }

        /// <summary>
        /// Checks if a string is a valid Base64 encoded value.
        /// </summary>
        /// <param name="input">String to validate</param>
        /// <returns>True if string is valid Base64, false otherwise</returns>
        private static bool IsBase64String(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            try
            {
                // Attempt to convert the string from Base64
                Convert.FromBase64String(input);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}