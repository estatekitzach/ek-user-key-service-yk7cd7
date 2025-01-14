using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;
using EstateKit.Api.DTOs;

namespace EstateKit.Api.Controllers.V1
{
    /// <summary>
    /// Controller responsible for managing encryption key operations including generation,
    /// rotation, and retrieval with comprehensive security and monitoring capabilities.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class KeyController : ControllerBase
    {
        private readonly IKeyManagementService _keyManagementService;
        private readonly ILogger<KeyController> _logger;

        /// <summary>
        /// Initializes a new instance of the KeyController with required dependencies.
        /// </summary>
        /// <param name="keyManagementService">Service for FIPS 140-2 compliant key operations.</param>
        /// <param name="logger">Logger for structured logging and monitoring.</param>
        /// <exception cref="ArgumentNullException">Thrown if required dependencies are null.</exception>
        public KeyController(IKeyManagementService keyManagementService, ILogger<KeyController> logger)
        {
            _keyManagementService = keyManagementService ?? throw new ArgumentNullException(nameof(keyManagementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generates a new FIPS 140-2 compliant encryption key pair for a user.
        /// </summary>
        /// <param name="request">Key generation request containing user identifier.</param>
        /// <returns>Key generation response with success status and rotation metadata.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(KeyGenerationResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<KeyGenerationResponseDto>> GenerateKeyAsync([FromBody] KeyGenerationRequestDto request)
        {
            try
            {
                _logger.LogInformation("Initiating key generation for user {UserId}", request.UserId);

                var userKey = await _keyManagementService.GenerateKeyPairAsync(request.UserId);

                var response = new KeyGenerationResponseDto
                {
                    Success = true,
                    UserId = userKey.UserId,
                    KeyVersion = userKey.RotationVersion,
                    GeneratedAt = userKey.CreatedAt,
                    NextRotationDue = userKey.CreatedAt.AddDays(90) // 90-day rotation per requirements
                };

                _logger.LogInformation("Successfully generated key for user {UserId}", request.UserId);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for user {UserId}", request.UserId);
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://api.estatekit.com/errors/invalid-request"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Operation failed for user {UserId}", request.UserId);
                return Conflict(new ProblemDetails
                {
                    Title = "Operation Failed",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://api.estatekit.com/errors/conflict"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Key generation failed for user {UserId}", request.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "Key generation failed. Please try again later.",
                    Status = StatusCodes.Status500InternalServerError,
                    Type = "https://api.estatekit.com/errors/server-error"
                });
            }
        }

        /// <summary>
        /// Retrieves the active encryption key for a user with caching support.
        /// </summary>
        /// <param name="userId">Unique identifier of the user.</param>
        /// <returns>Active encryption key if found.</returns>
        [HttpGet("{userId}")]
        [ProducesResponseType(typeof(UserKey), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ResponseCache(Duration = 300)] // 5-minute cache for active keys
        public async Task<ActionResult<UserKey>> GetActiveKeyAsync(long userId)
        {
            try
            {
                _logger.LogInformation("Retrieving active key for user {UserId}", userId);

                var userKey = await _keyManagementService.GetActiveKeyAsync(userId);
                
                // Add ETag for caching
                var eTag = $"W/\"{userKey.RotationVersion}\"";
                Response.Headers.ETag = eTag;

                return Ok(userKey);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Active key not found for user {UserId}", userId);
                return NotFound(new ProblemDetails
                {
                    Title = "Key Not Found",
                    Detail = $"No active key found for user {userId}",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://api.estatekit.com/errors/key-not-found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve key for user {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "Key retrieval failed. Please try again later.",
                    Status = StatusCodes.Status500InternalServerError,
                    Type = "https://api.estatekit.com/errors/server-error"
                });
            }
        }

        /// <summary>
        /// Rotates the encryption key for a user with data re-encryption.
        /// </summary>
        /// <param name="userId">Unique identifier of the user.</param>
        /// <param name="rotationReason">Reason for key rotation.</param>
        /// <returns>Key rotation response with new key metadata.</returns>
        [HttpPost("{userId}/rotate")]
        [ProducesResponseType(typeof(KeyGenerationResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<KeyGenerationResponseDto>> RotateKeyAsync(long userId, [FromBody] string rotationReason)
        {
            try
            {
                _logger.LogInformation("Initiating key rotation for user {UserId}", userId);

                var rotatedKey = await _keyManagementService.RotateKeyPairAsync(userId, rotationReason);

                // Invalidate any cached keys
                Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");

                var response = new KeyGenerationResponseDto
                {
                    Success = true,
                    UserId = rotatedKey.UserId,
                    KeyVersion = rotatedKey.RotationVersion,
                    GeneratedAt = rotatedKey.UpdatedAt,
                    NextRotationDue = rotatedKey.UpdatedAt.AddDays(90)
                };

                _logger.LogInformation("Successfully rotated key for user {UserId}", userId);
                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Key not found for rotation - user {UserId}", userId);
                return NotFound(new ProblemDetails
                {
                    Title = "Key Not Found",
                    Detail = $"No active key found for user {userId}",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://api.estatekit.com/errors/key-not-found"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rotation in progress for user {UserId}", userId);
                return Conflict(new ProblemDetails
                {
                    Title = "Rotation In Progress",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://api.estatekit.com/errors/rotation-in-progress"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Key rotation failed for user {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "Key rotation failed. Please try again later.",
                    Status = StatusCodes.Status500InternalServerError,
                    Type = "https://api.estatekit.com/errors/server-error"
                });
            }
        }
    }
}