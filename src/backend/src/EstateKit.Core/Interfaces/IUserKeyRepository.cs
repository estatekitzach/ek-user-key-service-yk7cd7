using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EstateKit.Core.Entities;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Repository interface for managing user encryption keys and their rotation history
    /// with comprehensive security, audit, and compliance capabilities.
    /// Implements the repository pattern for secure key storage and retrieval operations.
    /// </summary>
    public interface IUserKeyRepository
    {
        /// <summary>
        /// Retrieves the currently active encryption key for a specific user.
        /// Enforces key expiration policy of 90 days and validates key status.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The active UserKey if found and valid; null otherwise.</returns>
        /// <remarks>
        /// - Validates key expiration against 90-day rotation policy
        /// - Checks key format using regex pattern
        /// - Verifies key status and version
        /// </remarks>
        Task<UserKey> GetActiveKeyAsync(long userId);

        /// <summary>
        /// Creates a new encryption key for a user with proper validation and timestamps.
        /// Implements strict key format validation and concurrency control.
        /// </summary>
        /// <param name="userKey">The UserKey entity to create.</param>
        /// <returns>The newly created UserKey with updated timestamps and version.</returns>
        /// <remarks>
        /// - Validates key format and length (minimum 2048 bits)
        /// - Sets creation and update timestamps
        /// - Initializes rotation version
        /// - Implements optimistic concurrency
        /// </remarks>
        Task<UserKey> CreateKeyAsync(UserKey userKey);

        /// <summary>
        /// Updates an existing encryption key during rotation with status tracking
        /// and concurrency control to prevent conflicts.
        /// </summary>
        /// <param name="userKey">The UserKey entity to update.</param>
        /// <returns>The updated UserKey with new timestamps and version.</returns>
        /// <remarks>
        /// - Validates key format and status
        /// - Updates rotation version
        /// - Implements optimistic concurrency
        /// - Tracks modification timestamp
        /// </remarks>
        Task<UserKey> UpdateKeyAsync(UserKey userKey);

        /// <summary>
        /// Records a key rotation event in the history with detailed tracking
        /// for audit and compliance purposes.
        /// </summary>
        /// <param name="keyHistory">The UserKeyHistory entity to create.</param>
        /// <returns>The created UserKeyHistory record with complete rotation details.</returns>
        /// <remarks>
        /// - Records rotation reason and timestamp
        /// - Tracks system version and initiator
        /// - Maintains compliance audit trail
        /// - Links to original key record
        /// </remarks>
        Task<UserKeyHistory> AddKeyHistoryAsync(UserKeyHistory keyHistory);

        /// <summary>
        /// Retrieves the complete rotation history for a user's keys with filtering
        /// and ordering capabilities for audit purposes.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>Collection of UserKeyHistory records ordered by rotation date.</returns>
        /// <remarks>
        /// - Orders by rotation date descending
        /// - Includes rotation reasons and initiators
        /// - Supports compliance reporting
        /// - Maintains complete audit trail
        /// </remarks>
        Task<IEnumerable<UserKeyHistory>> GetKeyHistoryAsync(long userId);
    }
}