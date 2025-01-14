using System;
using System.ComponentModel.DataAnnotations;

namespace EstateKit.Api.DTOs
{
    /// <summary>
    /// Data Transfer Object that encapsulates parameters for key rotation requests,
    /// supporting immediate, scheduled, and emergency rotation scenarios with comprehensive validation.
    /// </summary>
    public class RotateKeyRequestDto
    {
        /// <summary>
        /// Unique identifier of the user whose encryption key needs rotation.
        /// Must be a positive number.
        /// </summary>
        [Required(ErrorMessage = "User ID is required for key rotation")]
        [Range(1, long.MaxValue, ErrorMessage = "User ID must be a positive number")]
        public long UserId { get; set; }

        /// <summary>
        /// Business or security reason for initiating the key rotation.
        /// Limited to 50 characters to ensure concise documentation.
        /// </summary>
        [Required(ErrorMessage = "A reason must be specified for key rotation")]
        [StringLength(50, ErrorMessage = "Rotation reason cannot exceed 50 characters")]
        public string RotationReason { get; set; }

        /// <summary>
        /// Optional identifier for security incident-triggered rotations.
        /// Used to link key rotations to security incident tracking systems.
        /// Format should match UUID/GUID format (36 characters).
        /// </summary>
        [StringLength(36, ErrorMessage = "Security incident ID cannot exceed 36 characters")]
        public string SecurityIncidentId { get; set; }

        /// <summary>
        /// Optional future timestamp for scheduled key rotations.
        /// Used for compliance-driven or planned rotation scenarios.
        /// Must be a future date/time when specified.
        /// </summary>
        [FutureDate(ErrorMessage = "Scheduled rotation time must be in the future")]
        public DateTime? ScheduledTime { get; set; }
    }

    /// <summary>
    /// Custom validation attribute to ensure scheduled rotation times are in the future.
    /// </summary>
    public class FutureDateAttribute : ValidationAttribute
    {
        /// <summary>
        /// Validates that the provided date is in the future.
        /// </summary>
        /// <param name="value">The date value to validate</param>
        /// <param name="validationContext">The validation context</param>
        /// <returns>ValidationResult indicating success or failure</returns>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            if (value is DateTime dateTime)
            {
                if (dateTime > DateTime.UtcNow)
                {
                    return ValidationResult.Success;
                }
                return new ValidationResult(ErrorMessage ?? "Scheduled time must be in the future");
            }

            return new ValidationResult("Invalid date format");
        }
    }
}