using System;
using System.Threading;
using System.Threading.Tasks;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EstateKit.Core.Tests.Services
{
    /// <summary>
    /// Comprehensive test suite for KeyRotationService covering all rotation scenarios,
    /// error cases, and security validations.
    /// </summary>
    public class KeyRotationServiceTests : IDisposable
    {
        private readonly Mock<IKeyManagementService> _keyManagementServiceMock;
        private readonly Mock<IUserKeyRepository> _userKeyRepositoryMock;
        private readonly Mock<ILogger<KeyRotationService>> _loggerMock;
        private readonly KeyRotationService _sut;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public KeyRotationServiceTests()
        {
            _keyManagementServiceMock = new Mock<IKeyManagementService>();
            _userKeyRepositoryMock = new Mock<IUserKeyRepository>();
            _loggerMock = new Mock<ILogger<KeyRotationService>>();
            _cancellationTokenSource = new CancellationTokenSource();

            _sut = new KeyRotationService(
                _keyManagementServiceMock.Object,
                _userKeyRepositoryMock.Object,
                _loggerMock.Object);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
        }

        [Fact]
        public async Task RotateKeyAsync_ValidRequest_SuccessfulRotation()
        {
            // Arrange
            const long userId = 123;
            const string rotationReason = "Regular 90-day rotation";
            var currentKey = new UserKey(userId, "current-key-value");
            var newKey = new UserKey(userId, "new-key-value");

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(userId))
                .ReturnsAsync(currentKey);

            _keyManagementServiceMock
                .Setup(x => x.RotateKeyPairAsync(userId, rotationReason))
                .ReturnsAsync(newKey);

            _userKeyRepositoryMock
                .Setup(x => x.UpdateKeyAsync(newKey))
                .ReturnsAsync(newKey);

            // Act
            var result = await _sut.RotateKeyAsync(
                userId,
                rotationReason,
                _cancellationTokenSource.Token);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(userId);
            result.Key.Should().Be("new-key-value");
            result.IsActive.Should().BeTrue();

            _keyManagementServiceMock.Verify(
                x => x.GetActiveKeyAsync(userId),
                Times.Once);

            _keyManagementServiceMock.Verify(
                x => x.RotateKeyPairAsync(userId, rotationReason),
                Times.Once);

            _userKeyRepositoryMock.Verify(
                x => x.AddKeyHistoryAsync(It.Is<UserKeyHistory>(h =>
                    h.UserId == userId &&
                    h.KeyValue == "current-key-value" &&
                    h.RotationReason == rotationReason)),
                Times.Once);

            _userKeyRepositoryMock.Verify(
                x => x.UpdateKeyAsync(newKey),
                Times.Once);
        }

        [Fact]
        public async Task RotateKeyAsync_InvalidUserId_ThrowsArgumentException()
        {
            // Arrange
            const long invalidUserId = 0;
            const string rotationReason = "Regular rotation";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.RotateKeyAsync(
                    invalidUserId,
                    rotationReason,
                    _cancellationTokenSource.Token));
        }

        [Fact]
        public async Task RotateKeyAsync_EmptyRotationReason_ThrowsArgumentException()
        {
            // Arrange
            const long userId = 123;
            const string emptyReason = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.RotateKeyAsync(
                    userId,
                    emptyReason,
                    _cancellationTokenSource.Token));
        }

        [Fact]
        public async Task RotateKeyAsync_NoActiveKey_ThrowsKeyNotFoundException()
        {
            // Arrange
            const long userId = 123;
            const string rotationReason = "Regular rotation";

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(userId))
                .ReturnsAsync((UserKey)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _sut.RotateKeyAsync(
                    userId,
                    rotationReason,
                    _cancellationTokenSource.Token));
        }

        [Fact]
        public async Task EmergencyRotateKeyAsync_SecurityIncident_SuccessfulRotation()
        {
            // Arrange
            const long userId = 123;
            const string incidentId = "SEC-2024-001";
            var currentKey = new UserKey(userId, "current-key-value");
            var newKey = new UserKey(userId, "emergency-key-value");

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(userId))
                .ReturnsAsync(currentKey);

            _keyManagementServiceMock
                .Setup(x => x.RotateKeyPairAsync(userId, It.IsAny<string>()))
                .ReturnsAsync(newKey);

            _userKeyRepositoryMock
                .Setup(x => x.UpdateKeyAsync(newKey))
                .ReturnsAsync(newKey);

            // Act
            var result = await _sut.EmergencyRotateKeyAsync(
                userId,
                incidentId,
                _cancellationTokenSource.Token);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(userId);
            result.Key.Should().Be("emergency-key-value");
            result.IsActive.Should().BeTrue();

            _userKeyRepositoryMock.Verify(
                x => x.AddKeyHistoryAsync(It.Is<UserKeyHistory>(h =>
                    h.UserId == userId &&
                    h.KeyValue == "current-key-value" &&
                    h.RotationReason.Contains(incidentId) &&
                    h.CreatedBy == "SYSTEM-EMERGENCY")),
                Times.Once);
        }

        [Fact]
        public async Task ScheduleRotationAsync_ValidFutureDate_SuccessfulScheduling()
        {
            // Arrange
            const long userId = 123;
            var futureDate = DateTime.UtcNow.AddDays(1);
            const string rotationReason = "Compliance rotation";
            var currentKey = new UserKey(userId, "current-key-value");

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(userId))
                .ReturnsAsync(currentKey);

            // Act
            var result = await _sut.ScheduleRotationAsync(
                userId,
                futureDate,
                rotationReason,
                _cancellationTokenSource.Token);

            // Assert
            result.Should().BeTrue();
            _keyManagementServiceMock.Verify(
                x => x.GetActiveKeyAsync(userId),
                Times.Once);
        }

        [Fact]
        public async Task ScheduleRotationAsync_PastDate_ThrowsArgumentException()
        {
            // Arrange
            const long userId = 123;
            var pastDate = DateTime.UtcNow.AddDays(-1);
            const string rotationReason = "Compliance rotation";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.ScheduleRotationAsync(
                    userId,
                    pastDate,
                    rotationReason,
                    _cancellationTokenSource.Token));
        }

        [Fact]
        public async Task EmergencyRotateKeyAsync_InvalidIncidentId_ThrowsArgumentException()
        {
            // Arrange
            const long userId = 123;
            const string emptyIncidentId = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.EmergencyRotateKeyAsync(
                    userId,
                    emptyIncidentId,
                    _cancellationTokenSource.Token));
        }

        [Fact]
        public async Task EmergencyRotateKeyAsync_KmsServiceError_ThrowsInvalidOperationException()
        {
            // Arrange
            const long userId = 123;
            const string incidentId = "SEC-2024-001";
            var currentKey = new UserKey(userId, "current-key-value");

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(userId))
                .ReturnsAsync(currentKey);

            _keyManagementServiceMock
                .Setup(x => x.RotateKeyPairAsync(userId, It.IsAny<string>()))
                .ThrowsAsync(new ApplicationException(ErrorCodes.KMS_SERVICE_ERROR));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.EmergencyRotateKeyAsync(
                    userId,
                    incidentId,
                    _cancellationTokenSource.Token));

            exception.Message.Should().Be(ErrorCodes.KEY_ROTATION_IN_PROGRESS);
        }
    }
}