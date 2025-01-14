# Main Terraform configuration file for EstateKit Personal Information API infrastructure
# Version: 1.0
# Provider versions:
# - AWS Provider v5.0
# - Kubernetes Provider v2.0

terraform {
  required_version = ">= 1.0"
  
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.0"
    }
  }

  backend "s3" {
    # Backend configuration should be provided via backend config file
    key = "estatekit/terraform.tfstate"
  }
}

# Configure AWS Provider
provider "aws" {
  region = var.region

  default_tags {
    tags = local.common_tags
  }
}

# Local variables
locals {
  common_tags = {
    Project            = var.project
    Environment        = var.environment
    ManagedBy         = "Terraform"
    SecurityLevel     = "High"
    DataClassification = "Sensitive"
  }
}

# VPC Module
module "vpc" {
  source = "./modules/vpc"

  project     = var.project
  environment = var.environment
  vpc_cidr    = var.vpc_cidr

  enable_nat_gateway     = true
  single_nat_gateway     = false
  enable_dns_hostnames   = true
  enable_dns_support     = true
  enable_flow_log        = true
  flow_log_retention_days = 90

  tags = local.common_tags
}

# EKS Module
module "eks" {
  source = "./modules/eks"

  cluster_name        = "${var.project}-${var.environment}"
  vpc_id             = module.vpc.vpc_id
  private_subnet_ids = module.vpc.private_subnets
  kubernetes_version = var.eks_cluster_version
  node_instance_type = var.eks_node_instance_type

  enable_cluster_autoscaler = true
  enable_metrics_server     = true
  enable_cluster_logging    = true
  log_retention_days       = 90

  tags = local.common_tags

  depends_on = [module.vpc]
}

# KMS Module (must be created before RDS and Redis for encryption)
module "kms" {
  source = "./modules/kms"

  alias             = "${var.project}-${var.environment}"
  deletion_window   = var.key_deletion_window
  enable_key_rotation = true
  multi_region      = true

  tags = local.common_tags
}

# RDS Module
module "rds" {
  source = "./modules/rds"

  identifier        = "${var.project}-${var.environment}"
  vpc_id           = module.vpc.vpc_id
  subnet_ids       = module.vpc.database_subnets
  instance_class   = var.rds_instance_class
  engine_version   = var.rds_engine_version

  multi_az                    = var.multi_az_enabled
  backup_retention_period     = var.backup_retention_period
  deletion_protection        = var.enable_deletion_protection
  performance_insights_enabled = true
  storage_encrypted          = true
  kms_key_id                = module.kms.key_arn

  tags = local.common_tags

  depends_on = [module.vpc, module.kms]
}

# Redis Module
module "redis" {
  source = "./modules/redis"

  cluster_id       = "${var.project}-${var.environment}"
  vpc_id          = module.vpc.vpc_id
  subnet_ids      = module.vpc.private_subnets
  node_type       = var.redis_node_type
  engine_version  = var.redis_engine_version

  cluster_mode_enabled        = true
  automatic_failover_enabled = true
  at_rest_encryption_enabled = true
  transit_encryption_enabled = true
  multi_az_enabled          = var.multi_az_enabled

  tags = local.common_tags

  depends_on = [module.vpc, module.kms]
}

# Outputs
output "vpc_id" {
  description = "VPC identifier"
  value       = module.vpc.vpc_id
}

output "eks_cluster_endpoint" {
  description = "EKS cluster endpoint"
  value       = module.eks.cluster_endpoint
  sensitive   = true
}

output "rds_endpoint" {
  description = "RDS instance endpoint"
  value       = module.rds.endpoint
  sensitive   = true
}

output "redis_endpoint" {
  description = "Redis cluster endpoint"
  value       = module.redis.endpoint
  sensitive   = true
}

output "kms_key_arn" {
  description = "KMS key ARN for encryption"
  value       = module.kms.key_arn
  sensitive   = true
}