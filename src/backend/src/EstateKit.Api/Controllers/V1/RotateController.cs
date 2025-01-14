using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using EstateKit.Api.DTOs;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Constants;

namespace EstateKit.Api.Controllers.V1
{
    /// <summary>
    /// Controller responsible for managing encryption key rotation operations.
    /// Supports scheduled, on-demand, and emergency key rotations with comprehensive validation and audit logging.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    [EnableRateLimiting("rotation-policy")]
    public class RotateController : ControllerBase
    {
        private readonly IKeyRotationService _keyRotationService;
        private readonly ILogger<RotateController> _logger;

        /// <summary>
        /// Initializes a new instance of the RotateController with required dependencies.
        /// </summary>
        /// <param name="keyRotationService">Service for managing key rotation operations</param>
        /// <param name="logger">Logger for operation tracking and monitoring</param>
        /// <exception cref="ArgumentNullException">Thrown when required dependencies are null</exception>
        public RotateController(
            IKeyRotationService keyRotationService,
            ILogger<RotateController> logger)
        {
            _keyRotationService = keyRotationService ?? throw new ArgumentNullException(nameof(keyRotationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles key rotation requests with support for immediate, scheduled, and emergency scenarios.
        /// </summary>
        /// <param name="request">The key rotation request parameters</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Result of the key rotation operation with audit information</returns>
        [HttpPost]
        [ProducesResponseType(typeof(RotateKeyResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [EnableRateLimiting("rotation-policy")]
        public async Task<ActionResult<RotateKeyResponseDto>> RotateKeyAsync(
            [FromBody] RotateKeyRequestDto request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Key rotation request initiated for user {UserId} with reason: {Reason}",
                    request.UserId,
                    request.RotationReason);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Handle emergency rotation with security incident
                if (!string.IsNullOrEmpty(request.SecurityIncidentId))
                {
                    _logger.LogWarning(
                        "Emergency key rotation triggered for user {UserId} due to security incident {IncidentId}",
                        request.UserId,
                        request.SecurityIncidentId);

                    var emergencyResult = await _keyRotationService.EmergencyRotateKeyAsync(
                        request.UserId,
                        request.SecurityIncidentId,
                        cancellationToken);

                    return Ok(new RotateKeyResponseDto(
                        true,
                        request.UserId,
                        "emergency"));
                }

                // Handle scheduled rotation
                if (request.ScheduledTime.HasValue)
                {
                    _logger.LogInformation(
                        "Scheduling key rotation for user {UserId} at {ScheduledTime}",
                        request.UserId,
                        request.ScheduledTime);

                    var schedulingResult = await _keyRotationService.ScheduleRotationAsync(
                        request.UserId,
                        request.ScheduledTime.Value,
                        request.RotationReason,
                        cancellationToken);

                    return Ok(new RotateKeyResponseDto(
                        schedulingResult,
                        request.UserId,
                        "scheduled"));
                }

                // Handle standard rotation
                var result = await _keyRotationService.RotateKeyAsync(
                    request.UserId,
                    request.RotationReason,
                    cancellationToken);

                return Ok(new RotateKeyResponseDto(
                    true,
                    request.UserId,
                    "on-demand"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(
                    ex,
                    "Invalid rotation request for user {UserId}: {Message}",
                    request.UserId,
                    ex.Message);
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    { "RotationRequest", new[] { ex.Message } }
                }));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains(ErrorCodes.KEY_ROTATION_IN_PROGRESS))
            {
                _logger.LogWarning(
                    ex,
                    "Concurrent rotation attempt for user {UserId}",
                    request.UserId);
                return Conflict(new ProblemDetails
                {
                    Title = "Key Rotation In Progress",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://api.estatekit.com/errors/key-rotation-in-progress"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Key rotation failed for user {UserId}: {Message}",
                    request.UserId,
                    ex.Message);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new ProblemDetails
                    {
                        Title = "Key Rotation Failed",
                        Detail = "An unexpected error occurred during key rotation",
                        Status = StatusCodes.Status500InternalServerError,
                        Type = "https://api.estatekit.com/errors/internal-server-error"
                    });
            }
        }
    }
}