# Network Infrastructure Outputs
output "vpc_id" {
  description = "ID of the VPC hosting the EstateKit infrastructure"
  value       = module.vpc.vpc_id
}

output "private_subnet_ids" {
  description = "List of private subnet IDs where secure resources are deployed"
  value       = module.vpc.private_subnet_ids
}

output "availability_zones" {
  description = "List of availability zones used for multi-AZ deployment"
  value       = module.vpc.azs
}

# EKS Cluster Outputs
output "eks_cluster_endpoint" {
  description = "Endpoint for EKS control plane API server"
  value       = module.eks.cluster_endpoint
}

output "eks_cluster_security_group_id" {
  description = "Security group ID attached to the EKS cluster"
  value       = module.eks.cluster_security_group_id
}

# Database Outputs
output "rds_endpoint" {
  description = "Connection endpoint for RDS PostgreSQL primary instance"
  value       = module.rds.primary_endpoint
  sensitive   = true
}

output "rds_replica_endpoints" {
  description = "Connection endpoints for RDS PostgreSQL read replicas"
  value       = module.rds.replica_endpoints
  sensitive   = true
}

# Cache Outputs
output "redis_primary_endpoint" {
  description = "Connection endpoint for Redis ElastiCache primary node"
  value       = module.elasticache.primary_endpoint
  sensitive   = true
}

# Security Outputs
output "kms_key_arns" {
  description = "Map of KMS key ARNs used for encryption"
  value = {
    rds     = module.kms.rds_key_arn
    ebs     = module.kms.ebs_key_arn
    secrets = module.kms.secrets_key_arn
    app     = module.kms.application_key_arn
  }
}

output "security_group_ids" {
  description = "Map of security group IDs for each component"
  value = {
    eks_nodes     = module.eks.node_security_group_id
    rds          = module.rds.security_group_id
    redis        = module.elasticache.security_group_id
    application  = module.security.app_security_group_id
  }
}

output "iam_role_arns" {
  description = "Map of IAM role ARNs for service accounts"
  value = {
    eks_nodes    = module.eks.node_iam_role_arn
    rds_proxy    = module.rds.proxy_iam_role_arn
    application  = module.iam.app_role_arn
    kms_admin    = module.iam.kms_admin_role_arn
  }
}

# Disaster Recovery Outputs
output "dr_region_config" {
  description = "Configuration details for disaster recovery region"
  value = {
    region             = var.dr_region
    vpc_id            = module.dr_vpc.vpc_id
    private_subnets   = module.dr_vpc.private_subnet_ids
    rds_endpoint      = module.dr_rds.primary_endpoint
    eks_endpoint      = module.dr_eks.cluster_endpoint
  }
  sensitive = true
}

# Metadata Outputs
output "environment" {
  description = "Deployment environment identifier"
  value       = var.environment
}

output "aws_region" {
  description = "Primary AWS region where resources are deployed"
  value       = var.aws_region
}

output "tags" {
  description = "Common tags applied to all resources"
  value       = local.common_tags
}