using System.Text.Json.Serialization; // v9.0.0

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Data Transfer Object (DTO) representing the response from the decryption endpoint.
    /// Contains the decrypted string array data returned to API clients with proper null handling.
    /// </summary>
    public class DecryptResponseDto
    {
        /// <summary>
        /// Gets or sets the array of decrypted string data.
        /// Never returns null - will return empty array if no data is present.
        /// </summary>
        [JsonPropertyName("decryptedData")]
        public string[] DecryptedData { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DecryptResponseDto"/> class
        /// with an empty array for null safety.
        /// </summary>
        public DecryptResponseDto()
        {
            DecryptedData = Array.Empty<string>();
        }
    }
}