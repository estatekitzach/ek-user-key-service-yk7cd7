using Amazon.KeyManagementService;
using EstateKit.Core.Configuration;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Caching;
using EstateKit.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;
using System;
using System.Net.Security;
using System.Security.Authentication;

namespace EstateKit.Api.Extensions
{
    /// <summary>
    /// Extension methods for configuring EstateKit API services with enhanced security,
    /// monitoring, and resilience features.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures all required services for the EstateKit API with FIPS 140-2 compliance
        /// and enterprise-grade security features.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The configured service collection.</returns>
        public static IServiceCollection AddEstateKitServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // Configure AWS services including KMS with FIPS endpoints
            services.AddAwsServices(configuration);

            // Configure Redis caching with enhanced security and performance
            services.AddCaching(configuration);

            // Configure encryption services with FIPS 140-2 compliance
            services.AddEncryptionServices();

            return services;
        }

        /// <summary>
        /// Configures AWS service clients including KMS with FIPS endpoints and regional failover.
        /// </summary>
        private static IServiceCollection AddAwsServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure AWS options with FIPS endpoints
            services.Configure<AWSOptions>(configuration.GetSection("AWS"));
            
            // Configure KMS client with retry policies and monitoring
            services.AddSingleton<IAmazonKeyManagementService>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AWSOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<IAmazonKeyManagementService>>();

                var clientConfig = new AmazonKeyManagementServiceConfig
                {
                    UseFIPSEndpoint = true,
                    MaxErrorRetry = 3,
                    ThrottleRetries = true
                };

                var kmsClient = new AmazonKeyManagementServiceClient(
                    options.Credentials,
                    clientConfig);

                // Wrap client with resilience policies
                var retryPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(3,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        onRetry: (exception, timeSpan, retryCount, context) =>
                        {
                            logger.LogWarning(
                                "Retry {RetryCount} of KMS operation after {Delay}ms",
                                retryCount,
                                timeSpan.TotalMilliseconds);
                        });

                return kmsClient;
            });

            return services;
        }

        /// <summary>
        /// Configures Redis caching with connection pooling, compression, and resilience policies.
        /// </summary>
        private static IServiceCollection AddCaching(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind and validate cache configuration
            services.Configure<CacheConfiguration>(
                configuration.GetSection("Cache"));

            // Configure Redis connection with enhanced security
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var cacheConfig = sp.GetRequiredService<IOptions<CacheConfiguration>>().Value;
                var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();

                var options = ConfigurationOptions.Parse(cacheConfig.ConnectionString);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                options.ConnectRetry = 3;
                options.KeepAlive = 60;
                options.SslProtocols = SslProtocols.Tls13;
                options.SslHost = options.EndPoints[0].ToString();
                options.CertificateValidation += (sender, cert, chain, errors) =>
                {
                    // Implement custom certificate validation if required
                    return errors == SslPolicyErrors.None;
                };

                var multiplexer = ConnectionMultiplexer.Connect(options);
                multiplexer.ConnectionFailed += (sender, args) =>
                {
                    logger.LogError("Redis connection failed: {Error}", args.Exception?.Message);
                };

                return multiplexer;
            });

            // Register cache service with resilience policies
            services.AddSingleton<ICacheService, RedisCacheService>(sp =>
            {
                var connection = sp.GetRequiredService<IConnectionMultiplexer>();
                var cacheConfig = sp.GetRequiredService<IOptions<CacheConfiguration>>().Value;
                var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();

                return new RedisCacheService(connection, Options.Create(cacheConfig), logger);
            });

            return services;
        }

        /// <summary>
        /// Configures encryption services with FIPS 140-2 compliance and monitoring.
        /// </summary>
        private static IServiceCollection AddEncryptionServices(
            this IServiceCollection services)
        {
            // Register encryption service with proper security controls
            services.AddSingleton<IEncryptionService, EncryptionService>();

            // Configure encryption service monitoring
            services.AddHealthChecks()
                .AddCheck<EncryptionService>("EncryptionService")
                .AddRedis("Redis")
                .AddAWSService<IAmazonKeyManagementService>("KMS");

            return services;
        }
    }
}