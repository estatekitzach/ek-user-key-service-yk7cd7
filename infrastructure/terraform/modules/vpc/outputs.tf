# VPC identifier output for other modules
output "vpc_id" {
  description = "The ID of the VPC containing the EstateKit Personal Information API infrastructure"
  value       = module.vpc.vpc_id
  sensitive   = false
}

# Private subnet IDs for EKS cluster deployment
output "private_subnets" {
  description = "List of private subnet IDs for secure EKS cluster deployment across multiple availability zones"
  value       = module.vpc.private_subnets
  sensitive   = false
}

# Public subnet IDs for load balancer deployment
output "public_subnets" {
  description = "List of public subnet IDs for load balancer and NAT gateway deployment"
  value       = module.vpc.public_subnets
  sensitive   = false
}

# Database subnet IDs for RDS deployment
output "database_subnets" {
  description = "List of isolated database subnet IDs for RDS deployment with multi-AZ support"
  value       = module.vpc.database_subnets
  sensitive   = false
}

# Security group ID for EKS cluster
output "eks_security_group_id" {
  description = "Security group ID controlling network access for the EKS cluster workloads"
  value       = aws_security_group.eks_security_group.id
  sensitive   = false
}

# Security group ID for RDS instances
output "rds_security_group_id" {
  description = "Security group ID managing access to the RDS database instances"
  value       = aws_security_group.rds_security_group.id
  sensitive   = false
}

# Security group ID for Redis cache
output "redis_security_group_id" {
  description = "Security group ID controlling access to the Redis cache cluster"
  value       = aws_security_group.redis_security_group.id
  sensitive   = false
}

# CIDR blocks for network configuration
output "vpc_cidr_block" {
  description = "The CIDR block of the VPC for network planning and security group rules"
  value       = module.vpc.vpc_cidr_block
  sensitive   = false
}

# NAT Gateway IPs for egress traffic
output "nat_public_ips" {
  description = "List of public Elastic IPs created for NAT Gateway egress traffic"
  value       = module.vpc.nat_public_ips
  sensitive   = false
}

# VPC Default Security Group
output "default_security_group_id" {
  description = "The ID of the VPC's default security group for reference and cleanup"
  value       = module.vpc.default_security_group_id
  sensitive   = false
}

# VPC Flow Log configuration
output "vpc_flow_log_id" {
  description = "The ID of the VPC Flow Log for security monitoring and compliance"
  value       = module.vpc.vpc_flow_log_id
  sensitive   = false
}

# Route Table IDs for network troubleshooting
output "private_route_table_ids" {
  description = "List of private route table IDs for network configuration validation"
  value       = module.vpc.private_route_table_ids
  sensitive   = false
}

output "public_route_table_ids" {
  description = "List of public route table IDs for network configuration validation"
  value       = module.vpc.public_route_table_ids
  sensitive   = false
}

output "database_route_table_ids" {
  description = "List of database route table IDs for network configuration validation"
  value       = module.vpc.database_route_table_ids
  sensitive   = false
}