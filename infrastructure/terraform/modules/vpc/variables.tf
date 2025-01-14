# Core project variables
variable "project" {
  type        = string
  description = "Project name for resource naming and tagging"
  default     = "estatekit"

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.project))
    error_message = "Project name must consist of lowercase letters, numbers, and hyphens only."
  }
}

variable "environment" {
  type        = string
  description = "Environment name (development, staging, production)"

  validation {
    condition     = contains(["development", "staging", "production"], var.environment)
    error_message = "Environment must be one of: development, staging, production"
  }
}

# VPC configuration variables
variable "vpc_cidr" {
  type        = string
  description = "CIDR block for the VPC (must be /16 for proper subnet allocation)"
  default     = "10.0.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0)) && split("/", var.vpc_cidr)[1] == "16"
    error_message = "VPC CIDR must be a valid /16 CIDR block."
  }
}

variable "azs" {
  type        = list(string)
  description = "List of availability zones for multi-AZ deployment (minimum 2 for HA)"

  validation {
    condition     = length(var.azs) >= 2
    error_message = "At least 2 availability zones are required for high availability."
  }
}

# Subnet configuration variables
variable "private_subnets" {
  type        = list(string)
  description = "List of CIDR blocks for private subnets (one per AZ, must be within VPC CIDR)"

  validation {
    condition     = length(var.private_subnets) == length(var.azs)
    error_message = "Number of private subnets must match number of availability zones."
  }
}

variable "public_subnets" {
  type        = list(string)
  description = "List of CIDR blocks for public subnets (one per AZ, must be within VPC CIDR)"

  validation {
    condition     = length(var.public_subnets) == length(var.azs)
    error_message = "Number of public subnets must match number of availability zones."
  }
}

variable "database_subnets" {
  type        = list(string)
  description = "List of CIDR blocks for database subnets (one per AZ, must be within VPC CIDR)"

  validation {
    condition     = length(var.database_subnets) == length(var.azs)
    error_message = "Number of database subnets must match number of availability zones."
  }
}

# Network feature flags
variable "enable_nat_gateway" {
  type        = bool
  description = "Enable NAT Gateway for private subnet internet access (required for EKS)"
  default     = true
}

variable "single_nat_gateway" {
  type        = bool
  description = "Use single NAT Gateway instead of one per AZ (not recommended for production)"
  default     = false
}

variable "enable_dns_hostnames" {
  type        = bool
  description = "Enable DNS hostnames in the VPC (required for EKS and RDS)"
  default     = true
}

variable "enable_dns_support" {
  type        = bool
  description = "Enable DNS support in the VPC (required for EKS and RDS)"
  default     = true
}

# Resource tagging
variable "tags" {
  type        = map(string)
  description = "Tags to be applied to all resources (must include required security and compliance tags)"
  default = {
    Terraform           = "true"
    Project            = "estatekit"
    SecurityCompliance = "required"
  }

  validation {
    condition     = contains(keys(var.tags), "SecurityCompliance")
    error_message = "Tags must include SecurityCompliance tag for audit purposes."
  }
}