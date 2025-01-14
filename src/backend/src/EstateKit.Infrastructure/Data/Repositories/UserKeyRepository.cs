using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Entities;
using EstateKit.Core.Constants;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Enhanced repository implementation for managing user encryption keys with comprehensive
    /// security controls, audit logging, and compliance tracking capabilities.
    /// </summary>
    public class UserKeyRepository : IUserKeyRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserKeyRepository> _logger;
        private const int KEY_EXPIRATION_DAYS = 90;

        public UserKeyRepository(ApplicationDbContext context, ILogger<UserKeyRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<UserKey> GetActiveKeyAsync(long userId)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve active key for user {UserId}", userId);

                if (userId <= 0)
                {
                    throw new ArgumentException("Invalid user ID", nameof(userId));
                }

                var activeKey = await _context.UserKeys
                    .Where(k => k.UserId == userId && k.IsActive)
                    .SingleOrDefaultAsync();

                if (activeKey == null)
                {
                    _logger.LogWarning("No active key found for user {UserId}", userId);
                    return null;
                }

                // Check key expiration (90-day policy)
                if ((DateTime.UtcNow - activeKey.CreatedAt).TotalDays > KEY_EXPIRATION_DAYS)
                {
                    _logger.LogWarning("Active key for user {UserId} has expired", userId);
                    activeKey.Deactivate();
                    await _context.SaveChangesAsync();
                    return null;
                }

                _logger.LogInformation("Successfully retrieved active key for user {UserId}", userId);
                return activeKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active key for user {UserId}", userId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<UserKey> CreateKeyAsync(UserKey userKey)
        {
            if (userKey == null)
            {
                throw new ArgumentNullException(nameof(userKey));
            }

            if (!UserKey.ValidateKey(userKey.Key))
            {
                _logger.LogError("Invalid key format attempted for user {UserId}", userKey.UserId);
                throw new ArgumentException("Invalid key format", nameof(userKey));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Deactivate any existing active keys
                var existingActiveKey = await _context.UserKeys
                    .Where(k => k.UserId == userKey.UserId && k.IsActive)
                    .SingleOrDefaultAsync();

                if (existingActiveKey != null)
                {
                    existingActiveKey.Deactivate();
                }

                await _context.UserKeys.AddAsync(userKey);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new key for user {UserId}", userKey.UserId);
                await transaction.CommitAsync();

                return userKey;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating key for user {UserId}", userKey.UserId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<UserKey> UpdateKeyAsync(UserKey userKey)
        {
            if (userKey == null)
            {
                throw new ArgumentNullException(nameof(userKey));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingKey = await _context.UserKeys
                    .Where(k => k.UserId == userKey.UserId && k.IsActive)
                    .SingleOrDefaultAsync();

                if (existingKey == null)
                {
                    throw new InvalidOperationException($"No active key found for user {userKey.UserId}");
                }

                _context.Entry(existingKey).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated key for user {UserId}", userKey.UserId);
                await transaction.CommitAsync();

                return existingKey;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Concurrency conflict updating key for user {UserId}", userKey.UserId);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating key for user {UserId}", userKey.UserId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<UserKeyHistory> AddKeyHistoryAsync(UserKeyHistory keyHistory)
        {
            if (keyHistory == null)
            {
                throw new ArgumentNullException(nameof(keyHistory));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.UserKeyHistory.AddAsync(keyHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Recorded key rotation history for user {UserId}, reason: {Reason}",
                    keyHistory.UserId,
                    keyHistory.RotationReason
                );

                await transaction.CommitAsync();
                return keyHistory;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error recording key history for user {UserId}", keyHistory.UserId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<UserKeyHistory>> GetKeyHistoryAsync(long userId)
        {
            try
            {
                _logger.LogInformation("Retrieving key rotation history for user {UserId}", userId);

                if (userId <= 0)
                {
                    throw new ArgumentException("Invalid user ID", nameof(userId));
                }

                var history = await _context.UserKeyHistory
                    .Where(h => h.UserId == userId)
                    .OrderByDescending(h => h.RotationDate)
                    .ToListAsync();

                _logger.LogInformation(
                    "Retrieved {Count} key rotation records for user {UserId}",
                    history.Count,
                    userId
                );

                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving key history for user {UserId}", userId);
                throw;
            }
        }
    }
}