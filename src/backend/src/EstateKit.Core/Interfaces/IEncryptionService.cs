using System.Threading.Tasks;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for FIPS 140-2 compliant encryption and decryption operations
    /// using AWS KMS with hardware security module (HSM) backing for cryptographic operations.
    /// </summary>
    /// <remarks>
    /// This service implements asymmetric encryption using user-specific keys stored in AWS KMS,
    /// ensuring compliance with FIPS 140-2 standards through HSM-backed key operations.
    /// All operations are performed asynchronously with batch processing support.
    /// </remarks>
    public interface IEncryptionService
    {
        /// <summary>
        /// Encrypts an array of strings using the user's public key with FIPS 140-2 compliant encryption.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose public key will be used for encryption.</param>
        /// <param name="data">Array of strings to be encrypted. Each string will be encrypted individually.</param>
        /// <returns>
        /// A task that represents the asynchronous operation, containing an array of Base64-encoded encrypted strings.
        /// The array maintains the same order as the input data array.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when userId is 0 or data is null.</exception>
        /// <exception cref="ArgumentException">Thrown when data array is empty or contains null/empty strings.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when no active encryption key is found for the user.</exception>
        /// <exception cref="KeyRotationInProgressException">Thrown when key rotation is in progress for the user.</exception>
        /// <exception cref="EncryptionException">Thrown when encryption operation fails.</exception>
        Task<string[]> EncryptAsync(long userId, string[] data);

        /// <summary>
        /// Decrypts an array of encrypted strings using the user's private key via AWS KMS HSM.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose private key will be used for decryption.</param>
        /// <param name="encryptedData">Array of Base64-encoded encrypted strings to be decrypted.</param>
        /// <returns>
        /// A task that represents the asynchronous operation, containing an array of decrypted strings.
        /// The array maintains the same order as the input encryptedData array.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when userId is 0 or encryptedData is null.</exception>
        /// <exception cref="ArgumentException">Thrown when encryptedData array is empty or contains invalid Base64 strings.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when no active encryption key is found for the user.</exception>
        /// <exception cref="KeyRotationInProgressException">Thrown when key rotation is in progress for the user.</exception>
        /// <exception cref="EncryptionException">Thrown when decryption operation fails.</exception>
        Task<string[]> DecryptAsync(long userId, string[] encryptedData);
    }
}