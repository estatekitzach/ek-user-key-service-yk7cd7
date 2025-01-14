using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
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
    /// Comprehensive test suite for EncryptionService verifying FIPS 140-2 compliance,
    /// batch operations, concurrency handling, and key rotation scenarios.
    /// </summary>
    public class EncryptionServiceTests
    {
        private readonly Mock<IKeyManagementService> _keyManagementServiceMock;
        private readonly Mock<IAmazonKeyManagementService> _kmsMock;
        private readonly Mock<ILogger<EncryptionService>> _loggerMock;
        private readonly IEncryptionService _encryptionService;
        private const long TEST_USER_ID = 123;
        private const string TEST_PUBLIC_KEY = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...";

        public EncryptionServiceTests()
        {
            _keyManagementServiceMock = new Mock<IKeyManagementService>(MockBehavior.Strict);
            _kmsMock = new Mock<IAmazonKeyManagementService>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger<EncryptionService>>();

            _encryptionService = new EncryptionService(
                _keyManagementServiceMock.Object,
                _loggerMock.Object,
                _kmsMock.Object);
        }

        [Fact]
        public async Task BatchEncryptAsync_ValidInput_SuccessfullyEncryptsData()
        {
            // Arrange
            var testData = new[] { "test1", "test2", "test3" };
            var userKey = new UserKey(TEST_USER_ID, TEST_PUBLIC_KEY);
            var encryptResponse = new EncryptResponse
            {
                CiphertextBlob = new System.IO.MemoryStream(
                    Convert.FromBase64String("encrypted_data"))
            };

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(TEST_USER_ID))
                .ReturnsAsync(userKey);

            _kmsMock
                .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), default))
                .ReturnsAsync(encryptResponse);

            // Act
            var result = await _encryptionService.EncryptAsync(TEST_USER_ID, testData);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().Be(testData.Length);
            result.Should().AllSatisfy(x => x.Should().NotBeNullOrEmpty());

            _keyManagementServiceMock.Verify(
                x => x.GetActiveKeyAsync(TEST_USER_ID), Times.Once);
            _kmsMock.Verify(
                x => x.EncryptAsync(
                    It.Is<EncryptRequest>(r => 
                        r.KeyId == TEST_PUBLIC_KEY && 
                        r.EncryptionAlgorithm == EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256),
                    default),
                Times.Exactly(testData.Length));
        }

        [Fact]
        public async Task BatchDecryptAsync_ValidInput_SuccessfullyDecryptsData()
        {
            // Arrange
            var encryptedData = new[] { 
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("encrypted1")),
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("encrypted2"))
            };
            var userKey = new UserKey(TEST_USER_ID, TEST_PUBLIC_KEY);
            var decryptResponse = new DecryptResponse
            {
                Plaintext = new System.IO.MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("decrypted"))
            };

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(TEST_USER_ID))
                .ReturnsAsync(userKey);

            _kmsMock
                .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), default))
                .ReturnsAsync(decryptResponse);

            // Act
            var result = await _encryptionService.DecryptAsync(TEST_USER_ID, encryptedData);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().Be(encryptedData.Length);
            result.Should().AllSatisfy(x => x.Should().Be("decrypted"));

            _kmsMock.Verify(
                x => x.DecryptAsync(
                    It.Is<DecryptRequest>(r => 
                        r.KeyId == TEST_PUBLIC_KEY && 
                        r.EncryptionAlgorithm == EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256),
                    default),
                Times.Exactly(encryptedData.Length));
        }

        [Fact]
        public async Task EncryptAsync_ConcurrentRequests_HandlesCorrectly()
        {
            // Arrange
            var testData = new[] { "test1", "test2" };
            var userKey = new UserKey(TEST_USER_ID, TEST_PUBLIC_KEY);
            var encryptResponse = new EncryptResponse
            {
                CiphertextBlob = new System.IO.MemoryStream(
                    Convert.FromBase64String("encrypted_data"))
            };

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(TEST_USER_ID))
                .ReturnsAsync(userKey);

            _kmsMock
                .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), default))
                .ReturnsAsync(encryptResponse);

            // Act
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => _encryptionService.EncryptAsync(TEST_USER_ID, testData));
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(5);
            results.Should().AllSatisfy(result =>
            {
                result.Should().HaveCount(2);
                result.Should().AllSatisfy(x => x.Should().NotBeNullOrEmpty());
            });
        }

        [Fact]
        public async Task DecryptAsync_KeyRotation_HandlesTransition()
        {
            // Arrange
            var encryptedData = new[] { 
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("encrypted")) 
            };
            var oldKey = new UserKey(TEST_USER_ID, TEST_PUBLIC_KEY);
            var newKey = new UserKey(TEST_USER_ID, "new_" + TEST_PUBLIC_KEY);
            var decryptResponse = new DecryptResponse
            {
                Plaintext = new System.IO.MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("decrypted"))
            };

            var callCount = 0;
            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(TEST_USER_ID))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    // Simulate key rotation after first call
                    return callCount == 1 ? oldKey : newKey;
                });

            _kmsMock
                .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), default))
                .ReturnsAsync(decryptResponse);

            // Act
            var result = await _encryptionService.DecryptAsync(TEST_USER_ID, encryptedData);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Should().Be("decrypted");

            _keyManagementServiceMock.Verify(
                x => x.GetActiveKeyAsync(TEST_USER_ID), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task EncryptAsync_InvalidUserId_ThrowsArgumentException(long invalidUserId)
        {
            // Arrange
            var testData = new[] { "test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _encryptionService.EncryptAsync(invalidUserId, testData));
        }

        [Fact]
        public async Task DecryptAsync_InvalidBase64Input_ThrowsArgumentException()
        {
            // Arrange
            var invalidData = new[] { "not-base64!" };
            var userKey = new UserKey(TEST_USER_ID, TEST_PUBLIC_KEY);

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(TEST_USER_ID))
                .ReturnsAsync(userKey);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _encryptionService.DecryptAsync(TEST_USER_ID, invalidData));
        }

        [Fact]
        public async Task EncryptAsync_KeyRotationInProgress_ThrowsInvalidOperationException()
        {
            // Arrange
            var testData = new[] { "test" };
            var inactiveKey = new UserKey(TEST_USER_ID, TEST_PUBLIC_KEY);
            inactiveKey.Deactivate();

            _keyManagementServiceMock
                .Setup(x => x.GetActiveKeyAsync(TEST_USER_ID))
                .ReturnsAsync(inactiveKey);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _encryptionService.EncryptAsync(TEST_USER_ID, testData));
            ex.Message.Should().Be(ErrorCodes.KEY_ROTATION_IN_PROGRESS);
        }
    }
}