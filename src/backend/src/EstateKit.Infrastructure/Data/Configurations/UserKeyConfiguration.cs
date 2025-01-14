using EstateKit.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EstateKit.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Framework Core configuration class that defines the database schema, constraints,
    /// and indexes for the UserKey entity in PostgreSQL, ensuring GDPR compliance and optimal
    /// query performance.
    /// </summary>
    public class UserKeyConfiguration : IEntityTypeConfiguration<UserKey>
    {
        public void Configure(EntityTypeBuilder<UserKey> builder)
        {
            // Table configuration
            builder.ToTable("user_keys", "public")
                .HasComment("Stores user encryption keys with support for rotation and GDPR compliance");

            // Primary key configuration
            builder.HasKey(uk => uk.UserId);
            builder.Property(uk => uk.UserId)
                .UseIdentityColumn()
                .HasColumnType("bigint")
                .IsRequired()
                .HasComment("Unique identifier for the user associated with this encryption key");

            // Key value configuration with GDPR-compliant constraints
            builder.Property(uk => uk.Key)
                .HasColumnType("varchar(200)")
                .IsRequired()
                .UseCollation("C")  // Case-sensitive binary collation for key values
                .HasComment("Public key value used for encryption operations");

            // Timestamp configurations with time zone awareness
            builder.Property(uk => uk.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasComment("UTC timestamp of key creation");

            builder.Property(uk => uk.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasComment("UTC timestamp of last key modification");

            // Status and version tracking configuration
            builder.Property(uk => uk.IsActive)
                .HasColumnType("boolean")
                .IsRequired()
                .HasDefaultValue(true)
                .HasComment("Indicates if this is the currently active key for the user");

            builder.Property(uk => uk.RotationVersion)
                .HasColumnType("bigint")
                .IsRequired()
                .HasDefaultValue(1)
                .HasComment("Incremental version number for key rotation tracking");

            // Concurrency token configuration
            builder.Property(uk => uk.RowVersion)
                .IsRowVersion()
                .HasColumnType("bytea")
                .IsRequired()
                .HasComment("Concurrency token for optimistic locking");

            // Index configurations for performance optimization
            builder.HasIndex(uk => new { uk.IsActive, uk.UserId })
                .HasDatabaseName("IX_UserKeys_IsActive_UserId")
                .HasFilter("\"is_active\" = true")
                .HasComment("Optimized index for active key lookups");

            builder.HasIndex(uk => uk.CreatedAt)
                .HasDatabaseName("IX_UserKeys_CreatedAt")
                .HasComment("Index supporting audit queries by creation date");

            // Additional PostgreSQL-specific configurations
            builder.HasAnnotation("Npgsql:TableSpace", "pg_default")
                .HasAnnotation("Npgsql:Storage", "HEAP");
        }
    }
}