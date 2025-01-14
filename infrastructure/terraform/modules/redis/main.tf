# Provider configuration
terraform {
  required_version = "~> 1.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws" # version ~> 5.0
    }
    random = {
      source  = "hashicorp/random" # version ~> 3.0
    }
  }
}

# Random suffix for unique resource names
resource "random_id" "suffix" {
  byte_length = 4
}

# Redis parameter group with optimized settings
resource "aws_elasticache_parameter_group" "redis" {
  family      = "redis7.x"
  name        = "${var.cluster_id}-params-${random_id.suffix.hex}"
  description = "EstateKit Redis optimized parameters for key caching"

  parameter {
    name  = "maxmemory-policy"
    value = "allkeys-lru"  # LRU eviction for TTL management
  }

  parameter {
    name  = "timeout"
    value = "900"  # 15-minute connection timeout
  }

  parameter {
    name  = "maxmemory-samples"
    value = "10"  # Optimized LRU sampling
  }

  parameter {
    name  = "tcp-keepalive"
    value = "300"  # TCP keepalive for connection stability
  }

  parameter {
    name  = "notify-keyspace-events"
    value = "Ex"  # Enable keyspace notifications for TTL events
  }

  tags = {
    Environment = var.environment
    Service     = "EstateKit"
    ManagedBy   = "Terraform"
  }
}

# Subnet group for Redis deployment
resource "aws_elasticache_subnet_group" "redis" {
  name        = "${var.cluster_id}-subnet-${random_id.suffix.hex}"
  subnet_ids  = var.private_subnet_ids
  description = "Private subnet group for EstateKit Redis cluster"

  tags = {
    Environment  = var.environment
    Service      = "EstateKit-Redis"
    NetworkType  = "Private"
    ManagedBy    = "Terraform"
  }
}

# Security group for Redis access
resource "aws_security_group" "redis" {
  name        = "${var.cluster_id}-sg-${random_id.suffix.hex}"
  vpc_id      = var.vpc_id
  description = "Security group for EstateKit Redis cluster"

  ingress {
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    cidr_blocks     = ["10.0.0.0/8"]  # VPC CIDR range
    description     = "Redis port access from VPC"
  }

  egress {
    from_port       = 0
    to_port         = 0
    protocol        = "-1"
    cidr_blocks     = ["0.0.0.0/0"]
    description     = "Allow outbound traffic"
  }

  tags = {
    Environment   = var.environment
    Service       = "EstateKit-Redis"
    SecurityLevel = "High"
    ManagedBy     = "Terraform"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# Redis replication group
resource "aws_elasticache_replication_group" "redis" {
  replication_group_id          = "${var.cluster_id}-${random_id.suffix.hex}"
  description                   = "EstateKit Redis cluster for secure key caching"
  node_type                     = var.node_type
  port                         = 6379
  parameter_group_name         = aws_elasticache_parameter_group.redis.name
  subnet_group_name            = aws_elasticache_subnet_group.redis.name
  security_group_ids           = [aws_security_group.redis.id]
  
  # High availability configuration
  automatic_failover_enabled   = true
  multi_az_enabled            = true
  num_cache_clusters          = 2
  
  # Engine configuration
  engine                      = "redis"
  engine_version             = "7.2"
  
  # Security configuration
  at_rest_encryption_enabled  = true
  transit_encryption_enabled  = true
  auth_token                 = var.auth_token
  
  # Maintenance configuration
  maintenance_window         = var.maintenance_window
  snapshot_retention_limit   = 7
  snapshot_window           = "03:00-04:00"
  auto_minor_version_upgrade = true
  
  # Performance configuration
  preferred_cache_cluster_azs = slice(data.aws_availability_zones.available.names, 0, 2)

  tags = {
    Environment    = var.environment
    Service       = "EstateKit"
    ManagedBy     = "Terraform"
    SecurityLevel = "High"
    Encryption    = "Required"
  }

  lifecycle {
    prevent_destroy = true
  }
}

# Data source for AZ information
data "aws_availability_zones" "available" {
  state = "available"
}

# Outputs for other modules
output "redis_endpoints" {
  description = "Redis cluster endpoints"
  value = {
    primary_endpoint_address = aws_elasticache_replication_group.redis.primary_endpoint_address
    reader_endpoint_address = aws_elasticache_replication_group.redis.reader_endpoint_address
    port                   = aws_elasticache_replication_group.redis.port
  }
}

output "security_group_id" {
  description = "ID of the Redis security group"
  value       = aws_security_group.redis.id
}