using System.Text.Json.Serialization; // v9.0.0

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Data transfer object representing the response from the encryption endpoint.
    /// Contains an array of encrypted strings with proper JSON serialization and null handling.
    /// </summary>
    public class EncryptResponseDto
    {
        /// <summary>
        /// Gets or sets the array of encrypted string data.
        /// Never returns null - will return empty array instead for consistent serialization.
        /// </summary>
        [JsonPropertyName("encryptedData")]
        public string[] EncryptedData { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptResponseDto"/> class.
        /// </summary>
        /// <param name="encryptedData">Optional array of encrypted strings. If null, an empty array will be used.</param>
        public EncryptResponseDto(string[]? encryptedData = null)
        {
            EncryptedData = encryptedData ?? Array.Empty<string>();
        }
    }
}