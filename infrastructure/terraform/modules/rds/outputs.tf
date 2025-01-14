# RDS instance connection endpoint
output "db_instance_endpoint" {
  description = "Connection endpoint for the RDS instance combining hostname and port"
  value       = aws_db_instance.this.endpoint
  sensitive   = false
}

# RDS instance hostname
output "db_instance_address" {
  description = "Hostname of the RDS instance for network access"
  value       = aws_db_instance.this.address
  sensitive   = false
}

# RDS instance port
output "db_instance_port" {
  description = "Port number of the RDS instance for connection configuration"
  value       = aws_db_instance.this.port
  sensitive   = false
}

# RDS instance ARN
output "db_instance_arn" {
  description = "ARN of the RDS instance for AWS resource management"
  value       = aws_db_instance.this.arn
  sensitive   = false
}

# RDS instance ID
output "db_instance_id" {
  description = "Unique identifier of the RDS instance"
  value       = aws_db_instance.this.id
  sensitive   = false
}