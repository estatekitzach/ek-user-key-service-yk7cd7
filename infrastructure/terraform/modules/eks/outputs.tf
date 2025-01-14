# EKS cluster endpoint for API access
output "cluster_endpoint" {
  description = "Endpoint URL for the EKS cluster API server used for kubectl and application integration"
  value       = aws_eks_cluster.main.endpoint
  sensitive   = false
}

# EKS cluster name for resource identification
output "cluster_name" {
  description = "Name identifier of the EKS cluster for AWS resource tagging and kubectl context"
  value       = aws_eks_cluster.main.name
  sensitive   = false
}

# Security group ID for network access control
output "cluster_security_group_id" {
  description = "Security group ID attached to the EKS cluster for network access control"
  value       = aws_eks_cluster.main.vpc_config[0].cluster_security_group_id
  sensitive   = false
}

# IAM role ARN for cluster permissions
output "cluster_iam_role_arn" {
  description = "IAM role ARN used by the EKS cluster for AWS service permissions"
  value       = aws_eks_cluster.main.role_arn
  sensitive   = false
}

# Node group configurations and status
output "node_groups" {
  description = "Map of node groups created including instance types, scaling configurations, and labels"
  value       = aws_eks_node_group.main
  sensitive   = false
}

# Certificate authority data for cluster authentication
output "cluster_certificate_authority_data" {
  description = "Base64 encoded certificate data required for cluster authentication and TLS verification"
  value       = aws_eks_cluster.main.certificate_authority[0].data
  sensitive   = true
}