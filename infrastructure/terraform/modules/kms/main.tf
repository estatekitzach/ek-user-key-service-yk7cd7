# AWS Provider configuration with version constraint
terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# Primary region KMS key configuration
resource "aws_kms_key" "primary" {
  description                        = "FIPS 140-2 compliant primary region KMS key for EstateKit Personal Information API"
  deletion_window_in_days           = var.key_deletion_window
  enable_key_rotation               = var.key_rotation_enabled
  is_enabled                        = true
  customer_master_key_spec          = "SYMMETRIC_DEFAULT"
  key_usage                         = "ENCRYPT_DECRYPT"
  multi_region                      = true
  bypass_policy_lockout_safety_check = false

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "Enable IAM User Permissions"
        Effect = "Allow"
        Principal = {
          AWS = "*"
        }
        Action = [
          "kms:*"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "aws:PrincipalOrgID": data.aws_organizations_organization.current.id
          }
        }
      },
      {
        Sid    = "Enable Key Rotation"
        Effect = "Allow"
        Principal = {
          AWS = "*"
        }
        Action = [
          "kms:EnableKeyRotation",
          "kms:RotateKey"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "aws:PrincipalOrgID": data.aws_organizations_organization.current.id
          }
        }
      }
    ]
  })

  tags = merge(
    var.tags,
    {
      Name        = "${var.project}-${var.environment}-primary"
      Region      = "primary"
      FIPS        = "compliant"
      Environment = var.environment
      Managed_By  = "terraform"
    }
  )
}

# DR region KMS key configuration
resource "aws_kms_key" "dr" {
  provider = aws.dr

  description                        = "FIPS 140-2 compliant DR region KMS key for EstateKit Personal Information API"
  deletion_window_in_days           = var.key_deletion_window
  enable_key_rotation               = var.key_rotation_enabled
  is_enabled                        = true
  customer_master_key_spec          = "SYMMETRIC_DEFAULT"
  key_usage                         = "ENCRYPT_DECRYPT"
  multi_region                      = true
  bypass_policy_lockout_safety_check = false

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "Enable IAM User Permissions"
        Effect = "Allow"
        Principal = {
          AWS = "*"
        }
        Action = [
          "kms:*"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "aws:PrincipalOrgID": data.aws_organizations_organization.current.id
          }
        }
      },
      {
        Sid    = "Enable Key Rotation"
        Effect = "Allow"
        Principal = {
          AWS = "*"
        }
        Action = [
          "kms:EnableKeyRotation",
          "kms:RotateKey"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "aws:PrincipalOrgID": data.aws_organizations_organization.current.id
          }
        }
      }
    ]
  })

  tags = merge(
    var.tags,
    {
      Name        = "${var.project}-${var.environment}-dr"
      Region      = "dr"
      FIPS        = "compliant"
      Environment = var.environment
      Managed_By  = "terraform"
    }
  )
}

# Primary region KMS alias
resource "aws_kms_alias" "primary" {
  name          = format("alias/%s-%s-primary", var.key_alias_prefix, var.environment)
  target_key_id = aws_kms_key.primary.key_id
}

# DR region KMS alias
resource "aws_kms_alias" "dr" {
  provider      = aws.dr
  name          = format("alias/%s-%s-dr", var.key_alias_prefix, var.environment)
  target_key_id = aws_kms_key.dr.key_id
}

# Data source for AWS Organizations
data "aws_organizations_organization" "current" {}

# Output definitions for key ARNs and aliases
output "primary_key_arn" {
  description = "ARN of the primary region KMS key"
  value       = aws_kms_key.primary.arn
}

output "dr_key_arn" {
  description = "ARN of the DR region KMS key"
  value       = aws_kms_key.dr.arn
}

output "primary_key_alias_arn" {
  description = "ARN of the primary region KMS key alias"
  value       = aws_kms_alias.primary.arn
}

output "dr_key_alias_arn" {
  description = "ARN of the DR region KMS key alias"
  value       = aws_kms_alias.dr.arn
}