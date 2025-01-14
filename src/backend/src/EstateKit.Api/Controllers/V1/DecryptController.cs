using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EstateKit.Api.DTOs;
using EstateKit.Core.Interfaces;

namespace EstateKit.Api.Controllers.V1
{
    /// <summary>
    /// Controller handling decryption requests for sensitive personal information using AWS KMS
    /// with FIPS 140-2 compliance, performance monitoring, and security controls.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class DecryptController : ControllerBase
    {
        private readonly IEncryptionService _encryptionService;
        private readonly ActivitySource _activitySource;
        private const int MaxRequestTimeoutMs = 3000; // 3 seconds SLA requirement

        /// <summary>
        /// Initializes a new instance of the <see cref="DecryptController"/> class.
        /// </summary>
        /// <param name="encryptionService">FIPS 140-2 compliant encryption service.</param>
        /// <exception cref="ArgumentNullException">Thrown if encryptionService is null.</exception>
        public DecryptController(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _activitySource = new ActivitySource("EstateKit.Api.DecryptController", "1.0.0");
        }

        /// <summary>
        /// Decrypts an array of encrypted strings for a specific user with performance monitoring
        /// and security controls.
        /// </summary>
        /// <param name="request">The decryption request containing user ID and encrypted data.</param>
        /// <returns>
        /// ActionResult containing the decrypted string array or appropriate error response.
        /// </returns>
        [HttpPost]
        [ProducesResponseType(typeof(DecryptResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DecryptResponseDto>> DecryptAsync([FromBody] DecryptRequestDto request)
        {
            using var activity = _activitySource.StartActivity("DecryptAsync");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate request model state
                if (!ModelState.IsValid)
                {
                    activity?.SetTag("error", "InvalidModelState");
                    return BadRequest(ModelState);
                }

                // Validate request data format
                if (!request.ValidateEncryptedData())
                {
                    activity?.SetTag("error", "InvalidDataFormat");
                    return BadRequest("Invalid encrypted data format");
                }

                // Add request metadata to activity
                activity?.SetTag("userId", request.UserId);
                activity?.SetTag("dataCount", request.EncryptedData.Length);

                // Perform decryption operation
                var decryptedData = await _encryptionService.DecryptAsync(
                    request.UserId,
                    request.EncryptedData
                );

                // Check SLA compliance
                stopwatch.Stop();
                var responseTime = stopwatch.ElapsedMilliseconds;
                activity?.SetTag("responseTime", responseTime);

                if (responseTime > MaxRequestTimeoutMs)
                {
                    activity?.SetTag("slaViolation", true);
                    // Log SLA violation but still return successful response
                    // Actual logging implementation would be injected via ILogger
                }

                return Ok(new DecryptResponseDto 
                { 
                    DecryptedData = decryptedData 
                });
            }
            catch (ArgumentNullException ex)
            {
                activity?.SetTag("error", "ArgumentNull");
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                activity?.SetTag("error", "InvalidArgument");
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                activity?.SetTag("error", "KeyNotFound");
                return NotFound(ex.Message);
            }
            catch (KeyRotationInProgressException ex)
            {
                activity?.SetTag("error", "KeyRotationInProgress");
                return StatusCode(StatusCodes.Status409Conflict, ex.Message);
            }
            catch (EncryptionException ex)
            {
                activity?.SetTag("error", "EncryptionError");
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
            catch (Exception ex)
            {
                activity?.SetTag("error", "UnhandledException");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "An unexpected error occurred while processing your request.");
            }
        }
    }
}