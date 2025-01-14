using System.Threading.Tasks;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for FIPS 140-2 compliant encryption key management operations
    /// in the EstateKit system. Provides methods for generating, rotating, retrieving,
    /// and deactivating asymmetric encryption keys using AWS KMS.
    /// </summary>
    public interface IKeyManagementService
    {
        /// <summary>
        /// Generates a new FIPS 140-2 compliant asymmetric key pair for a user using AWS KMS.
        /// </summary>
        /// <param name="userId">The unique identifier of the user requiring a new key pair.</param>
        /// <returns>A new UserKey instance containing the public key and concurrency token.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is invalid (less than or equal to 0).</exception>
        /// <exception cref="InvalidOperationException">Thrown when user already has an active key.</exception>
        /// <exception cref="ApplicationException">Thrown with ErrorCodes.KMS_SERVICE_ERROR when AWS KMS operation fails.</exception>
        Task<UserKey> GenerateKeyPairAsync(long userId);

        /// <summary>
        /// Rotates the existing key pair for a user with audit trail and concurrency control.
        /// Automatically archives the current key and generates a new FIPS 140-2 compliant key pair.
        /// </summary>
        /// <param name="userId">The unique identifier of the user requiring key rotation.</param>
        /// <param name="rotationReason">The reason for key rotation (e.g., scheduled, compromised, compliance).</param>
        /// <returns>Updated UserKey instance with new public key and concurrency token.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is invalid or rotationReason is null/empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown with ErrorCodes.KEY_NOT_FOUND when no active key exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown with ErrorCodes.KEY_ROTATION_IN_PROGRESS when rotation is in progress.</exception>
        /// <exception cref="ApplicationException">Thrown with ErrorCodes.KMS_SERVICE_ERROR when AWS KMS operation fails.</exception>
        Task<UserKey> RotateKeyPairAsync(long userId, string rotationReason);

        /// <summary>
        /// Retrieves the current active encryption key for a user with validation.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose key is being retrieved.</param>
        /// <returns>Active UserKey instance with concurrency token.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is invalid (less than or equal to 0).</exception>
        /// <exception cref="KeyNotFoundException">Thrown with ErrorCodes.KEY_NOT_FOUND when no active key exists.</exception>
        Task<UserKey> GetActiveKeyAsync(long userId);

        /// <summary>
        /// Deactivates a user's current active encryption key with concurrency control.
        /// This operation is typically performed during key rotation or when retiring keys.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose key should be deactivated.</param>
        /// <returns>True if deactivation was successful, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is invalid (less than or equal to 0).</exception>
        /// <exception cref="KeyNotFoundException">Thrown with ErrorCodes.KEY_NOT_FOUND when no active key exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when key is already inactive.</exception>
        Task<bool> DeactivateKeyAsync(long userId);
    }
}