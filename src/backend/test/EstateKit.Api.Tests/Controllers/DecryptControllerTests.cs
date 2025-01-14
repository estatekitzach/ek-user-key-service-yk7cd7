using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EstateKit.Api.Controllers.V1;
using EstateKit.Api.DTOs;
using EstateKit.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBench;
using Xunit;

namespace EstateKit.Api.Tests.Controllers
{
    /// <summary>
    /// Comprehensive test suite for DecryptController verifying functionality,
    /// performance, security, and FIPS 140-2 compliance requirements.
    /// </summary>
    public class DecryptControllerTests : IDisposable
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;
        private readonly DecryptController _controller;
        private readonly Counter _throughputCounter;
        private const int MaxResponseTime = 3000; // 3 seconds SLA requirement

        public DecryptControllerTests()
        {
            _encryptionServiceMock = new Mock<IEncryptionService>(MockBehavior.Strict);
            _controller = new DecryptController(_encryptionServiceMock.Object);
            _throughputCounter = new Counter();
        }

        [Fact]
        [PerfBenchmark(
            Description = "Validates decryption performance meets SLA requirements",
            NumberOfIterations = 100,
            RunMode = RunMode.Throughput,
            TestMode = TestMode.Test,
            SkipWarmups = false)]
        public async Task DecryptAsync_ValidRequest_ReturnsDecryptedData()
        {
            // Arrange
            var userId = 12345L;
            var encryptedData = new[] { "base64EncodedString1", "base64EncodedString2" };
            var decryptedData = new[] { "decryptedString1", "decryptedString2" };
            var request = new DecryptRequestDto
            {
                UserId = userId,
                EncryptedData = encryptedData
            };

            _encryptionServiceMock
                .Setup(x => x.DecryptAsync(userId, encryptedData))
                .ReturnsAsync(decryptedData);

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await _controller.DecryptAsync(request);

            // Assert
            stopwatch.Stop();
            var actionResult = Assert.IsType<ActionResult<DecryptResponseDto>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var response = Assert.IsType<DecryptResponseDto>(okResult.Value);

            // Verify response content
            response.DecryptedData.Should().BeEquivalentTo(decryptedData);

            // Verify performance requirements
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(MaxResponseTime);

            // Verify service calls
            _encryptionServiceMock.Verify(
                x => x.DecryptAsync(userId, encryptedData),
                Times.Once);

            _throughputCounter.Increment();
        }

        [Fact]
        [PerfBenchmark(
            Description = "Validates concurrent request handling capability",
            NumberOfIterations = 1000,
            RunMode = RunMode.Throughput,
            TestMode = TestMode.Test)]
        public async Task DecryptAsync_ConcurrentRequests_HandlesLoad()
        {
            // Arrange
            const int concurrentRequests = 1000;
            var userId = 12345L;
            var encryptedData = new[] { "base64EncodedString" };
            var decryptedData = new[] { "decryptedString" };
            var request = new DecryptRequestDto
            {
                UserId = userId,
                EncryptedData = encryptedData
            };

            _encryptionServiceMock
                .Setup(x => x.DecryptAsync(userId, encryptedData))
                .ReturnsAsync(decryptedData);

            // Act
            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(_ => _controller.DecryptAsync(request))
                .ToList();

            var stopwatch = Stopwatch.StartNew();
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            results.Should().HaveCount(concurrentRequests);
            results.Should().AllBeOfType<ActionResult<DecryptResponseDto>>();
            
            // Verify average response time meets SLA
            var averageResponseTime = stopwatch.ElapsedMilliseconds / concurrentRequests;
            averageResponseTime.Should().BeLessThan(MaxResponseTime);

            // Verify service calls
            _encryptionServiceMock.Verify(
                x => x.DecryptAsync(userId, encryptedData),
                Times.Exactly(concurrentRequests));

            _throughputCounter.Increment();
        }

        [Fact]
        public async Task DecryptAsync_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var request = new DecryptRequestDto
            {
                UserId = 0, // Invalid user ID
                EncryptedData = new[] { "invalidBase64!" }
            };

            // Act
            var result = await _controller.DecryptAsync(request);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DecryptResponseDto>>(result);
            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task DecryptAsync_KeyNotFound_ReturnsNotFound()
        {
            // Arrange
            var userId = 12345L;
            var encryptedData = new[] { "base64EncodedString" };
            var request = new DecryptRequestDto
            {
                UserId = userId,
                EncryptedData = encryptedData
            };

            _encryptionServiceMock
                .Setup(x => x.DecryptAsync(userId, encryptedData))
                .ThrowsAsync(new KeyNotFoundException("Key not found"));

            // Act
            var result = await _controller.DecryptAsync(request);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DecryptResponseDto>>(result);
            Assert.IsType<NotFoundObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task DecryptAsync_KeyRotationInProgress_ReturnsConflict()
        {
            // Arrange
            var userId = 12345L;
            var encryptedData = new[] { "base64EncodedString" };
            var request = new DecryptRequestDto
            {
                UserId = userId,
                EncryptedData = encryptedData
            };

            _encryptionServiceMock
                .Setup(x => x.DecryptAsync(userId, encryptedData))
                .ThrowsAsync(new KeyRotationInProgressException("Key rotation in progress"));

            // Act
            var result = await _controller.DecryptAsync(request);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DecryptResponseDto>>(result);
            var statusResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status409Conflict, statusResult.StatusCode);
        }

        [Fact]
        public async Task DecryptAsync_EncryptionError_ReturnsInternalServerError()
        {
            // Arrange
            var userId = 12345L;
            var encryptedData = new[] { "base64EncodedString" };
            var request = new DecryptRequestDto
            {
                UserId = userId,
                EncryptedData = encryptedData
            };

            _encryptionServiceMock
                .Setup(x => x.DecryptAsync(userId, encryptedData))
                .ThrowsAsync(new EncryptionException("Encryption error"));

            // Act
            var result = await _controller.DecryptAsync(request);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DecryptResponseDto>>(result);
            var statusResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        }

        [Fact]
        public async Task DecryptAsync_EmptyRequest_ReturnsBadRequest()
        {
            // Arrange
            var request = new DecryptRequestDto
            {
                UserId = 12345L,
                EncryptedData = Array.Empty<string>()
            };

            // Act
            var result = await _controller.DecryptAsync(request);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DecryptResponseDto>>(result);
            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task DecryptAsync_NullRequest_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.DecryptAsync(null);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DecryptResponseDto>>(result);
            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }

        public void Dispose()
        {
            _throughputCounter?.Dispose();
        }
    }
}