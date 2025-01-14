# Terraform variables definition file for EstateKit Personal Information API infrastructure
# Version: 1.0
# Terraform Version: ~> 1.0

# Project Configuration
variable "project" {
  type        = string
  description = "Project name for resource naming and tagging"
  default     = "estatekit"
}

variable "environment" {
  type        = string
  description = "Environment name (development, staging, production)"
  validation {
    condition     = contains(["development", "staging", "production"], var.environment)
    error_message = "Environment must be one of: development, staging, production"
  }
}

# Region Configuration
variable "region" {
  type        = string
  description = "AWS region for primary deployment"
  default     = "us-east-1"
  validation {
    condition     = contains(["us-east-1", "us-west-2"], var.region)
    error_message = "Region must be one of: us-east-1, us-west-2"
  }
}

# Network Configuration
variable "vpc_cidr" {
  type        = string
  description = "CIDR block for VPC network"
  default     = "10.0.0.0/16"
  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "VPC CIDR must be a valid IPv4 CIDR block"
  }
}

# EKS Configuration
variable "eks_cluster_version" {
  type        = string
  description = "Kubernetes version for EKS cluster"
  default     = "1.27"
  validation {
    condition     = contains(["1.26", "1.27", "1.28"], var.eks_cluster_version)
    error_message = "EKS version must be one of: 1.26, 1.27, 1.28"
  }
}

variable "eks_node_instance_type" {
  type        = string
  description = "EC2 instance type for EKS nodes"
  default     = "t3.large"
  validation {
    condition     = contains(["t3.large", "t3.xlarge", "m5.large", "m5.xlarge"], var.eks_node_instance_type)
    error_message = "EKS node instance type must be one of: t3.large, t3.xlarge, m5.large, m5.xlarge"
  }
}

# RDS Configuration
variable "rds_instance_class" {
  type        = string
  description = "RDS instance class for PostgreSQL"
  default     = "db.t3.large"
  validation {
    condition     = contains(["db.t3.large", "db.t3.xlarge", "db.m5.large", "db.m5.xlarge"], var.rds_instance_class)
    error_message = "RDS instance class must be one of: db.t3.large, db.t3.xlarge, db.m5.large, db.m5.xlarge"
  }
}

variable "rds_engine_version" {
  type        = string
  description = "PostgreSQL version for RDS"
  default     = "16.0"
  validation {
    condition     = contains(["16.0", "15.4"], var.rds_engine_version)
    error_message = "PostgreSQL version must be one of: 16.0, 15.4"
  }
}

# ElastiCache Configuration
variable "redis_node_type" {
  type        = string
  description = "ElastiCache node type for Redis"
  default     = "cache.t3.medium"
  validation {
    condition     = contains(["cache.t3.medium", "cache.t3.large", "cache.m5.large"], var.redis_node_type)
    error_message = "Redis node type must be one of: cache.t3.medium, cache.t3.large, cache.m5.large"
  }
}

variable "redis_engine_version" {
  type        = string
  description = "Redis version for ElastiCache"
  default     = "7.2"
  validation {
    condition     = contains(["7.0", "7.2"], var.redis_engine_version)
    error_message = "Redis version must be one of: 7.0, 7.2"
  }
}

# Security Configuration
variable "key_deletion_window" {
  type        = number
  description = "KMS key deletion window in days"
  default     = 30
  validation {
    condition     = var.key_deletion_window >= 7 && var.key_deletion_window <= 30
    error_message = "KMS key deletion window must be between 7 and 30 days"
  }
}

# Backup Configuration
variable "backup_retention_period" {
  type        = number
  description = "RDS backup retention period in days"
  default     = 35
  validation {
    condition     = var.backup_retention_period >= 7 && var.backup_retention_period <= 35
    error_message = "Backup retention period must be between 7 and 35 days"
  }
}

# High Availability Configuration
variable "multi_az_enabled" {
  type        = bool
  description = "Enable Multi-AZ deployment for RDS"
  default     = true
}

variable "enable_deletion_protection" {
  type        = bool
  description = "Enable deletion protection for RDS"
  default     = true
}