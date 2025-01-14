# Backend configuration for EstateKit Terraform state management
# Version: 5.0
# Purpose: Configures secure and scalable state storage using AWS S3 with DynamoDB locking

terraform {
  backend "s3" {
    # Primary state storage configuration
    bucket = "estatekit-terraform-state"
    key    = "infrastructure/${var.environment}/terraform.tfstate"
    region = "us-east-1"
    
    # Enhanced security settings
    encrypt        = true
    acl            = "private"
    force_path_style = false
    
    # KMS encryption configuration
    kms_key_id     = "arn:aws:kms:us-east-1:ACCOUNT_ID:key/estatekit-terraform-state"
    
    # State locking configuration
    dynamodb_table = "estatekit-terraform-locks"
    dynamodb_endpoint = "dynamodb.us-east-1.amazonaws.com"
    
    # Workspace management
    workspace_key_prefix = "workspace"
    
    # Version control
    versioning = true
    
    # Authentication and authorization
    role_arn = "arn:aws:iam::ACCOUNT_ID:role/EstateKitTerraformStateRole"
    profile  = "estatekit-terraform"
    
    # Validation settings
    skip_credentials_validation = false
    skip_region_validation     = false
    skip_metadata_api_check    = false
  }
}