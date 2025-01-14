# Core environment variable with validation
variable "environment" {
  type        = string
  description = "Environment name for resource naming and tagging (development, staging, production)"

  validation {
    condition     = contains(["development", "staging", "production"], var.environment)
    error_message = "Environment must be one of: development, staging, production"
  }
}

# Instance configuration variables
variable "db_instance_class" {
  type        = string
  description = "RDS instance class for PostgreSQL database - minimum t3.large for production workloads"
  default     = "db.t3.large"
}

variable "db_name" {
  type        = string
  description = "Name of the PostgreSQL database for EstateKit Personal Information API"
  default     = "estatekit"
}

variable "engine_version" {
  type        = string
  description = "PostgreSQL engine version - must be 16.0 or higher for required features"
  default     = "16.0"
}

# Storage configuration variables
variable "allocated_storage" {
  type        = number
  description = "Initial allocated storage size in GB - minimum 100GB for production"
  default     = 100
}

variable "max_allocated_storage" {
  type        = number
  description = "Maximum storage size in GB for autoscaling - should be 10x initial size"
  default     = 1000
}

# High availability configuration
variable "multi_az" {
  type        = bool
  description = "Enable Multi-AZ deployment for high availability - required for production"
  default     = true
}

# Backup and protection configuration
variable "backup_retention_period" {
  type        = number
  description = "Number of days to retain automated backups - 35 days for compliance"
  default     = 35
}

variable "deletion_protection" {
  type        = bool
  description = "Enable deletion protection for RDS instance - required for production"
  default     = true
}

# Network configuration variables
variable "private_subnet_ids" {
  type        = list(string)
  description = "List of private subnet IDs for RDS deployment - minimum 2 subnets required"
}

variable "rds_security_group_id" {
  type        = string
  description = "Security group ID for RDS instance - must allow access from API service"
}

# Encryption configuration
variable "kms_key_arn" {
  type        = string
  description = "ARN of KMS key for RDS encryption - required for data protection"
}

# Resource tagging
variable "tags" {
  type        = map(string)
  description = "Tags to apply to RDS resources including environment and service tags"
  default     = {}
}