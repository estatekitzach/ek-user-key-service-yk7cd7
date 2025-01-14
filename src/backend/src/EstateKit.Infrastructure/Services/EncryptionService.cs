using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using EstateKit.Core.Constants;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace EstateKit.Infrastructure.Services
{
    /// <summary>
    /// FIPS 140-2 compliant implementation of IEncryptionService that handles optimized batch 
    /// encryption/decryption operations using AWS KMS with comprehensive error handling and monitoring.
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly IKeyManagementService _keyManagementService;
        private readonly ILogger<EncryptionService> _logger;
        private readonly IAmazonKeyManagementService _kmsClient;
        private readonly AsyncRetryPolicy<string[]> _retryPolicy;
        private readonly SemaphoreSlim _semaphore;
        private const int MAX_CONCURRENT_OPERATIONS = 10;
        private const int MAX_BATCH_SIZE = 100;
        private const int MAX_RETRIES = 3;

        /// <summary>
        /// Initializes a new instance of EncryptionService with required dependencies.
        /// </summary>
        public EncryptionService(
            IKeyManagementService keyManagementService,
            ILogger<EncryptionService> logger,
            IAmazonKeyManagementService kmsClient)
        {
            _keyManagementService = keyManagementService ?? throw new ArgumentNullException(nameof(keyManagementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
            
            // Initialize concurrency control
            _semaphore = new SemaphoreSlim(MAX_CONCURRENT_OPERATIONS);

            // Configure retry policy with exponential backoff
            _retryPolicy = Policy<string[]>
                .Handle<RequestLimitExceededException>()
                .Or<KMSInternalException>()
                .WaitAndRetryAsync(
                    MAX_RETRIES,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} after {Delay}ms for operation {OperationKey}. Error: {Error}",
                            retryCount, timeSpan.TotalMilliseconds, context["OperationKey"], exception.Message);
                    }
                );

            _logger.LogInformation("EncryptionService initialized with FIPS 140-2 compliance");
        }

        /// <inheritdoc/>
        public async Task<string[]> EncryptAsync(long userId, string[] data)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("Invalid user ID", nameof(userId));
            }

            if (data == null || !data.Any())
            {
                throw new ArgumentException("Data array cannot be null or empty", nameof(data));
            }

            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting encryption for user {UserId} with {Count} items", userId, data.Length);

            try
            {
                await _semaphore.WaitAsync();

                // Get user's active public key
                var userKey = await _keyManagementService.GetActiveKeyAsync(userId);
                if (!userKey.IsActive)
                {
                    throw new InvalidOperationException(ErrorCodes.KEY_ROTATION_IN_PROGRESS);
                }

                // Process data in parallel batches
                var results = new ConcurrentBag<(int Index, string EncryptedValue)>();
                var batches = data.Select((value, index) => (Value: value, Index: index))
                    .GroupBy(x => x.Index / MAX_BATCH_SIZE)
                    .Select(g => g.ToList());

                await Task.WhenAll(batches.Select(async batch =>
                {
                    var encryptRequest = new EncryptRequest
                    {
                        KeyId = userKey.Key,
                        EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256
                    };

                    foreach (var item in batch)
                    {
                        if (string.IsNullOrEmpty(item.Value))
                        {
                            throw new ArgumentException($"Data item at index {item.Index} is null or empty");
                        }

                        encryptRequest.Plaintext = new System.IO.MemoryStream(
                            System.Text.Encoding.UTF8.GetBytes(item.Value));

                        var response = await _kmsClient.EncryptAsync(encryptRequest);
                        var encryptedValue = Convert.ToBase64String(response.CiphertextBlob.ToArray());
                        results.Add((item.Index, encryptedValue));
                    }
                }));

                // Reorder results to match input array order
                var orderedResults = results.OrderBy(r => r.Index)
                    .Select(r => r.EncryptedValue)
                    .ToArray();

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Completed encryption for user {UserId} in {Duration}ms",
                    userId, duration.TotalMilliseconds);

                return orderedResults;
            }
            catch (KeyNotFoundException)
            {
                _logger.LogError("Active key not found for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption failed for user {UserId}: {Error}", userId, ex.Message);
                throw new InvalidOperationException(ErrorCodes.KMS_SERVICE_ERROR, ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<string[]> DecryptAsync(long userId, string[] encryptedData)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("Invalid user ID", nameof(userId));
            }

            if (encryptedData == null || !encryptedData.Any())
            {
                throw new ArgumentException("Encrypted data array cannot be null or empty", nameof(encryptedData));
            }

            var startTime = DateTime.UtcNow;
            _logger.LogInformation(
                "Starting decryption for user {UserId} with {Count} items",
                userId, encryptedData.Length);

            try
            {
                await _semaphore.WaitAsync();

                // Get user's active key
                var userKey = await _keyManagementService.GetActiveKeyAsync(userId);
                if (!userKey.IsActive)
                {
                    throw new InvalidOperationException(ErrorCodes.KEY_ROTATION_IN_PROGRESS);
                }

                return await _retryPolicy.ExecuteAsync(async (context) =>
                {
                    var results = new ConcurrentBag<(int Index, string DecryptedValue)>();
                    var batches = encryptedData.Select((value, index) => (Value: value, Index: index))
                        .GroupBy(x => x.Index / MAX_BATCH_SIZE)
                        .Select(g => g.ToList());

                    await Task.WhenAll(batches.Select(async batch =>
                    {
                        var decryptRequest = new DecryptRequest
                        {
                            KeyId = userKey.Key,
                            EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256
                        };

                        foreach (var item in batch)
                        {
                            try
                            {
                                var encryptedBytes = Convert.FromBase64String(item.Value);
                                decryptRequest.CiphertextBlob = new System.IO.MemoryStream(encryptedBytes);

                                var response = await _kmsClient.DecryptAsync(decryptRequest);
                                var decryptedValue = System.Text.Encoding.UTF8.GetString(
                                    response.Plaintext.ToArray());
                                results.Add((item.Index, decryptedValue));
                            }
                            catch (FormatException)
                            {
                                throw new ArgumentException(
                                    $"Invalid Base64 string at index {item.Index}",
                                    nameof(encryptedData));
                            }
                        }
                    }));

                    var orderedResults = results.OrderBy(r => r.Index)
                        .Select(r => r.DecryptedValue)
                        .ToArray();

                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogInformation(
                        "Completed decryption for user {UserId} in {Duration}ms",
                        userId, duration.TotalMilliseconds);

                    return orderedResults;
                }, new Context { ["OperationKey"] = $"Decrypt_{userId}" });
            }
            catch (KeyNotFoundException)
            {
                _logger.LogError("Active key not found for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed for user {UserId}: {Error}", userId, ex.Message);
                throw new InvalidOperationException(ErrorCodes.KMS_SERVICE_ERROR, ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}