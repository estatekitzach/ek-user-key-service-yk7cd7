using System;
using System.Net;
using Amazon.Runtime;
using EstateKit.Core.Constants;
using EstateKit.Core.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace EstateKit.Api.Filters
{
    /// <summary>
    /// Global exception filter that provides standardized error handling, secure logging,
    /// and environment-aware error responses for the EstateKit Personal Information API.
    /// </summary>
    public class ApiExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ApiExceptionFilter> _logger;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the ApiExceptionFilter with required dependencies.
        /// </summary>
        /// <param name="logger">Logger for structured exception logging.</param>
        /// <param name="environment">Web host environment for determining error detail exposure.</param>
        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IWebHostEnvironment environment)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary>
        /// Handles exceptions by converting them to standardized API responses with appropriate
        /// security measures and logging.
        /// </summary>
        /// <param name="context">The exception context containing the current request and exception details.</param>
        public void OnException(ExceptionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var correlationId = Guid.NewGuid().ToString();
            var exception = context.Exception;

            // Log exception with correlation ID and request context
            _logger.LogError(exception,
                "Error processing request. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
                correlationId,
                context.HttpContext.Request.Path,
                context.HttpContext.Request.Method);

            var (statusCode, errorCode) = DetermineStatusCodeAndErrorCode(exception);
            var errorResponse = CreateErrorResponse(exception, errorCode, correlationId);

            var result = new ObjectResult(errorResponse)
            {
                StatusCode = (int)statusCode
            };

            // Add correlation ID to response headers for request tracking
            context.HttpContext.Response.Headers.Add("X-Correlation-ID", correlationId);
            context.Result = result;
            context.ExceptionHandled = true;
        }

        /// <summary>
        /// Creates a standardized error response object with security considerations.
        /// </summary>
        private object CreateErrorResponse(Exception exception, string errorCode, string correlationId)
        {
            var response = new
            {
                ErrorCode = errorCode,
                Message = GetSanitizedMessage(exception),
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                RecoveryAction = GetRecoveryAction(exception, errorCode),
                // Only include stack trace in development environment
                StackTrace = _environment.IsDevelopment() ? exception.StackTrace : null
            };

            return response;
        }

        /// <summary>
        /// Determines appropriate HTTP status code and error code based on exception type.
        /// </summary>
        private (HttpStatusCode statusCode, string errorCode) DetermineStatusCodeAndErrorCode(Exception exception)
        {
            return exception switch
            {
                KeyNotFoundException keyNotFound => 
                    (HttpStatusCode.NotFound, keyNotFound.ErrorCode),
                
                KeyRotationInProgressException keyRotation => 
                    (HttpStatusCode.Conflict, keyRotation.ErrorCode),
                
                ArgumentException _ => 
                    (HttpStatusCode.BadRequest, ErrorCodes.INVALID_INPUT_FORMAT),
                
                AmazonServiceException _ => 
                    (HttpStatusCode.ServiceUnavailable, ErrorCodes.KMS_SERVICE_ERROR),
                
                _ => (HttpStatusCode.InternalServerError, ErrorCodes.KMS_SERVICE_ERROR)
            };
        }

        /// <summary>
        /// Returns a sanitized error message safe for client consumption.
        /// </summary>
        private string GetSanitizedMessage(Exception exception)
        {
            // For security, only return specific exception messages for known exception types
            return exception switch
            {
                KeyNotFoundException keyNotFound => keyNotFound.Message,
                KeyRotationInProgressException keyRotation => keyRotation.Message,
                ArgumentException argEx => "Invalid input format. Please verify request parameters.",
                AmazonServiceException _ => "An error occurred while processing the encryption service request.",
                _ => "An unexpected error occurred. Please try again later."
            };
        }

        /// <summary>
        /// Provides appropriate recovery action guidance based on exception type.
        /// </summary>
        private string GetRecoveryAction(Exception exception, string errorCode)
        {
            return errorCode switch
            {
                ErrorCodes.KEY_NOT_FOUND => 
                    "Generate new key or verify key identifier",
                
                ErrorCodes.KEY_ROTATION_IN_PROGRESS => 
                    "Retry request after brief delay",
                
                ErrorCodes.INVALID_INPUT_FORMAT => 
                    "Validate input format against API specifications",
                
                ErrorCodes.KMS_SERVICE_ERROR => 
                    "Contact system support or retry after service restoration",
                
                _ => "Please try again later or contact support if the issue persists"
            };
        }
    }
}