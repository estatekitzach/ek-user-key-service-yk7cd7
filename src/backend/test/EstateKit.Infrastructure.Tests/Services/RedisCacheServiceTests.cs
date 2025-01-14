using EstateKit.Core.Configuration;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace EstateKit.Infrastructure.Tests.Services
{
    public class RedisCacheServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _connectionMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<ILogger<RedisCacheService>> _loggerMock;
        private readonly CacheConfiguration _configuration;
        private readonly RedisCacheService _cacheService;

        public RedisCacheServiceTests()
        {
            // Initialize mocks
            _connectionMock = new Mock<IConnectionMultiplexer>();
            _databaseMock = new Mock<IDatabase>();
            _loggerMock = new Mock<ILogger<RedisCacheService>>();

            // Setup default configuration
            _configuration = new CacheConfiguration
            {
                DefaultTTLMinutes = 15,
                EnableCompression = true,
                CompressionThresholdBytes = 1024,
                InstanceName = "test",
                DatabaseId = 0,
                RetryCount = 3,
                RetryDelayMilliseconds = 100,
                ConnectionString = "localhost:6379"
            };

            // Setup database mock
            _connectionMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_databaseMock.Object);

            // Create service instance
            _cacheService = new RedisCacheService(
                _connectionMock.Object,
                Options.Create(_configuration),
                _loggerMock.Object
            );
        }

        [Fact]
        public void Constructor_ValidatesParameters()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RedisCacheService(
                null,
                Options.Create(_configuration),
                _loggerMock.Object
            ));

            Assert.Throws<ArgumentNullException>(() => new RedisCacheService(
                _connectionMock.Object,
                null,
                _loggerMock.Object
            ));

            Assert.Throws<ArgumentNullException>(() => new RedisCacheService(
                _connectionMock.Object,
                Options.Create(_configuration),
                null
            ));
        }

        [Fact]
        public async Task SetAsync_ValidInput_StoresCompressedValue()
        {
            // Arrange
            var key = "test-key";
            var value = new string('x', 2048); // Large enough to trigger compression
            var expectedCacheKey = $"{_configuration.InstanceName}:{key}";

            _databaseMock.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()
            )).ReturnsAsync(true);

            // Act
            var result = await _cacheService.SetAsync(key, value);

            // Assert
            Assert.True(result);
            _databaseMock.Verify(x => x.StringSetAsync(
                expectedCacheKey,
                It.Is<RedisValue>(v => v.ToString().StartsWith("compressed:")),
                TimeSpan.FromMinutes(_configuration.DefaultTTLMinutes),
                When.Always,
                CommandFlags.None
            ), Times.Once);
        }

        [Fact]
        public async Task GetAsync_CacheMiss_HandlesGracefully()
        {
            // Arrange
            var key = "missing-key";
            var expectedCacheKey = $"{_configuration.InstanceName}:{key}";

            _databaseMock.Setup(x => x.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()
            )).ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _cacheService.GetAsync<string>(key);

            // Assert
            Assert.Null(result);
            _databaseMock.Verify(x => x.StringGetAsync(
                expectedCacheKey,
                CommandFlags.None
            ), Times.Once);
            VerifyLogMessage("Cache miss for key: missing-key");
        }

        [Fact]
        public async Task RemoveAsync_ExistingKey_RemovesSuccessfully()
        {
            // Arrange
            var key = "existing-key";
            var expectedCacheKey = $"{_configuration.InstanceName}:{key}";

            _databaseMock.Setup(x => x.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()
            )).ReturnsAsync(true);

            // Act
            var result = await _cacheService.RemoveAsync(key);

            // Assert
            Assert.True(result);
            _databaseMock.Verify(x => x.KeyDeleteAsync(
                expectedCacheKey,
                CommandFlags.None
            ), Times.Once);
            VerifyLogMessage($"Cache remove for key: {key}, Success: True");
        }

        [Fact]
        public async Task ExistsAsync_ConnectionFailure_HandlesError()
        {
            // Arrange
            var key = "test-key";
            var expectedCacheKey = $"{_configuration.InstanceName}:{key}";

            _databaseMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()
            )).ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test error"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(async () =>
                await _cacheService.ExistsAsync(key));

            VerifyLogError("Error checking existence for key: test-key");
        }

        [Fact]
        public async Task GetAsync_CompressedValue_DecompressesCorrectly()
        {
            // Arrange
            var key = "compressed-key";
            var value = new TestData { Id = 1, Name = "Test" };
            var expectedCacheKey = $"{_configuration.InstanceName}:{key}";
            var compressedData = "compressed:" + Convert.ToBase64String(
                JsonSerializer.SerializeToUtf8Bytes(value));

            _databaseMock.Setup(x => x.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()
            )).ReturnsAsync(compressedData);

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(value.Id, result.Id);
            Assert.Equal(value.Name, result.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task SetAsync_InvalidKey_ThrowsArgumentNullException(string key)
        {
            // Arrange
            var value = "test";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _cacheService.SetAsync(key, value));
        }

        private void VerifyLogMessage(string message)
        {
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        private void VerifyLogError(string message)
        {
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}