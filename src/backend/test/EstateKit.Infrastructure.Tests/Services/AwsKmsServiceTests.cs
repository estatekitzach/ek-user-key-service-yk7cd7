using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;
using EstateKit.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EstateKit.Infrastructure.Tests.Services
{
    public class AwsKmsServiceTests : IDisposable
    {
        private readonly Mock<IAmazonKeyManagementService> _mockKmsClient;
        private readonly Mock<ILogger<AwsKmsService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly IOptions<AwsConfiguration> _config;
        private readonly AwsKmsService _service;

        public AwsKmsServiceTests()
        {
            _mockKmsClient = new Mock<IAmazonKeyManagementService>();
            _mockLogger = new Mock<ILogger<AwsKmsService>>();
            _mockCache = new Mock<IMemoryCache>();
            
            // Configure FIPS-compliant test settings
            _config = Options.Create(new AwsConfiguration
            {
                AccessKeyId = "test-access-key",
                SecretAccessKey = "test-secret-key",
                Region = "us-east-1",
                Environment = "test",
                UseFipsEndpoint = true
            });

            _service = new AwsKmsService(_config, _mockLogger.Object, _mockCache.Object);
        }

        [Fact]
        public async Task GenerateKeyPair_ValidUserId_ReturnsUserKey()
        {
            // Arrange
            const long userId = 123;
            var keyId = "test-key-id";
            var publicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', 256))); // 2048-bit key

            _mockKmsClient.Setup(x => x.CreateKeyAsync(
                It.IsAny<CreateKeyRequest>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new CreateKeyResponse
            {
                KeyMetadata = new KeyMetadata { KeyId = keyId }
            });

            _mockKmsClient.Setup(x => x.GetPublicKeyAsync(
                It.Is<GetPublicKeyRequest>(r => r.KeyId == keyId),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new GetPublicKeyResponse
            {
                PublicKey = new MemoryStream(Convert.FromBase64String(publicKey))
            });

            _mockKmsClient.Setup(x => x.DescribeCustomKeyStoresAsync(
                It.IsAny<DescribeCustomKeyStoresRequest>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new DescribeCustomKeyStoresResponse
            {
                CustomKeyStores = new List<CustomKeyStoresEntry>
                {
                    new CustomKeyStoresEntry
                    {
                        CustomKeyStoreType = CustomKeyStoreType.AWS_CLOUDHSM,
                        ConnectionState = ConnectionStateType.CONNECTED
                    }
                }
            });

            // Act
            var result = await _service.GenerateKeyPairAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.True(result.IsActive);
            Assert.True(UserKey.ValidateKey(result.Key));
            Assert.True(DateTime.UtcNow.Subtract(result.CreatedAt).TotalSeconds < 5);

            _mockKmsClient.Verify(x => x.CreateKeyAsync(
                It.Is<CreateKeyRequest>(r => 
                    r.KeySpec == "RSA_2048" && 
                    r.KeyUsage == KeyUsageType.ENCRYPT_DECRYPT &&
                    r.MultiRegion == true),
                It.IsAny<CancellationToken>()
            ), Times.Once);

            _mockLogger.Verify(x => x.LogInformation(
                It.Is<string>(s => s.Contains("Successfully generated FIPS compliant key pair")),
                It.Is<long>(l => l == userId)
            ), Times.Once);
        }

        [Fact]
        public async Task RotateKeyPair_ValidInput_ReturnsUpdatedUserKey()
        {
            // Arrange
            const long userId = 123;
            const string rotationReason = "scheduled";
            var oldKey = new UserKey(userId, Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', 256))));
            var newKeyId = "new-key-id";
            var newPublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('y', 256)));

            _mockCache.Setup(x => x.TryGetValue(It.IsAny<string>(), out oldKey))
                .Returns(true);

            _mockKmsClient.Setup(x => x.CreateKeyAsync(
                It.IsAny<CreateKeyRequest>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new CreateKeyResponse
            {
                KeyMetadata = new KeyMetadata { KeyId = newKeyId }
            });

            _mockKmsClient.Setup(x => x.GetPublicKeyAsync(
                It.Is<GetPublicKeyRequest>(r => r.KeyId == newKeyId),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new GetPublicKeyResponse
            {
                PublicKey = new MemoryStream(Convert.FromBase64String(newPublicKey))
            });

            // Act
            var result = await _service.RotateKeyPairAsync(userId, rotationReason);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.True(result.IsActive);
            Assert.NotEqual(oldKey.Key, result.Key);
            Assert.True(DateTime.UtcNow.Subtract(result.CreatedAt).TotalSeconds < 5);

            _mockLogger.Verify(x => x.LogInformation(
                It.Is<string>(s => s.Contains("Successfully rotated key pair")),
                It.Is<long>(l => l == userId)
            ), Times.Once);

            _mockCache.Verify(x => x.Remove(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task GetActiveKey_ExistingKey_ReturnsUserKey()
        {
            // Arrange
            const long userId = 123;
            var existingKey = new UserKey(userId, Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', 256))));

            _mockCache.Setup(x => x.TryGetValue(It.IsAny<string>(), out existingKey))
                .Returns(true);

            // Act
            var result = await _service.GetActiveKeyAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.True(result.IsActive);
            Assert.Equal(existingKey.Key, result.Key);

            _mockCache.Verify(x => x.TryGetValue(
                It.Is<string>(s => s.Contains(userId.ToString())),
                out It.Ref<UserKey>.IsAny
            ), Times.Once);
        }

        [Fact]
        public async Task DeactivateKey_ValidUserId_ReturnsSuccess()
        {
            // Arrange
            const long userId = 123;
            var activeKey = new UserKey(userId, Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', 256))));

            _mockCache.Setup(x => x.TryGetValue(It.IsAny<string>(), out activeKey))
                .Returns(true);

            // Act
            var result = await _service.DeactivateKeyAsync(userId);

            // Assert
            Assert.True(result);
            _mockCache.Verify(x => x.Remove(It.IsAny<string>()), Times.Once);
            _mockLogger.Verify(x => x.LogInformation(
                It.Is<string>(s => s.Contains("Successfully deactivated key")),
                It.Is<long>(l => l == userId)
            ), Times.Once);
        }

        [Fact]
        public async Task GenerateKeyPair_InvalidUserId_ThrowsArgumentException()
        {
            // Arrange
            const long invalidUserId = 0;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.GenerateKeyPairAsync(invalidUserId));
        }

        [Fact]
        public async Task RotateKeyPair_EmptyReason_ThrowsArgumentException()
        {
            // Arrange
            const long userId = 123;
            const string emptyReason = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.RotateKeyPairAsync(userId, emptyReason));
        }

        [Fact]
        public async Task GetActiveKey_KmsServiceError_ThrowsApplicationException()
        {
            // Arrange
            const long userId = 123;
            _mockKmsClient.Setup(x => x.CreateKeyAsync(
                It.IsAny<CreateKeyRequest>(),
                It.IsAny<CancellationToken>()
            )).ThrowsAsync(new KMSInternalException("KMS error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ApplicationException>(() => 
                _service.GetActiveKeyAsync(userId));
            Assert.Equal(ErrorCodes.KMS_SERVICE_ERROR, exception.Message);
        }

        public void Dispose()
        {
            _mockKmsClient?.Dispose();
        }
    }
}