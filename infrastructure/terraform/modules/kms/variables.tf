# Core project variable for resource naming and tagging
variable "project" {
  type        = string
  description = "Project name for resource naming and tagging"
  default     = "estatekit"

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.project))
    error_message = "Project name must contain only lowercase letters, numbers, and hyphens"
  }
}

# Environment segregation variable
variable "environment" {
  type        = string
  description = "Environment name for deployment segregation (development, staging, production)"

  validation {
    condition     = contains(["development", "staging", "production"], var.environment)
    error_message = "Environment must be one of: development, staging, production"
  }
}

# KMS key deletion window configuration (FIPS 140-2 compliant)
variable "key_deletion_window" {
  type        = number
  description = "KMS key deletion window in days (FIPS 140-2 compliant range)"
  default     = 30

  validation {
    condition     = var.key_deletion_window >= 7 && var.key_deletion_window <= 30
    error_message = "Key deletion window must be between 7 and 30 days for FIPS 140-2 compliance"
  }
}

# Automatic key rotation configuration
variable "key_rotation_enabled" {
  type        = bool
  description = "Enable automatic key rotation (90-day rotation period per security requirements)"
  default     = true
}

# KMS key alias prefix configuration
variable "key_alias_prefix" {
  type        = string
  description = "Prefix for KMS key aliases following EstateKit naming convention"
  default     = "estatekit-kms"

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]+$", var.key_alias_prefix))
    error_message = "Key alias prefix must start with a letter and contain only lowercase letters, numbers, and hyphens"
  }
}

# Resource tagging configuration including compliance tags
variable "tags" {
  type        = map(string)
  description = "Additional tags for KMS resources including required security and compliance tracking"
  default = {
    Compliance         = "FIPS140-2"
    SecurityZone      = "restricted"
    DataClassification = "confidential"
  }
}