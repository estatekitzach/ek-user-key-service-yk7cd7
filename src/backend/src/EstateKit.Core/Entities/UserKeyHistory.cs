using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace EstateKit.Core.Entities
{
    /// <summary>
    /// Represents a historical record of a user's encryption key rotation with enhanced audit capabilities
    /// and validation for compliance tracking and security investigation purposes.
    /// </summary>
    [Table("user_key_history")]
    public class UserKeyHistory
    {
        private const string KEY_FORMAT_PATTERN = @"^[A-Za-z0-9+/=\-_]+$";

        /// <summary>
        /// Unique identifier for the key history record.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; private set; }

        /// <summary>
        /// Foreign key reference to the associated user's key record.
        /// </summary>
        [Required]
        [Column("user_id")]
        [ForeignKey("UserKey")]
        public long UserId { get; private set; }

        /// <summary>
        /// The historical key value that was rotated.
        /// </summary>
        [Required]
        [Column("key_value")]
        [StringLength(200)]
        public string KeyValue { get; private set; }

        /// <summary>
        /// Timestamp when the key rotation occurred.
        /// </summary>
        [Required]
        [Column("rotation_date")]
        public DateTime RotationDate { get; private set; }

        /// <summary>
        /// Reason for the key rotation (e.g., scheduled, emergency, compliance).
        /// </summary>
        [Required]
        [Column("rotation_reason")]
        [StringLength(50)]
        public string RotationReason { get; private set; }

        /// <summary>
        /// Identity of the user or system that initiated the key rotation.
        /// </summary>
        [Required]
        [Column("created_by")]
        [StringLength(100)]
        public string CreatedBy { get; private set; }

        /// <summary>
        /// Version of the system when the key rotation occurred.
        /// </summary>
        [Required]
        [Column("system_version")]
        [StringLength(20)]
        public string SystemVersion { get; private set; }

        /// <summary>
        /// Navigation property to the associated UserKey entity.
        /// </summary>
        [Required]
        public UserKey UserKey { get; private set; }

        // Private parameterless constructor for EF Core
        private UserKeyHistory()
        {
        }

        /// <summary>
        /// Initializes a new instance of UserKeyHistory with required properties and validation.
        /// </summary>
        /// <param name="userId">The user ID associated with the key rotation.</param>
        /// <param name="keyValue">The historical key value being recorded.</param>
        /// <param name="rotationReason">The reason for the key rotation.</param>
        /// <param name="createdBy">Identity of the rotation initiator.</param>
        /// <param name="systemVersion">Current system version.</param>
        /// <exception cref="ArgumentException">Thrown when input parameters are invalid.</exception>
        public UserKeyHistory(long userId, string keyValue, string rotationReason, 
            string createdBy, string systemVersion)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("UserId must be greater than 0.", nameof(userId));
            }

            if (!ValidateKeyValue(keyValue))
            {
                throw new ArgumentException("Invalid key value format.", nameof(keyValue));
            }

            if (string.IsNullOrWhiteSpace(rotationReason))
            {
                throw new ArgumentException("Rotation reason is required.", nameof(rotationReason));
            }

            if (string.IsNullOrWhiteSpace(createdBy))
            {
                throw new ArgumentException("Creator identity is required.", nameof(createdBy));
            }

            if (string.IsNullOrWhiteSpace(systemVersion))
            {
                throw new ArgumentException("System version is required.", nameof(systemVersion));
            }

            UserId = userId;
            KeyValue = keyValue;
            RotationReason = rotationReason;
            CreatedBy = createdBy;
            SystemVersion = systemVersion;
            RotationDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Validates the format of the historical key value.
        /// </summary>
        /// <param name="keyValue">The key value to validate.</param>
        /// <returns>True if the key value is valid, false otherwise.</returns>
        private static bool ValidateKeyValue(string keyValue)
        {
            if (string.IsNullOrWhiteSpace(keyValue))
            {
                return false;
            }

            return Regex.IsMatch(keyValue, KEY_FORMAT_PATTERN, RegexOptions.Compiled);
        }
    }
}