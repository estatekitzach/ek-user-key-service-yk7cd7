using System;
using System.Threading.Tasks;
using EstateKit.Api.Controllers.V1;
using EstateKit.Api.DTOs;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EstateKit.Api.Tests.Controllers
{
    /// <summary>
    /// Comprehensive test suite for KeyController validating key management operations,
    /// error handling, and security scenarios.
    /// </summary>
    public class KeyControllerTests
    {
        private readonly Mock<IKeyManagementService> _keyManagementServiceMock;
        private readonly Mock<ILogger<KeyController>> _loggerMock;
        private readonly KeyController _controller;

        public KeyControllerTests()
        {
            _keyManagementServiceMock = new Mock<IKeyManagementService>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger<KeyController>>();
            _controller = new KeyController(_keyManagementServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GenerateKeyAsync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new KeyGenerationRequestDto { UserId = 123 };
            var userKey = new UserKey(request.UserId, "validKey123");

            _keyManagementServiceMock
                .Setup(x => x.GenerateKeyPairAsync(request.UserId))
                .ReturnsAsync(userKey);

            // Act
            var result = await _controller.GenerateKeyAsync(request);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<KeyGenerationResponseDto>().Subject;

            response.Success.Should().BeTrue();
            response.UserId.Should().Be(request.UserId);
            response.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            response.NextRotationDue.Should().BeCloseTo(response.GeneratedAt.AddDays(90), TimeSpan.FromSeconds(1));

            _keyManagementServiceMock.Verify(x => x.GenerateKeyPairAsync(request.UserId), Times.Once);
        }

        [Fact]
        public async Task GenerateKeyAsync_InvalidUserId_ReturnsBadRequest()
        {
            // Arrange
            var request = new KeyGenerationRequestDto { UserId = 0 };

            _keyManagementServiceMock
                .Setup(x => x.GenerateKeyPairAsync(request.UserId))
                .ThrowsAsync(new ArgumentException("Invalid user ID"));

            // Act
            var result = await _controller.GenerateKeyAsync(request);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;

            problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
            problemDetails.Title.Should().Be("Invalid Request");
            problemDetails.Type.Should().Be("https://api.estatekit.com/errors/invalid-request");
        }

        [Fact]
        public async Task GenerateKeyAsync_ExistingActiveKey_ReturnsConflict()
        {
            // Arrange
            var request = new KeyGenerationRequestDto { UserId = 123 };

            _keyManagementServiceMock
                .Setup(x => x.GenerateKeyPairAsync(request.UserId))
                .ThrowsAsync(new InvalidOperationException("Active key exists"));

            // Act
            var result = await _controller.GenerateKeyAsync(request);

            // Assert
            var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
            var problemDetails = conflictResult.Value.Should().BeOfType<ProblemDetails>().Subject;

            problemDetails.Status.Should().Be(StatusCodes.Status409Conflict);
            problemDetails.Title.Should().Be("Operation Failed");
            problemDetails.Type.Should().Be("https://api.estatekit.com/errors/conflict");
        }

        [Fact]
        public async Task GetActiveKeyAsync_ExistingKey_ReturnsKey()
        {
            // Arrange
            long userId = 123;
            var userKey = new UserKey(userId, "validKey123");

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(userId))
                .ReturnsAsync(userKey);

            // Act
            var result = await _controller.GetActiveKeyAsync(userId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedKey = okResult.Value.Should().BeOfType<UserKey>().Subject;

            returnedKey.UserId.Should().Be(userId);
            returnedKey.Key.Should().Be("validKey123");
            returnedKey.IsActive.Should().BeTrue();

            _keyManagementServiceMock.Verify(x => x.GetActiveKeyAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetActiveKeyAsync_KeyNotFound_ReturnsNotFound()
        {
            // Arrange
            long userId = 123;

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(userId))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _controller.GetActiveKeyAsync(userId);

            // Assert
            var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;

            problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
            problemDetails.Title.Should().Be("Key Not Found");
            problemDetails.Type.Should().Be("https://api.estatekit.com/errors/key-not-found");
        }

        [Fact]
        public async Task RotateKeyAsync_ValidRequest_ReturnsNewKey()
        {
            // Arrange
            long userId = 123;
            string rotationReason = "scheduled";
            var rotatedKey = new UserKey(userId, "newKey123");

            _keyManagementServiceMock
                .Setup(x => x.RotateKeyPairAsync(userId, rotationReason))
                .ReturnsAsync(rotatedKey);

            // Act
            var result = await _controller.RotateKeyAsync(userId, rotationReason);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<KeyGenerationResponseDto>().Subject;

            response.Success.Should().BeTrue();
            response.UserId.Should().Be(userId);
            response.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            response.NextRotationDue.Should().BeCloseTo(response.GeneratedAt.AddDays(90), TimeSpan.FromSeconds(1));

            _keyManagementServiceMock.Verify(x => x.RotateKeyPairAsync(userId, rotationReason), Times.Once);
        }

        [Fact]
        public async Task RotateKeyAsync_RotationInProgress_ReturnsConflict()
        {
            // Arrange
            long userId = 123;
            string rotationReason = "scheduled";

            _keyManagementServiceMock
                .Setup(x => x.RotateKeyPairAsync(userId, rotationReason))
                .ThrowsAsync(new InvalidOperationException(ErrorCodes.KEY_ROTATION_IN_PROGRESS));

            // Act
            var result = await _controller.RotateKeyAsync(userId, rotationReason);

            // Assert
            var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
            var problemDetails = conflictResult.Value.Should().BeOfType<ProblemDetails>().Subject;

            problemDetails.Status.Should().Be(StatusCodes.Status409Conflict);
            problemDetails.Title.Should().Be("Rotation In Progress");
            problemDetails.Type.Should().Be("https://api.estatekit.com/errors/rotation-in-progress");
        }

        [Fact]
        public async Task RotateKeyAsync_KeyNotFound_ReturnsNotFound()
        {
            // Arrange
            long userId = 123;
            string rotationReason = "scheduled";

            _keyManagementServiceMock
                .Setup(x => x.RotateKeyPairAsync(userId, rotationReason))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _controller.RotateKeyAsync(userId, rotationReason);

            // Assert
            var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;

            problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
            problemDetails.Title.Should().Be("Key Not Found");
            problemDetails.Type.Should().Be("https://api.estatekit.com/errors/key-not-found");
        }

        [Fact]
        public async Task GenerateKeyAsync_KmsServiceError_ReturnsInternalServerError()
        {
            // Arrange
            var request = new KeyGenerationRequestDto { UserId = 123 };

            _keyManagementServiceMock
                .Setup(x => x.GenerateKeyPairAsync(request.UserId))
                .ThrowsAsync(new ApplicationException(ErrorCodes.KMS_SERVICE_ERROR));

            // Act
            var result = await _controller.GenerateKeyAsync(request);

            // Assert
            var serverErrorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            var problemDetails = serverErrorResult.Value.Should().BeOfType<ProblemDetails>().Subject;

            serverErrorResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            problemDetails.Title.Should().Be("Internal Server Error");
            problemDetails.Type.Should().Be("https://api.estatekit.com/errors/server-error");
        }
    }
}