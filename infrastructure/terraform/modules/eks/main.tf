# Configure required providers
terraform {
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

# Create KMS key for EKS cluster encryption
resource "aws_kms_key" "eks_encryption_key" {
  description             = "KMS key for EKS cluster encryption"
  deletion_window_in_days = 7
  enable_key_rotation    = true

  tags = {
    Name        = "${var.cluster_name}-encryption"
    Environment = "production"
    ManagedBy   = "terraform"
  }
}

# Create EKS cluster with enhanced security and monitoring
resource "aws_eks_cluster" "main" {
  name     = var.cluster_name
  version  = var.kubernetes_version
  role_arn = aws_iam_role.eks_cluster_role.arn

  vpc_config {
    subnet_ids              = var.private_subnet_ids
    endpoint_private_access = true
    endpoint_public_access  = false
    security_group_ids      = [aws_security_group.cluster_sg.id]
  }

  encryption_config {
    provider {
      key_arn = aws_kms_key.eks_encryption_key.arn
    }
    resources = ["secrets"]
  }

  enabled_cluster_log_types = [
    "api",
    "audit",
    "authenticator",
    "controllerManager",
    "scheduler"
  ]

  # Enable control plane logging to CloudWatch
  logging {
    cluster_logging {
      enabled_types = ["api", "audit", "authenticator", "controllerManager", "scheduler"]
    }
  }

  # Configure cluster add-ons
  addon_config {
    addon_name    = "vpc-cni"
    addon_version = "v1.12.0"
    resolve_conflicts = "OVERWRITE"
  }

  addon_config {
    addon_name    = "coredns"
    addon_version = "v1.9.3"
    resolve_conflicts = "OVERWRITE"
  }

  addon_config {
    addon_name    = "kube-proxy"
    addon_version = "v1.27.1"
    resolve_conflicts = "OVERWRITE"
  }

  tags = {
    Name        = var.cluster_name
    Environment = "production"
    ManagedBy   = "terraform"
  }

  depends_on = [
    aws_iam_role_policy_attachment.eks_cluster_policy
  ]
}

# Create managed node groups with auto-scaling
resource "aws_eks_node_group" "main" {
  for_each = var.node_group_config

  cluster_name    = aws_eks_cluster.main.name
  node_group_name = each.key
  node_role_arn   = aws_iam_role.eks_node_role.arn
  subnet_ids      = var.private_subnet_ids

  scaling_config {
    desired_size = each.value.desired_size
    min_size     = each.value.min_size
    max_size     = each.value.max_size
  }

  instance_types = each.value.instance_types

  # Configure node taints for workload isolation
  taint {
    key    = "dedicated"
    value  = each.key
    effect = "NO_SCHEDULE"
  }

  launch_template {
    id      = aws_launch_template.eks_nodes[each.key].id
    version = aws_launch_template.eks_nodes[each.key].latest_version
  }

  update_config {
    max_unavailable_percentage = 25
  }

  tags = {
    Name        = "${var.cluster_name}-${each.key}"
    Environment = "production"
    ManagedBy   = "terraform"
  }

  depends_on = [
    aws_iam_role_policy_attachment.eks_node_policy
  ]
}

# Create launch template for node groups
resource "aws_launch_template" "eks_nodes" {
  for_each = var.node_group_config

  name_prefix = "${var.cluster_name}-${each.key}"

  block_device_mappings {
    device_name = "/dev/xvda"

    ebs {
      volume_size           = each.value.disk_size
      volume_type          = "gp3"
      encrypted            = true
      kms_key_id          = aws_kms_key.eks_encryption_key.arn
      delete_on_termination = true
    }
  }

  monitoring {
    enabled = true
  }

  network_interfaces {
    associate_public_ip_address = false
    security_groups            = [aws_security_group.node_sg.id]
  }

  tag_specifications {
    resource_type = "instance"
    tags = {
      Name        = "${var.cluster_name}-${each.key}"
      Environment = "production"
      ManagedBy   = "terraform"
    }
  }

  user_data = base64encode(templatefile("${path.module}/templates/userdata.sh.tpl", {
    cluster_name = var.cluster_name
    node_group   = each.key
  }))
}

# Create cluster security group
resource "aws_security_group" "cluster_sg" {
  name_prefix = "${var.cluster_name}-cluster"
  vpc_id      = var.vpc_id

  ingress {
    from_port = 443
    to_port   = 443
    protocol  = "tcp"
    self      = true
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "${var.cluster_name}-cluster"
    Environment = "production"
    ManagedBy   = "terraform"
  }
}

# Create node security group
resource "aws_security_group" "node_sg" {
  name_prefix = "${var.cluster_name}-nodes"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 0
    to_port         = 0
    protocol        = "-1"
    security_groups = [aws_security_group.cluster_sg.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "${var.cluster_name}-nodes"
    Environment = "production"
    ManagedBy   = "terraform"
  }
}

# Create IAM roles and policies
resource "aws_iam_role" "eks_cluster_role" {
  name = "${var.cluster_name}-cluster-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "eks.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "eks_cluster_policy" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSClusterPolicy"
  role       = aws_iam_role.eks_cluster_role.name
}

resource "aws_iam_role" "eks_node_role" {
  name = "${var.cluster_name}-node-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "eks_node_policy" {
  for_each = toset([
    "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy",
    "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy",
    "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
  ])

  policy_arn = each.value
  role       = aws_iam_role.eks_node_role.name
}

# Output cluster information
output "cluster_endpoint" {
  description = "EKS cluster endpoint URL for API access"
  value       = aws_eks_cluster.main.endpoint
}

output "cluster_name" {
  description = "EKS cluster name"
  value       = aws_eks_cluster.main.name
}

output "cluster_security_group_id" {
  description = "Security group ID for the EKS cluster"
  value       = aws_security_group.cluster_sg.id
}

output "cluster_certificate_authority_data" {
  description = "Certificate authority data for client authentication"
  value       = aws_eks_cluster.main.certificate_authority[0].data
}