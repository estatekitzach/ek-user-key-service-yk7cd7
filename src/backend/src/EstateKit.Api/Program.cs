using Amazon.KeyManagementService;
using EstateKit.Api.Extensions;
using EstateKit.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure AWS services with FIPS endpoints
builder.Services.Configure<AWSOptions>(options =>
{
    options.UseFIPSEndpoint = true;
    options.Region = builder.Configuration["AWS:Region"];
});

// Configure structured logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
});

// Configure OpenTelemetry tracing and metrics
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("EstateKit.PersonalInformationApi", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddAWSInstrumentation()
        .AddRedisInstrumentation()
        .AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation());

// Configure CORS with strict origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("EstateKitPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>())
            .AllowedHeaders("Authorization", "Content-Type", "X-Correlation-ID")
            .AllowedMethods("GET", "POST")
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowCredentials();
    });
});

// Configure rate limiting for 1000+ concurrent requests
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter("GlobalLimiter",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 1000,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 1000,
                AutoReplenishment = true
            }));
});

// Configure JWT authentication with enhanced validation
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AWS:Cognito:Authority"];
        options.Audience = builder.Configuration["AWS:Cognito:Audience"];
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5);
    });

// Configure API versioning and documentation
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Configure response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json" });
});

// Configure thread pool settings for high concurrency
ThreadPool.SetMinThreads(100, 100);
ThreadPool.SetMaxThreads(1000, 1000);

// Configure health checks with dependencies
builder.Services.AddHealthChecks()
    .AddRedis(builder.Configuration.GetConnectionString("Redis"), tags: new[] { "cache" })
    .AddNpgSql(builder.Configuration.GetConnectionString("Database"), tags: new[] { "database" })
    .AddAWSService<IAmazonKeyManagementService>(tags: new[] { "kms" });

// Add EstateKit services
builder.Services.AddEstateKitServices(builder.Configuration);

// Build the application
var app = builder.Build();

// Configure security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", 
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
    await next();
});

// Configure HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Configure middleware pipeline
app.UseEstateKitMiddleware(app.Environment);

// Configure graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    // Allow 30 seconds for in-flight requests to complete
    Thread.Sleep(TimeSpan.FromSeconds(30));
});

// Start the application
await app.RunAsync();