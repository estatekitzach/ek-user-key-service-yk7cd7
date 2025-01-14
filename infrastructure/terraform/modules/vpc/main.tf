# Configure AWS provider with version constraint
terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# VPC module configuration for secure network infrastructure
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.0"

  name = "${var.project}-${var.environment}"
  cidr = var.vpc_cidr

  # Multi-AZ configuration for high availability
  azs              = var.azs
  private_subnets  = var.private_subnets
  public_subnets   = var.public_subnets
  database_subnets = var.database_subnets

  # NAT Gateway configuration for private subnet internet access
  enable_nat_gateway = var.enable_nat_gateway
  single_nat_gateway = var.single_nat_gateway

  # DNS configuration required for EKS and RDS
  enable_dns_hostnames = var.enable_dns_hostnames
  enable_dns_support   = var.enable_dns_support

  # VPC Flow Logs for security monitoring and compliance
  enable_flow_log                      = true
  create_flow_log_cloudwatch_log_group = true
  create_flow_log_cloudwatch_iam_role  = true
  flow_log_max_aggregation_interval    = 60

  # Resource tagging for compliance and management
  tags = merge(var.tags, {
    Environment         = var.environment
    Project            = var.project
    ManagedBy          = "terraform"
    SecurityCompliance = "financial-services"
  })
}

# Security group for EKS cluster with minimum required access
resource "aws_security_group" "eks_security_group" {
  name        = "${var.project}-${var.environment}-eks"
  description = "Security group for EKS cluster with minimum required access"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description = "HTTPS inbound"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = [var.vpc_cidr]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name          = "${var.project}-${var.environment}-eks"
    SecurityLevel = "high"
  }
}

# Security group for RDS with PostgreSQL access
resource "aws_security_group" "rds_security_group" {
  name        = "${var.project}-${var.environment}-rds"
  description = "Security group for RDS with PostgreSQL access"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description     = "PostgreSQL access"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.eks_security_group.id]
  }

  tags = {
    Name          = "${var.project}-${var.environment}-rds"
    SecurityLevel = "critical"
  }
}

# Security group for Redis cache
resource "aws_security_group" "redis_security_group" {
  name        = "${var.project}-${var.environment}-redis"
  description = "Security group for Redis cache"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description     = "Redis access"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [aws_security_group.eks_security_group.id]
  }

  tags = {
    Name          = "${var.project}-${var.environment}-redis"
    SecurityLevel = "high"
  }
}

# Output definitions for use in other modules
output "vpc_id" {
  description = "ID of the created VPC"
  value       = module.vpc.vpc_id
}

output "private_subnets" {
  description = "List of private subnet IDs"
  value       = module.vpc.private_subnets
}

output "database_subnets" {
  description = "List of database subnet IDs"
  value       = module.vpc.database_subnets
}

output "security_groups" {
  description = "Map of security group IDs"
  value = {
    eks_security_group_id   = aws_security_group.eks_security_group.id
    rds_security_group_id   = aws_security_group.rds_security_group.id
    redis_security_group_id = aws_security_group.redis_security_group.id
  }
}