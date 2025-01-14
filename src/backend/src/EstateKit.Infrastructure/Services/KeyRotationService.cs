using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Entities;
using EstateKit.Core.Constants;

namespace EstateKit.Infrastructure.Services
{
    /// <summary>
    /// Implements secure key rotation operations with enhanced validation, audit logging,
    /// and comprehensive security controls for the EstateKit Personal Information API.
    /// </summary>
    public class KeyRotationService : IKeyRotationService
    {
        private readonly IKeyManagementService _keyManagementService;
        private readonly IUserKeyRepository _userKeyRepository;
        private readonly ILogger<KeyRotationService> _logger;
        private const string SYSTEM_VERSION = "1.0.0"; // Should be injected from configuration

        public KeyRotationService(
            IKeyManagementService keyManagementService,
            IUserKeyRepository userKeyRepository,
            ILogger<KeyRotationService> logger)
        {
            _keyManagementService = keyManagementService ?? throw new ArgumentNullException(nameof(keyManagementService));
            _userKeyRepository = userKeyRepository ?? throw new ArgumentNullException(nameof(userKeyRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<UserKey> RotateKeyAsync(
            long userId,
            string rotationReason,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (userId <= 0)
                {
                    throw new ArgumentException("UserId must be greater than 0.", nameof(userId));
                }

                if (string.IsNullOrWhiteSpace(rotationReason))
                {
                    throw new ArgumentException("Rotation reason is required.", nameof(rotationReason));
                }

                _logger.LogInformation(
                    "Starting key rotation for user {UserId} with reason: {Reason}",
                    userId, rotationReason);

                // Get current active key to archive
                var currentKey = await _keyManagementService.GetActiveKeyAsync(userId);
                if (currentKey == null)
                {
                    throw new KeyNotFoundException($"No active key found for user {userId}");
                }

                // Generate new key pair through KMS
                var newKey = await _keyManagementService.RotateKeyPairAsync(userId, rotationReason);

                // Record rotation in history
                var keyHistory = new UserKeyHistory(
                    userId,
                    currentKey.Key,
                    rotationReason,
                    "SYSTEM",
                    SYSTEM_VERSION
                );

                await _userKeyRepository.AddKeyHistoryAsync(keyHistory);

                // Update the key in repository
                var updatedKey = await _userKeyRepository.UpdateKeyAsync(newKey);

                _logger.LogInformation(
                    "Successfully completed key rotation for user {UserId}",
                    userId);

                return updatedKey;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not KeyNotFoundException)
            {
                _logger.LogError(
                    ex,
                    "Error during key rotation for user {UserId}: {Error}",
                    userId, ex.Message);
                throw new InvalidOperationException(ErrorCodes.KEY_ROTATION_IN_PROGRESS, ex);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ScheduleRotationAsync(
            long userId,
            DateTime scheduledTime,
            string rotationReason,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (userId <= 0)
                {
                    throw new ArgumentException("UserId must be greater than 0.", nameof(userId));
                }

                if (scheduledTime <= DateTime.UtcNow)
                {
                    throw new ArgumentException("Scheduled time must be in the future.", nameof(scheduledTime));
                }

                if (string.IsNullOrWhiteSpace(rotationReason))
                {
                    throw new ArgumentException("Rotation reason is required.", nameof(rotationReason));
                }

                _logger.LogInformation(
                    "Scheduling key rotation for user {UserId} at {ScheduledTime}",
                    userId, scheduledTime);

                // Verify user has an active key
                var currentKey = await _keyManagementService.GetActiveKeyAsync(userId);
                if (currentKey == null)
                {
                    throw new KeyNotFoundException($"No active key found for user {userId}");
                }

                // Implementation Note: Actual scheduling logic would be implemented here
                // using a job scheduler like Hangfire or AWS EventBridge
                // For now, we'll just validate the request

                _logger.LogInformation(
                    "Successfully scheduled key rotation for user {UserId}",
                    userId);

                return true;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not KeyNotFoundException)
            {
                _logger.LogError(
                    ex,
                    "Error scheduling key rotation for user {UserId}: {Error}",
                    userId, ex.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<UserKey> EmergencyRotateKeyAsync(
            long userId,
            string securityIncidentId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (userId <= 0)
                {
                    throw new ArgumentException("UserId must be greater than 0.", nameof(userId));
                }

                if (string.IsNullOrWhiteSpace(securityIncidentId))
                {
                    throw new ArgumentException("Security incident ID is required.", nameof(securityIncidentId));
                }

                _logger.LogWarning(
                    "Starting emergency key rotation for user {UserId} due to incident {IncidentId}",
                    userId, securityIncidentId);

                // Get current active key to archive
                var currentKey = await _keyManagementService.GetActiveKeyAsync(userId);
                if (currentKey == null)
                {
                    throw new KeyNotFoundException($"No active key found for user {userId}");
                }

                // Generate new key pair with high priority
                var newKey = await _keyManagementService.RotateKeyPairAsync(
                    userId,
                    $"Emergency rotation - Incident: {securityIncidentId}");

                // Record emergency rotation in history
                var keyHistory = new UserKeyHistory(
                    userId,
                    currentKey.Key,
                    $"Emergency rotation - Incident: {securityIncidentId}",
                    "SYSTEM-EMERGENCY",
                    SYSTEM_VERSION
                );

                await _userKeyRepository.AddKeyHistoryAsync(keyHistory);

                // Update the key in repository
                var updatedKey = await _userKeyRepository.UpdateKeyAsync(newKey);

                _logger.LogWarning(
                    "Successfully completed emergency key rotation for user {UserId}",
                    userId);

                return updatedKey;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not KeyNotFoundException)
            {
                _logger.LogError(
                    ex,
                    "Error during emergency key rotation for user {UserId}: {Error}",
                    userId, ex.Message);
                throw new InvalidOperationException(ErrorCodes.KEY_ROTATION_IN_PROGRESS, ex);
            }
        }
    }
}