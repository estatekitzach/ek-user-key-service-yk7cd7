using System;
using System.Threading;
using System.Threading.Tasks;
using EstateKit.Core.Entities;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for managing encryption key rotation operations in the EstateKit system.
    /// Supports scheduled, on-demand, compliance-based, and emergency key rotations with comprehensive
    /// validation, error handling, and audit trail capabilities.
    /// </summary>
    public interface IKeyRotationService
    {
        /// <summary>
        /// Performs an on-demand rotation of a user's encryption key with validation and audit logging.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose key needs rotation.</param>
        /// <param name="rotationReason">The business reason for key rotation (e.g., "Regular 90-day rotation").</param>
        /// <param name="cancellationToken">Optional cancellation token for async operation.</param>
        /// <returns>The newly generated and activated UserKey after successful rotation.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is less than or equal to 0 or rotationReason is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when rotation is already in progress (KEY002).</exception>
        /// <exception cref="Exception">Thrown when key rotation fails due to system errors (SYS001).</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
        Task<UserKey> RotateKeyAsync(
            long userId,
            string rotationReason,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedules an automatic key rotation for compliance or regular rotation with validation.
        /// Supports scheduling future rotations with configurable timing and reason tracking.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose key needs scheduled rotation.</param>
        /// <param name="scheduledTime">The UTC datetime when the rotation should occur.</param>
        /// <param name="rotationReason">The compliance or business reason for scheduled rotation.</param>
        /// <param name="cancellationToken">Optional cancellation token for async operation.</param>
        /// <returns>True if scheduling was successful, false if scheduling failed with error details.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is invalid or scheduledTime is in the past.</exception>
        /// <exception cref="Exception">Thrown when scheduling fails due to system errors.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
        Task<bool> ScheduleRotationAsync(
            long userId,
            DateTime scheduledTime,
            string rotationReason,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Immediately rotates a user's encryption key in response to security incidents or compromises.
        /// Includes incident tracking, audit logging, and emergency response procedures.
        /// </summary>
        /// <param name="userId">The unique identifier of the user requiring emergency key rotation.</param>
        /// <param name="securityIncidentId">The identifier of the security incident triggering rotation.</param>
        /// <param name="cancellationToken">Optional cancellation token for async operation.</param>
        /// <returns>The newly generated and activated UserKey after emergency rotation.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is invalid or securityIncidentId format is incorrect.</exception>
        /// <exception cref="InvalidOperationException">Thrown when rotation is already in progress (KEY002).</exception>
        /// <exception cref="Exception">Thrown when emergency rotation fails due to system errors (SYS001).</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
        Task<UserKey> EmergencyRotateKeyAsync(
            long userId,
            string securityIncidentId,
            CancellationToken cancellationToken = default);
    }
}