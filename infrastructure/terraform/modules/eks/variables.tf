# Terraform variables definition file for the EKS module
# Version: 1.0
# Purpose: Configures the Kubernetes cluster deployment parameters for the EstateKit Personal Information API

variable "cluster_name" {
  type        = string
  description = "Name of the EKS cluster"

  validation {
    condition     = length(var.cluster_name) <= 100
    error_message = "Cluster name must be 100 characters or less"
  }
}

variable "kubernetes_version" {
  type        = string
  description = "Kubernetes version for the EKS cluster"
  default     = "1.27"

  validation {
    condition     = can(regex("^1\\.\\d+$", var.kubernetes_version))
    error_message = "Kubernetes version must be in format 1.x"
  }
}

variable "vpc_id" {
  type        = string
  description = "ID of the VPC where EKS cluster will be deployed"

  validation {
    condition     = can(regex("^vpc-", var.vpc_id))
    error_message = "VPC ID must start with 'vpc-'"
  }
}

variable "subnet_ids" {
  type        = list(string)
  description = "List of private subnet IDs for EKS node groups"

  validation {
    condition     = length(var.subnet_ids) >= 2
    error_message = "At least 2 subnet IDs are required for high availability"
  }
}

variable "node_groups" {
  type = map(object({
    name          = string
    instance_type = string
    desired_size  = number
    min_size      = number
    max_size      = number
    disk_size     = number
  }))
  description = "Map of node group configurations"
  default = {
    default = {
      name          = "default"
      instance_type = "t3.medium"
      desired_size  = 2
      min_size      = 1
      max_size      = 4
      disk_size     = 50
    }
  }
}

variable "enable_encryption" {
  type        = bool
  description = "Enable encryption for EKS cluster using KMS"
  default     = true
}

variable "tags" {
  type        = map(string)
  description = "Tags to apply to all EKS resources"
  default     = {}
}