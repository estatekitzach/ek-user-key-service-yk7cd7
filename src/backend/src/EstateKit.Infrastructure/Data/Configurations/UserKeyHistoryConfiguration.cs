using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EstateKit.Core.Entities;

namespace EstateKit.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Framework Core configuration class for UserKeyHistory entity implementing
    /// comprehensive audit trail and compliance requirements for encryption key rotation history.
    /// </summary>
    public class UserKeyHistoryConfiguration : IEntityTypeConfiguration<UserKeyHistory>
    {
        public void Configure(EntityTypeBuilder<UserKeyHistory> builder)
        {
            // Table configuration with security schema
            builder.ToTable("user_key_history", "security");

            // Primary key configuration with PostgreSQL identity
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(startValue: 1000)
                .IsRequired();

            // Foreign key relationship to UserKeys with cascade delete
            builder.HasOne(h => h.UserKey)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            // Key value configuration with encryption enabled
            builder.Property(h => h.KeyValue)
                .HasColumnType("varchar(200)")
                .IsRequired()
                .HasColumnName("key_value")
                .IsEncrypted(); // Enables column-level encryption

            // Rotation date with timezone and precision
            builder.Property(h => h.RotationDate)
                .HasColumnType("timestamp with time zone")
                .HasPrecision(6)
                .IsRequired()
                .HasColumnName("rotation_date")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Rotation reason with validation
            builder.Property(h => h.RotationReason)
                .HasColumnType("varchar(50)")
                .IsRequired()
                .HasColumnName("rotation_reason")
                .HasMaxLength(50);

            // Audit fields configuration
            builder.Property(h => h.CreatedBy)
                .HasColumnType("varchar(100)")
                .IsRequired()
                .HasColumnName("created_by")
                .HasMaxLength(100);

            builder.Property(h => h.SystemVersion)
                .HasColumnType("varchar(20)")
                .IsRequired()
                .HasColumnName("system_version")
                .HasMaxLength(20);

            // Optimized composite index for key rotation queries
            builder.HasIndex(h => new { h.UserId, h.RotationDate, h.RotationReason })
                .HasDatabaseName("ix_user_key_history_user_rotation")
                .IncludeProperties(h => new { h.KeyValue })
                .HasFilter("rotation_date >= CURRENT_DATE - INTERVAL '7 years'");

            // Row-level security policy
            builder.HasQueryFilter(h => EF.Property<string>(h, "TenantId") == 
                EF.Functions.GetCurrentTenantId());

            // Audit trigger configuration
            builder.HasTrigger("tr_user_key_history_audit")
                .HasDDL(@"
                    CREATE TRIGGER tr_user_key_history_audit
                    AFTER INSERT OR UPDATE OR DELETE ON security.user_key_history
                    FOR EACH ROW EXECUTE FUNCTION audit.log_key_history_changes();
                ");

            // Configure data sensitivity for GDPR compliance
            builder.HasDataSensitivity(DataSensitivity.Sensitive)
                .HasRetentionPolicy(RetentionPolicy.SevenYears);
        }
    }
}