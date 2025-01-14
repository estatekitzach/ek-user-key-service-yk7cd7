using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using EstateKit.Core.Constants;

namespace EstateKit.Core.Entities
{
    /// <summary>
    /// Represents a user's encryption key in the EstateKit system with support for 
    /// key rotation, history tracking, and optimistic concurrency control.
    /// </summary>
    [Table("user_keys")]
    public class UserKey
    {
        private const int MIN_KEY_LENGTH = 2048;
        private const string KEY_FORMAT_PATTERN = @"^[A-Za-z0-9+/=\-_]+$";

        /// <summary>
        /// Unique identifier for the user associated with this encryption key.
        /// </summary>
        [Key]
        [Required]
        [Column("user_id")]
        public long UserId { get; private set; }

        /// <summary>
        /// The public key value used for encryption operations.
        /// </summary>
        [Required]
        [Column("key")]
        [StringLength(2048)]
        public string Key { get; private set; }

        /// <summary>
        /// Timestamp when the key was initially created.
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Timestamp of the last modification to the key.
        /// </summary>
        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; private set; }

        /// <summary>
        /// Indicates whether this is the currently active key for the user.
        /// </summary>
        [Required]
        [Column("is_active")]
        public bool IsActive { get; private set; }

        /// <summary>
        /// Version number incremented during key rotations.
        /// </summary>
        [Required]
        [Column("rotation_version")]
        public long RotationVersion { get; private set; }

        /// <summary>
        /// Concurrency token for optimistic locking.
        /// </summary>
        [Required]
        [Timestamp]
        [ConcurrencyCheck]
        [Column("row_version")]
        public byte[] RowVersion { get; private set; }

        // Private parameterless constructor for EF Core
        private UserKey()
        {
        }

        /// <summary>
        /// Initializes a new instance of UserKey with required properties and default values.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="key">The public key value.</param>
        /// <exception cref="ArgumentException">Thrown when userId is invalid or key is null/empty.</exception>
        public UserKey(long userId, string key)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("UserId must be greater than 0.", nameof(userId));
            }

            if (!ValidateKey(key))
            {
                throw new ArgumentException("Invalid key format or length.", nameof(key));
            }

            UserId = userId;
            Key = key;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            IsActive = true;
            RotationVersion = 1;
            RowVersion = new byte[8]; // Initialize concurrency token
        }

        /// <summary>
        /// Marks the key as inactive during rotation with concurrency check.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when key is already inactive.</exception>
        public void Deactivate()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("Key is already inactive.");
            }

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the key value during rotation with validation and concurrency control.
        /// </summary>
        /// <param name="newKey">The new public key value.</param>
        /// <exception cref="ArgumentException">Thrown when newKey is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when key rotation is in progress.</exception>
        [ConcurrencyCheck]
        public void UpdateKey(string newKey)
        {
            if (!ValidateKey(newKey))
            {
                throw new ArgumentException(ErrorCodes.KEY_INVALID, nameof(newKey));
            }

            if (!IsActive)
            {
                throw new InvalidOperationException(ErrorCodes.KEY_ROTATION_IN_PROGRESS);
            }

            Key = newKey;
            RotationVersion++;
            UpdatedAt = DateTime.UtcNow;
            IsActive = true;
        }

        /// <summary>
        /// Validates the key format and status.
        /// </summary>
        /// <param name="key">The key to validate.</param>
        /// <returns>True if key is valid, false otherwise.</returns>
        public static bool ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (key.Length < MIN_KEY_LENGTH)
            {
                return false;
            }

            return Regex.IsMatch(key, KEY_FORMAT_PATTERN, RegexOptions.Compiled);
        }
    }
}