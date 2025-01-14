# Core Terraform configuration
terraform {
  required_version = "~> 1.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# Environment configuration
variable "environment" {
  description = "Environment name (e.g., development, staging, production)"
  type        = string
  
  validation {
    condition     = can(regex("^(development|staging|production)$", var.environment))
    error_message = "Environment must be one of: development, staging, production"
  }
}

# Cluster identification
variable "cluster_id" {
  description = "Identifier for the Redis cluster"
  type        = string
  
  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.cluster_id))
    error_message = "Cluster ID must contain only lowercase letters, numbers, and hyphens"
  }
}

# Instance configuration
variable "node_type" {
  description = "ElastiCache node instance type"
  type        = string
  default     = "cache.t4g.medium"
  
  validation {
    condition     = can(regex("^cache\\.[a-z0-9]+\\.[a-z0-9]+$", var.node_type))
    error_message = "Node type must be a valid ElastiCache instance type"
  }
}

# Network configuration
variable "vpc_id" {
  description = "ID of the VPC where Redis cluster will be deployed"
  type        = string
  
  validation {
    condition     = can(regex("^vpc-", var.vpc_id))
    error_message = "VPC ID must be a valid AWS VPC identifier"
  }
}

variable "private_subnet_ids" {
  description = "List of private subnet IDs for Redis deployment"
  type        = list(string)
  
  validation {
    condition     = length(var.private_subnet_ids) >= 2
    error_message = "At least two private subnets are required for high availability"
  }
}

# Security configuration
variable "auth_token" {
  description = "Authentication token for Redis cluster access"
  type        = string
  sensitive   = true
  
  validation {
    condition     = length(var.auth_token) >= 16
    error_message = "Auth token must be at least 16 characters long"
  }
}

# Maintenance configuration
variable "maintenance_window" {
  description = "Weekly maintenance window for Redis cluster"
  type        = string
  default     = "sun:03:00-sun:04:00"
  
  validation {
    condition     = can(regex("^[a-z]{3}:[0-9]{2}:[0-9]{2}-[a-z]{3}:[0-9]{2}:[0-9]{2}$", var.maintenance_window))
    error_message = "Maintenance window must be in format 'ddd:hh:mm-ddd:hh:mm'"
  }
}