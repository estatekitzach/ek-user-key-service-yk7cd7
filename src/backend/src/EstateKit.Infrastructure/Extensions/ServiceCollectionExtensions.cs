using Amazon.KeyManagementService;
using Amazon.Runtime;
using EstateKit.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StackExchange.Redis;
using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;

namespace EstateKit.Infrastructure.Extensions
{
    /// <summary>
    /// Provides extension methods for configuring EstateKit infrastructure services with enhanced security,
    /// FIPS 140-2 compliance, and resilient connections to AWS services and Redis cache.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures all EstateKit infrastructure services with FIPS 140-2 compliance and enhanced security.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Application configuration containing service settings.</param>
        /// <returns>The configured service collection.</returns>
        public static IServiceCollection AddEstateKitInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Enable FIPS 140-2 compliance mode
            CryptoConfig.AllowOnlyFipsAlgorithms = true;

            // Configure AWS services with enhanced security
            services.AddAwsServices(configuration);

            // Configure Redis caching with connection resilience
            services.AddCaching(configuration);

            // Register infrastructure services
            services.AddScoped<IEncryptionService, EncryptionService>();
            services.AddScoped<IKeyManagementService, KeyManagementService>();
            services.AddScoped<ICacheService, RedisCacheService>();

            return services;
        }

        /// <summary>
        /// Configures AWS services with FIPS 140-2 compliance and enhanced security policies.
        /// </summary>
        private static IServiceCollection AddAwsServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var awsSection = configuration.GetSection("AWS");
            var region = awsSection["Region"];
            var useFips = awsSection.GetValue<bool>("UseFips", true);

            // Configure AWS KMS client with retry policies
            var retryPolicy = Policy<IAmazonKeyManagementService>
                .Handle<AmazonServiceException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var circuitBreakerPolicy = Policy<IAmazonKeyManagementService>
                .Handle<AmazonServiceException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));

            services.AddSingleton<IAmazonKeyManagementService>(sp =>
            {
                var config = new AmazonKeyManagementServiceConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
                    UseFIPSEndpoint = useFips,
                    MaxErrorRetry = 3,
                    Timeout = TimeSpan.FromSeconds(10)
                };

                return new AmazonKeyManagementServiceClient(config);
            });

            return services;
        }

        /// <summary>
        /// Configures Redis caching with enhanced connection resilience and security.
        /// </summary>
        private static IServiceCollection AddCaching(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var redisSection = configuration.GetSection("Redis");
            var connectionString = redisSection["ConnectionString"];

            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);
            configOptions.KeepAlive = 60;
            configOptions.ConnectTimeout = 5000;
            configOptions.SyncTimeout = 5000;
            configOptions.Ssl = true;
            configOptions.SslProtocols = SslProtocols.Tls13;
            configOptions.CertificateValidation += (sender, certificate, chain, errors) =>
            {
                // Implement custom certificate validation if needed
                return errors == SslPolicyErrors.None;
            };

            // Configure connection multiplexer with resilience
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var retryPolicy = Policy
                    .Handle<RedisConnectionException>()
                    .WaitAndRetry(3, retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

                return retryPolicy.Execute(() =>
                    ConnectionMultiplexer.Connect(configOptions));
            });

            // Configure cache options
            services.Configure<RedisCacheOptions>(options =>
            {
                options.DefaultTTL = TimeSpan.FromMinutes(15);
                options.EnableCompression = true;
                options.MonitoringEnabled = true;
                options.HealthCheckInterval = TimeSpan.FromSeconds(30);
            });

            return services;
        }
    }

    /// <summary>
    /// Configuration options for Redis caching service.
    /// </summary>
    public class RedisCacheOptions
    {
        public TimeSpan DefaultTTL { get; set; }
        public bool EnableCompression { get; set; }
        public bool MonitoringEnabled { get; set; }
        public TimeSpan HealthCheckInterval { get; set; }
    }
}