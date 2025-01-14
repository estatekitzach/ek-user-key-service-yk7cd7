# Provider configuration for EstateKit Personal Information API infrastructure
# Version: 1.0
# Required Terraform version and provider versions
terraform {
  required_version = ">= 1.0.0"
  
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.0"
    }
  }
}

# Primary AWS provider configuration for main region (us-east-1)
provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project             = "EstateKit"
      Environment         = terraform.workspace
      ManagedBy          = "Terraform"
      Service            = "PersonalInformationAPI"
      SecurityLevel      = "High"
      ComplianceRequired = "True"
    }
  }
}

# DR region AWS provider configuration (us-west-2)
provider "aws" {
  alias  = "dr"
  region = "us-west-2"

  default_tags {
    tags = {
      Project             = "EstateKit"
      Environment         = "${terraform.workspace}-dr"
      ManagedBy          = "Terraform"
      Service            = "PersonalInformationAPI"
      SecurityLevel      = "High"
      ComplianceRequired = "True"
    }
  }
}

# Kubernetes provider configuration for EKS cluster management
provider "kubernetes" {
  host                   = module.eks.cluster_endpoint
  cluster_ca_certificate = base64decode(module.eks.cluster_ca_certificate)
  token                  = module.eks.cluster_token

  exec {
    api_version = "client.authentication.k8s.io/v1beta1"
    command     = "aws"
    args = [
      "eks",
      "get-token",
      "--cluster-name",
      module.eks.cluster_name
    ]
  }
}