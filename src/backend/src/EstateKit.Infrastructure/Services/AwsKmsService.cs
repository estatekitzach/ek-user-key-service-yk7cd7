using System;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace EstateKit.Infrastructure.Services
{
    /// <summary>
    /// AWS KMS implementation of IKeyManagementService with FIPS 140-2 compliance,
    /// enhanced security features, and performance optimizations.
    /// </summary>
    public class AwsKmsService : IKeyManagementService
    {
        private readonly IAmazonKeyManagementService _kmsClient;
        private readonly ILogger<AwsKmsService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IAsyncPolicy<IAmazonKeyManagementService> _retryPolicy;
        private readonly AwsConfiguration _config;

        // Cache key format for user keys
        private const string CACHE_KEY_FORMAT = "user_key_{0}";
        // Cache duration for key entries
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(15);
        // FIPS compliant key specification
        private const string KEY_SPEC = "RSA_2048";

        public AwsKmsService(
            IOptions<AwsConfiguration> config,
            ILogger<AwsKmsService> logger,
            IMemoryCache cache)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            // Initialize KMS client with FIPS endpoint if enabled
            var kmsConfig = new AmazonKeyManagementServiceConfig
            {
                UseFIPSEndpoint = _config.UseFipsEndpoint,
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_config.Region)
            };
            _kmsClient = new AmazonKeyManagementServiceClient(
                _config.AccessKeyId,
                _config.SecretAccessKey,
                kmsConfig);

            // Configure resilience policies
            _retryPolicy = ConfigureResiliencePolicy();
        }

        /// <inheritdoc/>
        public async Task<UserKey> GenerateKeyPairAsync(long userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("UserId must be greater than 0.", nameof(userId));
            }

            try
            {
                _logger.LogInformation("Generating new key pair for user {UserId}", userId);

                // Create KMS key with FIPS compliant parameters
                var createKeyRequest = new CreateKeyRequest
                {
                    KeySpec = KEY_SPEC,
                    KeyUsage = KeyUsageType.ENCRYPT_DECRYPT,
                    Origin = OriginType.AWS_KMS,
                    MultiRegion = true,
                    Description = $"EstateKit encryption key for user {userId}",
                    Tags = new List<Tag>
                    {
                        new Tag { TagKey = "UserId", TagValue = userId.ToString() },
                        new Tag { TagKey = "Application", TagValue = "EstateKit" },
                        new Tag { TagKey = "Environment", TagValue = _config.Environment }
                    }
                };

                var createKeyResponse = await _retryPolicy.ExecuteAsync(async () =>
                    await _kmsClient.CreateKeyAsync(createKeyRequest));

                // Get public key
                var getPublicKeyRequest = new GetPublicKeyRequest
                {
                    KeyId = createKeyResponse.KeyMetadata.KeyId
                };

                var publicKeyResponse = await _retryPolicy.ExecuteAsync(async () =>
                    await _kmsClient.GetPublicKeyAsync(getPublicKeyRequest));

                // Validate FIPS compliance
                if (!await ValidateFipsCompliance(publicKeyResponse.PublicKey.ToArray()))
                {
                    throw new ApplicationException("Generated key does not meet FIPS 140-2 requirements");
                }

                // Create and cache user key
                var userKey = new UserKey(userId, Convert.ToBase64String(publicKeyResponse.PublicKey.ToArray()));
                _cache.Set(string.Format(CACHE_KEY_FORMAT, userId), userKey, CACHE_DURATION);

                _logger.LogInformation("Successfully generated FIPS compliant key pair for user {UserId}", userId);
                return userKey;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Failed to generate key pair for user {UserId}", userId);
                throw new ApplicationException(ErrorCodes.KMS_SERVICE_ERROR, ex);
            }
        }

        /// <inheritdoc/>
        public async Task<UserKey> RotateKeyPairAsync(long userId, string rotationReason)
        {
            if (string.IsNullOrWhiteSpace(rotationReason))
            {
                throw new ArgumentException("Rotation reason must be provided", nameof(rotationReason));
            }

            try
            {
                _logger.LogInformation("Rotating key pair for user {UserId}, reason: {Reason}", userId, rotationReason);

                var currentKey = await GetActiveKeyAsync(userId);
                if (currentKey == null)
                {
                    throw new KeyNotFoundException(ErrorCodes.KEY_NOT_FOUND);
                }

                // Generate new key pair
                var newKey = await GenerateKeyPairAsync(userId);

                // Deactivate old key
                currentKey.Deactivate();

                // Remove from cache
                _cache.Remove(string.Format(CACHE_KEY_FORMAT, userId));

                _logger.LogInformation("Successfully rotated key pair for user {UserId}", userId);
                return newKey;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not KeyNotFoundException)
            {
                _logger.LogError(ex, "Failed to rotate key pair for user {UserId}", userId);
                throw new ApplicationException(ErrorCodes.KMS_SERVICE_ERROR, ex);
            }
        }

        /// <inheritdoc/>
        public async Task<UserKey> GetActiveKeyAsync(long userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("UserId must be greater than 0.", nameof(userId));
            }

            try
            {
                // Check cache first
                var cacheKey = string.Format(CACHE_KEY_FORMAT, userId);
                if (_cache.TryGetValue(cacheKey, out UserKey cachedKey))
                {
                    return cachedKey;
                }

                // If not in cache, generate new key
                return await GenerateKeyPairAsync(userId);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Failed to retrieve active key for user {UserId}", userId);
                throw new ApplicationException(ErrorCodes.KMS_SERVICE_ERROR, ex);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeactivateKeyAsync(long userId)
        {
            try
            {
                var currentKey = await GetActiveKeyAsync(userId);
                if (currentKey == null)
                {
                    throw new KeyNotFoundException(ErrorCodes.KEY_NOT_FOUND);
                }

                currentKey.Deactivate();
                _cache.Remove(string.Format(CACHE_KEY_FORMAT, userId));

                _logger.LogInformation("Successfully deactivated key for user {UserId}", userId);
                return true;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not KeyNotFoundException)
            {
                _logger.LogError(ex, "Failed to deactivate key for user {UserId}", userId);
                throw new ApplicationException(ErrorCodes.KMS_SERVICE_ERROR, ex);
            }
        }

        /// <summary>
        /// Validates that generated keys meet FIPS 140-2 compliance requirements.
        /// </summary>
        private async Task<bool> ValidateFipsCompliance(byte[] keyMaterial)
        {
            try
            {
                // Verify key length
                if (keyMaterial.Length < 256) // 2048 bits = 256 bytes
                {
                    return false;
                }

                // Verify FIPS mode is enabled
                var describeCustomKeyStoresRequest = new DescribeCustomKeyStoresRequest();
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _kmsClient.DescribeCustomKeyStoresAsync(describeCustomKeyStoresRequest));

                // Check if using FIPS validated HSM
                return response.CustomKeyStores.Any(store => 
                    store.CustomKeyStoreType == CustomKeyStoreType.AWS_CLOUDHSM &&
                    store.ConnectionState == ConnectionStateType.CONNECTED);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate FIPS compliance");
                return false;
            }
        }

        /// <summary>
        /// Configures resilience policies for AWS KMS operations.
        /// </summary>
        private IAsyncPolicy<IAmazonKeyManagementService> ConfigureResiliencePolicy()
        {
            // Retry policy with exponential backoff
            var retryPolicy = Policy<IAmazonKeyManagementService>
                .Handle<RequestLimitExceededException>()
                .Or<KMSInternalException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            // Circuit breaker policy
            var circuitBreakerPolicy = Policy<IAmazonKeyManagementService>
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));

            return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }
    }

    /// <summary>
    /// Configuration options for AWS KMS service.
    /// </summary>
    public class AwsConfiguration
    {
        public string AccessKeyId { get; set; }
        public string SecretAccessKey { get; set; }
        public string Region { get; set; }
        public string Environment { get; set; }
        public bool UseFipsEndpoint { get; set; }
    }
}