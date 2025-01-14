# Primary endpoint for Redis write operations
output "primary_endpoint" {
  description = "Primary endpoint address for Redis write operations in the replication group"
  value       = aws_elasticache_replication_group.redis.primary_endpoint_address
}

# Reader endpoint for scalable read operations
output "reader_endpoint" {
  description = "Reader endpoint address for Redis read operations, supporting read scaling"
  value       = aws_elasticache_replication_group.redis.reader_endpoint_address
}

# Port number for Redis connections
output "port" {
  description = "Port number for Redis connections, used for both read and write operations"
  value       = aws_elasticache_replication_group.redis.port
}

# Security group ID for network access control
output "security_group_id" {
  description = "ID of the security group controlling Redis network access and inbound rules"
  value       = aws_security_group.redis.id
}

# Full connection string for .NET Core integration
output "connection_string" {
  description = "Full Redis connection string for .NET Core 9 application configuration"
  value       = "redis://${aws_elasticache_replication_group.redis.primary_endpoint_address}:${aws_elasticache_replication_group.redis.port}"
}