using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using EstateKit.Api.DTOs;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Exceptions;
using System.Diagnostics;

namespace EstateKit.Api.Controllers.V1
{
    /// <summary>
    /// Controller handling encryption requests for the EstateKit Personal Information API.
    /// Provides endpoints for batch encryption of string arrays using user-specific public keys.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    public class EncryptController : ControllerBase
    {
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<EncryptController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptController"/> class.
        /// </summary>
        /// <param name="encryptionService">FIPS 140-2 compliant encryption service.</param>
        /// <param name="logger">Logger for telemetry and monitoring.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required dependency is null.</exception>
        public EncryptController(
            IEncryptionService encryptionService,
            ILogger<EncryptController> logger)
        {
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Encrypts an array of strings using the user's public key.
        /// </summary>
        /// <param name="request">The encryption request containing user ID and data to encrypt.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>ActionResult containing the encrypted data or appropriate error response.</returns>
        /// <response code="200">Returns the encrypted data array.</response>
        /// <response code="400">If the request is invalid or contains invalid data.</response>
        /// <response code="401">If the request is not properly authenticated.</response>
        /// <response code="404">If the user's encryption key is not found.</response>
        /// <response code="409">If key rotation is in progress for the user.</response>
        /// <response code="500">If an unexpected error occurs during encryption.</response>
        [HttpPost]
        [ProducesResponseType(typeof(EncryptResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EncryptResponseDto>> EncryptAsync(
            [FromBody] EncryptRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var correlationId = HttpContext.TraceIdentifier;
            _logger.LogInformation(
                "Starting encryption request for user {UserId}. CorrelationId: {CorrelationId}",
                request.UserId,
                correlationId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Invalid model state for encryption request. CorrelationId: {CorrelationId}",
                    correlationId);
                return BadRequest(ModelState);
            }

            if (request.Data == null || request.Data.Length == 0)
            {
                _logger.LogWarning(
                    "Empty data array provided for encryption. CorrelationId: {CorrelationId}",
                    correlationId);
                return BadRequest("Data array cannot be null or empty.");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var encryptedData = await _encryptionService.EncryptAsync(
                    request.UserId,
                    request.Data);

                stopwatch.Stop();
                _logger.LogInformation(
                    "Successfully encrypted {Count} items for user {UserId} in {ElapsedMs}ms. CorrelationId: {CorrelationId}",
                    request.Data.Length,
                    request.UserId,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);

                return Ok(new EncryptResponseDto(encryptedData));
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(
                    ex,
                    "Encryption key not found for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId,
                    correlationId);
                return NotFound($"Encryption key not found for user {request.UserId}");
            }
            catch (KeyRotationInProgressException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Key rotation in progress for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId,
                    correlationId);
                return Conflict("Key rotation is in progress. Please retry after a few moments.");
            }
            catch (InvalidKeyException ex)
            {
                _logger.LogError(
                    ex,
                    "Invalid encryption key for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId,
                    correlationId);
                return BadRequest("Invalid encryption key.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error during encryption for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId,
                    correlationId);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred during encryption.");
            }
            finally
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                // Log if operation exceeded performance threshold (3 seconds)
                if (stopwatch.ElapsedMilliseconds > 3000)
                {
                    _logger.LogWarning(
                        "Encryption operation exceeded performance threshold. Duration: {ElapsedMs}ms. CorrelationId: {CorrelationId}",
                        stopwatch.ElapsedMilliseconds,
                        correlationId);
                }
            }
        }
    }
}