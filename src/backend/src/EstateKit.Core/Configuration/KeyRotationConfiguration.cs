using Microsoft.Extensions.Configuration; // v9.0.0
using System; // v9.0.0

namespace EstateKit.Core.Configuration
{
    /// <summary>
    /// Configuration settings for encryption key rotation policies and schedules.
    /// Supports regular, compliance, and emergency rotation scenarios with comprehensive validation rules.
    /// </summary>
    public class KeyRotationConfiguration
    {
        /// <summary>
        /// Gets or sets the interval in days for regular automated key rotation.
        /// Valid range: 1-180 days.
        /// </summary>
        public int RegularRotationIntervalDays { get; set; }

        /// <summary>
        /// Gets or sets the interval in days for compliance-mandated key rotation.
        /// Valid range: 1-730 days.
        /// </summary>
        public int ComplianceRotationIntervalDays { get; set; }

        /// <summary>
        /// Gets or sets the timeout duration for emergency key rotation operations.
        /// Valid range: 1-30 minutes.
        /// </summary>
        public TimeSpan EmergencyRotationTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed rotation operations.
        /// Valid range: 1-10 attempts.
        /// </summary>
        public int MaxRetryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the delay between retry attempts for failed rotation operations.
        /// Valid range: 1 second - 5 minutes.
        /// </summary>
        public TimeSpan RetryDelay { get; set; }

        /// <summary>
        /// Gets or sets whether automatic key rotation is enabled.
        /// Default: true for enhanced security.
        /// </summary>
        public bool EnableAutomaticRotation { get; set; }

        /// <summary>
        /// Gets or sets the timeout duration for rotation lock acquisition.
        /// Valid range: 1-60 minutes.
        /// </summary>
        public TimeSpan RotationLockTimeout { get; set; }

        /// <summary>
        /// Gets or sets the minimum age requirement for keys before allowing rotation.
        /// Valid range: 1-30 days.
        /// </summary>
        public int MinimumKeyAge { get; set; }

        /// <summary>
        /// Gets or sets whether audit logging is required for rotation operations.
        /// Default: true for compliance tracking.
        /// </summary>
        public bool RequireAuditLogging { get; set; }

        /// <summary>
        /// Initializes a new instance of KeyRotationConfiguration with secure default values
        /// aligned with enterprise security standards.
        /// </summary>
        public KeyRotationConfiguration()
        {
            // Standard security compliance default of 90 days
            RegularRotationIntervalDays = 90;
            
            // Annual regulatory compliance requirement
            ComplianceRotationIntervalDays = 365;
            
            // Quick security response window
            EmergencyRotationTimeout = TimeSpan.FromMinutes(5);
            
            // Balanced resilience with 3 retry attempts
            MaxRetryAttempts = 3;
            
            // Optimal retry timing of 30 seconds
            RetryDelay = TimeSpan.FromSeconds(30);
            
            // Enable automatic rotation by default for security
            EnableAutomaticRotation = true;
            
            // Prevent concurrent rotations with 15-minute timeout
            RotationLockTimeout = TimeSpan.FromMinutes(15);
            
            // Prevent excessive rotations with 1-day minimum age
            MinimumKeyAge = 1;
            
            // Enable mandatory audit logging for compliance tracking
            RequireAuditLogging = true;
        }

        /// <summary>
        /// Performs comprehensive validation of all rotation configuration settings
        /// to ensure security and operational compliance.
        /// </summary>
        /// <returns>True if all configuration settings meet security and operational requirements.</returns>
        public bool Validate()
        {
            // Regular rotation interval validation (1-180 days)
            if (RegularRotationIntervalDays < 1 || RegularRotationIntervalDays > 180)
                return false;

            // Compliance rotation interval validation (1-730 days)
            if (ComplianceRotationIntervalDays < 1 || ComplianceRotationIntervalDays > 730)
                return false;

            // Emergency rotation timeout validation (1-30 minutes)
            if (EmergencyRotationTimeout < TimeSpan.FromMinutes(1) || 
                EmergencyRotationTimeout > TimeSpan.FromMinutes(30))
                return false;

            // Retry attempts validation (1-10 attempts)
            if (MaxRetryAttempts < 1 || MaxRetryAttempts > 10)
                return false;

            // Retry delay validation (1 second - 5 minutes)
            if (RetryDelay < TimeSpan.FromSeconds(1) || 
                RetryDelay > TimeSpan.FromMinutes(5))
                return false;

            // Rotation lock timeout validation (1-60 minutes)
            if (RotationLockTimeout < TimeSpan.FromMinutes(1) || 
                RotationLockTimeout > TimeSpan.FromMinutes(60))
                return false;

            // Minimum key age validation (1-30 days)
            if (MinimumKeyAge < 1 || MinimumKeyAge > 30)
                return false;

            // Audit logging must be enabled for compliance
            if (!RequireAuditLogging)
                return false;

            return true;
        }
    }
}