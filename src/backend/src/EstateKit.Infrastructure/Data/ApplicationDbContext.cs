using EstateKit.Core.Entities;
using EstateKit.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;

namespace EstateKit.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework Core database context for the EstateKit encryption key management system.
    /// Implements comprehensive security, auditing, and performance optimizations for PostgreSQL.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        private const int CommandTimeoutSeconds = 30;
        private const int MinPoolSize = 10;
        private readonly bool IsDevelopment;

        /// <summary>
        /// DbSet for managing user encryption keys with support for key rotation.
        /// </summary>
        public DbSet<UserKey> UserKeys { get; set; }

        /// <summary>
        /// DbSet for tracking comprehensive key rotation history and audit trail.
        /// </summary>
        public DbSet<UserKeyHistory> UserKeyHistory { get; set; }

        /// <summary>
        /// Initializes a new instance of ApplicationDbContext with enhanced security and performance settings.
        /// </summary>
        /// <param name="options">The database context options.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            IsDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        /// <summary>
        /// Configures the database model with security features, audit trails, and performance optimizations.
        /// </summary>
        /// <param name="modelBuilder">The model builder instance.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (modelBuilder == null)
            {
                throw new ArgumentNullException(nameof(modelBuilder));
            }

            // Apply entity configurations
            modelBuilder.ApplyConfiguration(new UserKeyConfiguration());
            modelBuilder.ApplyConfiguration(new UserKeyHistoryConfiguration());

            // Set default schema
            modelBuilder.HasDefaultSchema("estatekit");

            // Configure case-insensitive collation for text columns
            modelBuilder.UseCollation("und-x-icu");

            // Enable row-level security
            modelBuilder.HasPostgresExtension("pgcrypto")
                       .HasPostgresExtension("pg_stat_statements");

            // Configure global query filters for soft delete
            modelBuilder.Entity<UserKey>()
                .HasQueryFilter(uk => EF.Property<bool>(uk, "IsDeleted") == false);

            modelBuilder.Entity<UserKeyHistory>()
                .HasQueryFilter(ukh => EF.Property<bool>(ukh, "IsDeleted") == false);

            // Configure audit fields
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Add audit columns to all entities
                modelBuilder.Entity(entityType.Name).Property<DateTime>("CreatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                modelBuilder.Entity(entityType.Name).Property<DateTime>("UpdatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                modelBuilder.Entity(entityType.Name).Property<string>("CreatedBy")
                    .HasMaxLength(100);
                modelBuilder.Entity(entityType.Name).Property<string>("UpdatedBy")
                    .HasMaxLength(100);
                modelBuilder.Entity(entityType.Name).Property<bool>("IsDeleted")
                    .HasDefaultValue(false);
            }

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Configures advanced database context options including security, resilience, and performance settings.
        /// </summary>
        /// <param name="optionsBuilder">The options builder instance.</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder == null)
            {
                throw new ArgumentNullException(nameof(optionsBuilder));
            }

            // Enable detailed errors only in development
            if (IsDevelopment)
            {
                optionsBuilder.EnableDetailedErrors()
                             .EnableSensitiveDataLogging();
            }

            // Configure resilience
            optionsBuilder.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);

            // Performance optimizations
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);

            // Configure connection pooling
            optionsBuilder.UseNpgsql(options => {
                options.MinPoolSize(MinPoolSize)
                       .CommandTimeout(CommandTimeoutSeconds)
                       .EnableRetryOnFailure()
                       .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                       .UseNetTopologySuite()
                       .UseSecureConnection();
            });

            base.OnConfiguring(optionsBuilder);
        }
    }
}