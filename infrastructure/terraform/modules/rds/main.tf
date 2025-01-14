# Configure AWS provider with version constraint
terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# IAM role for enhanced monitoring
resource "aws_iam_role" "rds_monitoring" {
  name = "${var.environment}-estatekit-rds-monitoring"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "monitoring.rds.amazonaws.com"
        }
      }
    ]
  })

  tags = {
    Environment = var.environment
    Service     = "EstateKit"
    ManagedBy   = "Terraform"
  }
}

# Attach enhanced monitoring policy to IAM role
resource "aws_iam_role_policy_attachment" "rds_monitoring" {
  role       = aws_iam_role.rds_monitoring.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"
}

# RDS subnet group for multi-AZ deployment
resource "aws_db_subnet_group" "this" {
  name        = "${var.environment}-estatekit-rds"
  description = "Subnet group for EstateKit RDS instances"
  subnet_ids  = var.database_subnet_ids

  tags = {
    Environment = var.environment
    Service     = "EstateKit"
    ManagedBy   = "Terraform"
  }
}

# RDS parameter group for PostgreSQL optimization and security
resource "aws_db_parameter_group" "this" {
  name        = "${var.environment}-estatekit-pg16"
  family      = "postgres16"
  description = "Custom parameter group for EstateKit PostgreSQL 16"

  parameter {
    name  = "ssl"
    value = "1"
  }

  parameter {
    name  = "rds.force_ssl"
    value = "1"
  }

  parameter {
    name  = "log_connections"
    value = "1"
  }

  parameter {
    name  = "log_disconnections"
    value = "1"
  }

  parameter {
    name  = "log_statement"
    value = "all"
  }

  parameter {
    name  = "log_min_duration_statement"
    value = "1000"  # Log queries taking more than 1 second
  }

  parameter {
    name  = "shared_preload_libraries"
    value = "pg_stat_statements"  # Enable query performance monitoring
  }

  tags = {
    Environment = var.environment
    Service     = "EstateKit"
    ManagedBy   = "Terraform"
  }
}

# Primary RDS instance
resource "aws_db_instance" "this" {
  identifier     = "${var.environment}-estatekit-db"
  engine         = "postgres"
  engine_version = "16"
  
  instance_class        = var.db_instance_class
  allocated_storage     = 100
  max_allocated_storage = 1000
  storage_type          = "gp3"
  
  db_name  = var.db_name
  username = "estatekit_admin"
  password = var.db_password

  multi_az               = var.multi_az
  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [var.rds_security_group_id]
  parameter_group_name   = aws_db_parameter_group.this.name
  
  storage_encrypted = true
  kms_key_id       = var.kms_key_arn

  backup_retention_period = var.backup_retention_period
  backup_window          = "03:00-04:00"
  maintenance_window     = "Mon:04:00-Mon:05:00"

  auto_minor_version_upgrade = true
  deletion_protection       = var.deletion_protection
  skip_final_snapshot      = false
  final_snapshot_identifier = "${var.environment}-estatekit-final-snapshot"

  performance_insights_enabled          = true
  performance_insights_retention_period = 7
  monitoring_interval                   = 60
  monitoring_role_arn                  = aws_iam_role.rds_monitoring.arn

  enabled_cloudwatch_logs_exports = [
    "postgresql",
    "upgrade"
  ]

  copy_tags_to_snapshot = true

  tags = {
    Environment = var.environment
    Service     = "EstateKit"
    ManagedBy   = "Terraform"
    Compliance  = "GDPR,SOC2,PCIDSS"
  }

  lifecycle {
    prevent_destroy = true
  }
}

# CloudWatch alarms for RDS monitoring
resource "aws_cloudwatch_metric_alarm" "database_cpu" {
  alarm_name          = "${var.environment}-estatekit-db-cpu-utilization"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = "2"
  metric_name        = "CPUUtilization"
  namespace          = "AWS/RDS"
  period             = "300"
  statistic          = "Average"
  threshold          = "80"
  alarm_description  = "This metric monitors RDS CPU utilization"
  alarm_actions      = var.alarm_sns_topic_arn != "" ? [var.alarm_sns_topic_arn] : []

  dimensions = {
    DBInstanceIdentifier = aws_db_instance.this.id
  }
}

resource "aws_cloudwatch_metric_alarm" "database_memory" {
  alarm_name          = "${var.environment}-estatekit-db-freeable-memory"
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = "2"
  metric_name        = "FreeableMemory"
  namespace          = "AWS/RDS"
  period             = "300"
  statistic          = "Average"
  threshold          = "1000000000" # 1GB in bytes
  alarm_description  = "This metric monitors RDS freeable memory"
  alarm_actions      = var.alarm_sns_topic_arn != "" ? [var.alarm_sns_topic_arn] : []

  dimensions = {
    DBInstanceIdentifier = aws_db_instance.this.id
  }
}

resource "aws_cloudwatch_metric_alarm" "database_storage" {
  alarm_name          = "${var.environment}-estatekit-db-free-storage"
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = "2"
  metric_name        = "FreeStorageSpace"
  namespace          = "AWS/RDS"
  period             = "300"
  statistic          = "Average"
  threshold          = "10000000000" # 10GB in bytes
  alarm_description  = "This metric monitors RDS free storage space"
  alarm_actions      = var.alarm_sns_topic_arn != "" ? [var.alarm_sns_topic_arn] : []

  dimensions = {
    DBInstanceIdentifier = aws_db_instance.this.id
  }
}

# Outputs for other modules
output "rds_endpoint" {
  description = "The connection endpoint for the RDS instance"
  value       = aws_db_instance.this.endpoint
  sensitive   = false
}

output "rds_arn" {
  description = "The ARN of the RDS instance"
  value       = aws_db_instance.this.arn
  sensitive   = false
}

output "rds_id" {
  description = "The ID of the RDS instance"
  value       = aws_db_instance.this.id
  sensitive   = false
}