# Primary region KMS key outputs
output "primary_key_arn" {
  description = "ARN of the primary region KMS key for encryption operations"
  value       = aws_kms_key.primary.arn
}

output "primary_key_id" {
  description = "ID of the primary region KMS key for encryption operations"
  value       = aws_kms_key.primary.key_id
}

output "primary_key_alias_arn" {
  description = "ARN of the primary region KMS key alias"
  value       = aws_kms_alias.primary.arn
}

output "primary_key_alias_name" {
  description = "Name of the primary region KMS key alias"
  value       = aws_kms_alias.primary.name
}

# DR region KMS key outputs
output "dr_key_arn" {
  description = "ARN of the DR region KMS key for encryption operations"
  value       = aws_kms_key.dr.arn
}

output "dr_key_id" {
  description = "ID of the DR region KMS key for encryption operations"
  value       = aws_kms_key.dr.key_id
}

output "dr_key_alias_arn" {
  description = "ARN of the DR region KMS key alias"
  value       = aws_kms_alias.dr.arn
}

output "dr_key_alias_name" {
  description = "Name of the DR region KMS key alias"
  value       = aws_kms_alias.dr.name
}