using EstateKit.Core.Configuration;
using EstateKit.Core.Interfaces;
using Microsoft.Extensions.Logging; // v9.0.0
using Microsoft.Extensions.Options; // v9.0.0
using Polly; // v8.0.0
using StackExchange.Redis; // v2.7.10
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json; // v9.0.0
using System.Threading.Tasks;

namespace EstateKit.Infrastructure.Caching
{
    /// <summary>
    /// Enhanced Redis cache service implementation with compression support, resilient operations,
    /// and comprehensive error handling for the EstateKit Personal Information API.
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly IDatabase _database;
        private readonly CacheConfiguration _configuration;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly IAsyncPolicy _retryPolicy;
        private const string COMPRESSION_FLAG = "compressed:";
        private const int BUFFER_SIZE = 81920; // 80KB buffer for optimal compression

        public RedisCacheService(
            IConnectionMultiplexer connection,
            IOptions<CacheConfiguration> configuration,
            ILogger<RedisCacheService> logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _database = _connection.GetDatabase(_configuration.DatabaseId);

            // Configure retry policy with exponential backoff
            _retryPolicy = Policy
                .Handle<RedisConnectionException>()
                .Or<RedisTimeoutException>()
                .WaitAndRetryAsync(
                    _configuration.RetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(_configuration.RetryDelayMilliseconds * Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Redis operation retry {RetryCount} of {MaxRetries} after {Delay}ms",
                            retryCount,
                            _configuration.RetryCount,
                            timeSpan.TotalMilliseconds);
                    }
                );
        }

        /// <inheritdoc/>
        public async Task<T?> GetAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var cacheKey = $"{_configuration.InstanceName}:{key}";
                    var value = await _database.StringGetAsync(cacheKey);

                    if (!value.HasValue)
                    {
                        _logger.LogDebug("Cache miss for key: {Key}", key);
                        return default;
                    }

                    var stringValue = value.ToString();
                    byte[] dataBytes;

                    if (stringValue.StartsWith(COMPRESSION_FLAG))
                    {
                        // Handle compressed data
                        var compressedData = Convert.FromBase64String(stringValue.Substring(COMPRESSION_FLAG.Length));
                        dataBytes = DecompressData(compressedData);
                    }
                    else
                    {
                        dataBytes = Convert.FromBase64String(stringValue);
                    }

                    var result = JsonSerializer.Deserialize<T>(dataBytes);
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return result;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving value for key: {Key}", key);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var cacheKey = $"{_configuration.InstanceName}:{key}";
                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(value);
                    string serializedValue;

                    if (_configuration.EnableCompression && jsonBytes.Length > _configuration.CompressionThresholdBytes)
                    {
                        var compressedData = CompressData(jsonBytes);
                        serializedValue = $"{COMPRESSION_FLAG}{Convert.ToBase64String(compressedData)}";
                    }
                    else
                    {
                        serializedValue = Convert.ToBase64String(jsonBytes);
                    }

                    var expirationTime = expiration ?? TimeSpan.FromMinutes(_configuration.DefaultTTLMinutes);
                    var result = await _database.StringSetAsync(cacheKey, serializedValue, expirationTime);

                    _logger.LogDebug(
                        "Cache set for key: {Key}, Expiration: {Expiration}, Compressed: {Compressed}",
                        key,
                        expirationTime,
                        serializedValue.StartsWith(COMPRESSION_FLAG));

                    return result;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for key: {Key}", key);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var cacheKey = $"{_configuration.InstanceName}:{key}";
                    var result = await _database.KeyDeleteAsync(cacheKey);

                    _logger.LogDebug("Cache remove for key: {Key}, Success: {Success}", key, result);
                    return result;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key: {Key}", key);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var cacheKey = $"{_configuration.InstanceName}:{key}";
                    var exists = await _database.KeyExistsAsync(cacheKey);

                    _logger.LogDebug("Cache exists check for key: {Key}, Exists: {Exists}", key, exists);
                    return exists;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence for key: {Key}", key);
                throw;
            }
        }

        private byte[] CompressData(byte[] data)
        {
            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            using (var dataStream = new MemoryStream(data))
            {
                dataStream.CopyTo(gzipStream, BUFFER_SIZE);
            }
            return compressedStream.ToArray();
        }

        private byte[] DecompressData(byte[] compressedData)
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var decompressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(decompressedStream, BUFFER_SIZE);
            }
            return decompressedStream.ToArray();
        }
    }
}