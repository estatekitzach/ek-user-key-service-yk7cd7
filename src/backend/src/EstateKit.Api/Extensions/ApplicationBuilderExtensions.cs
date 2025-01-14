using EstateKit.Api.Middleware;
using EstateKit.Api.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.RateLimiting;

namespace EstateKit.Api.Extensions
{
    /// <summary>
    /// Extension methods for IApplicationBuilder to configure the EstateKit Personal Information API
    /// middleware pipeline with enhanced security, monitoring, and documentation features.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Configures all required middleware components for the EstateKit API with enhanced security and monitoring.
        /// </summary>
        /// <param name="app">The application builder instance</param>
        /// <param name="env">The web host environment</param>
        /// <returns>The configured application builder</returns>
        public static IApplicationBuilder UseEstateKitMiddleware(
            this IApplicationBuilder app,
            IWebHostEnvironment env)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (env == null) throw new ArgumentNullException(nameof(env));

            // Configure security headers
            app.UseSecurityHeaders();

            // Enable request logging and monitoring
            app.UseRequestLogging();

            // Configure correlation ID tracking
            app.UseCorrelationId();

            if (!env.IsDevelopment())
            {
                // Configure HSTS in production
                app.UseHsts();
            }

            // Enable HTTPS redirection
            app.UseHttpsRedirection();

            // Configure forwarded headers for proxy servers
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // Enable rate limiting
            app.UseRateLimiter();

            // Enable authentication and authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Configure API versioning
            app.UseApiVersioning();

            // Configure Swagger documentation in development
            if (env.IsDevelopment())
            {
                app.UseSwaggerConfiguration();
            }

            // Configure routing and endpoints
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });

            return app;
        }

        /// <summary>
        /// Configures enhanced request logging middleware with performance monitoring.
        /// </summary>
        private static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RequestLoggingMiddleware>();
        }

        /// <summary>
        /// Configures security headers for enhanced protection.
        /// </summary>
        private static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.Add("Permissions-Policy", 
                    "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
                context.Response.Headers.Add("Content-Security-Policy", 
                    "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';");

                await next();
            });
        }

        /// <summary>
        /// Configures correlation ID middleware for request tracking.
        /// </summary>
        private static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                const string correlationHeader = "X-Correlation-ID";
                
                if (!context.Request.Headers.TryGetValue(correlationHeader, out var correlationId))
                {
                    correlationId = Guid.NewGuid().ToString();
                    context.Request.Headers.Add(correlationHeader, correlationId.ToString());
                }

                context.Response.Headers.Add(correlationHeader, correlationId.ToString());
                
                await next();
            });
        }

        /// <summary>
        /// Configures rate limiting middleware with token bucket algorithm.
        /// </summary>
        private static IApplicationBuilder UseRateLimiter(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 1000,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 2,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    TokensPerPeriod = 1000,
                    AutoReplenishment = true
                });

                using var lease = await limiter.AcquireAsync();
                
                if (lease.IsAcquired)
                {
                    context.Response.Headers.Add("X-RateLimit-Limit", "1000");
                    context.Response.Headers.Add("X-RateLimit-Remaining", 
                        lease.GetRemainingPermits().ToString());
                    
                    await next();
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        error = "Too many requests", 
                        retryAfter = lease.GetRetryAfter()?.TotalSeconds ?? 60 
                    });
                }
            });
        }

        /// <summary>
        /// Configures enhanced Swagger documentation UI with security features.
        /// </summary>
        private static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app)
        {
            return app
                .UseSwagger(options =>
                {
                    options.RouteTemplate = "api-docs/{documentName}/swagger.json";
                    options.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                    {
                        swaggerDoc.Servers = new List<OpenApiServer>
                        {
                            new OpenApiServer 
                            { 
                                Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" 
                            }
                        };
                    });
                })
                .UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/api-docs/v1/swagger.json", "EstateKit Personal Information API v1");
                    options.RoutePrefix = "api-docs";
                    options.DocumentTitle = "EstateKit Personal Information API Documentation";
                    options.EnableDeepLinking();
                    options.DisplayRequestDuration();
                    options.EnableFilter();
                    options.EnableTryItOutByDefault();
                    
                    // Configure OAuth2
                    options.OAuthClientId("swagger-ui");
                    options.OAuthAppName("EstateKit API - Swagger");
                    options.OAuthUsePkce();
                });
        }
    }
}