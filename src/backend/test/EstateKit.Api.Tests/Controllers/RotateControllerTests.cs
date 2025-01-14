using System;
using System.Threading;
using System.Threading.Tasks;
using EstateKit.Api.Controllers.V1;
using EstateKit.Api.DTOs;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EstateKit.Api.Tests.Controllers
{
    public class RotateControllerTests : IDisposable
    {
        private readonly Mock<IKeyRotationService> _mockKeyRotationService;
        private readonly Mock<ILogger<RotateController>> _mockLogger;
        private readonly RotateController _controller;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public RotateControllerTests()
        {
            _mockKeyRotationService = new Mock<IKeyRotationService>(MockBehavior.Strict);
            _mockLogger = new Mock<ILogger<RotateController>>();
            _cancellationTokenSource = new CancellationTokenSource();
            _controller = new RotateController(_mockKeyRotationService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task TestRotateKeyAsync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new RotateKeyRequestDto
            {
                UserId = 12345,
                RotationReason = "on-demand"
            };

            var userKey = new UserKey(request.UserId, "validKey123");

            _mockKeyRotationService
                .Setup(s => s.RotateKeyAsync(
                    request.UserId,
                    request.RotationReason,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(userKey);

            // Act
            var result = await _controller.RotateKeyAsync(request, _cancellationTokenSource.Token);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<RotateKeyResponseDto>().Subject;
            response.Success.Should().BeTrue();
            response.UserId.Should().Be(request.UserId);
            response.RotationReason.Should().Be("on-demand");

            _mockKeyRotationService.Verify(
                s => s.RotateKeyAsync(
                    request.UserId,
                    request.RotationReason,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TestRotateKeyAsync_EmergencyRotation_CallsEmergencyService()
        {
            // Arrange
            var request = new RotateKeyRequestDto
            {
                UserId = 12345,
                RotationReason = "emergency",
                SecurityIncidentId = "SEC-123-456-789"
            };

            var userKey = new UserKey(request.UserId, "emergencyKey123");

            _mockKeyRotationService
                .Setup(s => s.EmergencyRotateKeyAsync(
                    request.UserId,
                    request.SecurityIncidentId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(userKey);

            // Act
            var result = await _controller.RotateKeyAsync(request, _cancellationTokenSource.Token);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<RotateKeyResponseDto>().Subject;
            response.Success.Should().BeTrue();
            response.UserId.Should().Be(request.UserId);
            response.RotationReason.Should().Be("emergency");

            _mockKeyRotationService.Verify(
                s => s.EmergencyRotateKeyAsync(
                    request.UserId,
                    request.SecurityIncidentId,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TestRotateKeyAsync_ScheduledRotation_CallsScheduleService()
        {
            // Arrange
            var scheduledTime = DateTime.UtcNow.AddDays(1);
            var request = new RotateKeyRequestDto
            {
                UserId = 12345,
                RotationReason = "scheduled",
                ScheduledTime = scheduledTime
            };

            _mockKeyRotationService
                .Setup(s => s.ScheduleRotationAsync(
                    request.UserId,
                    request.ScheduledTime.Value,
                    request.RotationReason,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.RotateKeyAsync(request, _cancellationTokenSource.Token);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<RotateKeyResponseDto>().Subject;
            response.Success.Should().BeTrue();
            response.UserId.Should().Be(request.UserId);
            response.RotationReason.Should().Be("scheduled");

            _mockKeyRotationService.Verify(
                s => s.ScheduleRotationAsync(
                    request.UserId,
                    request.ScheduledTime.Value,
                    request.RotationReason,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TestRotateKeyAsync_ConcurrentRequests_HandledCorrectly()
        {
            // Arrange
            var request = new RotateKeyRequestDto
            {
                UserId = 12345,
                RotationReason = "on-demand"
            };

            _mockKeyRotationService
                .Setup(s => s.RotateKeyAsync(
                    request.UserId,
                    request.RotationReason,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(ErrorCodes.KEY_ROTATION_IN_PROGRESS));

            // Act
            var result = await _controller.RotateKeyAsync(request, _cancellationTokenSource.Token);

            // Assert
            var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
            var problem = conflictResult.Value.Should().BeOfType<ProblemDetails>().Subject;
            problem.Status.Should().Be(409);
            problem.Title.Should().Be("Key Rotation In Progress");
            problem.Type.Should().Be("https://api.estatekit.com/errors/key-rotation-in-progress");
        }

        [Fact]
        public async Task TestRotateKeyAsync_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var request = new RotateKeyRequestDto
            {
                UserId = -1, // Invalid user ID
                RotationReason = "on-demand"
            };

            _mockKeyRotationService
                .Setup(s => s.RotateKeyAsync(
                    request.UserId,
                    request.RotationReason,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid user ID"));

            // Act
            var result = await _controller.RotateKeyAsync(request, _cancellationTokenSource.Token);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var validationProblem = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
            validationProblem.Errors.Should().ContainKey("RotationRequest");
        }

        [Fact]
        public async Task TestRotateKeyAsync_SystemError_ReturnsInternalServerError()
        {
            // Arrange
            var request = new RotateKeyRequestDto
            {
                UserId = 12345,
                RotationReason = "on-demand"
            };

            _mockKeyRotationService
                .Setup(s => s.RotateKeyAsync(
                    request.UserId,
                    request.RotationReason,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.RotateKeyAsync(request, _cancellationTokenSource.Token);

            // Assert
            var serverErrorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            serverErrorResult.StatusCode.Should().Be(500);
            var problem = serverErrorResult.Value.Should().BeOfType<ProblemDetails>().Subject;
            problem.Title.Should().Be("Key Rotation Failed");
            problem.Type.Should().Be("https://api.estatekit.com/errors/internal-server-error");
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}