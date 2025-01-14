# EstateKit Personal Information API

[![Build Status](https://github.com/estatekit/personal-info-api/workflows/ci/badge.svg)](https://github.com/estatekit/personal-info-api/actions)
[![Security Scan](https://github.com/estatekit/personal-info-api/workflows/security/badge.svg)](https://github.com/estatekit/personal-info-api/security)
[![FIPS 140-2](https://img.shields.io/badge/FIPS-140--2-blue.svg)](https://csrc.nist.gov/publications/detail/fips/140/2/final)
[![SOC 2](https://img.shields.io/badge/SOC-2-green.svg)](https://www.aicpa.org/soc2)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A secure encryption service designed to protect sensitive personal data through user-specific encryption keys. This system implements a separate encryption key management service ensuring encrypted data in the primary EstateKit database cannot be decrypted without proper authorization.

## Features

- 🔐 User-specific asymmetric key management via AWS KMS
- 🛡️ AES-256 data encryption with complete key isolation
- 🔄 Automated 90-day key rotation with data re-encryption
- ⚡ High-availability microservice architecture
- 📋 FIPS 140-2 compliant encryption implementation

## Architecture

- **API Layer**: .NET Core 9 microservice with clean architecture
- **Data Storage**: PostgreSQL 16 with encryption at rest
- **Caching**: Redis 7.2 with secure configuration
- **Key Management**: AWS KMS with hardware security modules
- **Authentication**: OAuth 2.0 via AWS Cognito
- **Infrastructure**: AWS EKS with multi-AZ deployment

## Prerequisites

- .NET Core 9.0 SDK
- Docker Desktop 24.0+
- AWS CLI 2.13+
- Terraform 1.5+
- kubectl with RBAC configuration

## Quick Start

1. **Configure AWS Credentials**
```bash
aws configure --profile estatekit-dev
```

2. **Deploy Infrastructure**
```bash
cd infrastructure/terraform
terraform init
terraform apply
```

3. **Deploy Application**
```bash
kubectl apply -f infrastructure/kubernetes/
```

4. **Verify Deployment**
```bash
kubectl get pods -n estatekit
```

## Security Features

### Encryption
- Asymmetric key generation via AWS KMS
- AES-256 encryption for data at rest
- TLS 1.3 for data in transit
- Secure key rotation every 90 days

### Authentication & Authorization
- OAuth 2.0 with AWS Cognito
- Role-based access control (RBAC)
- JWT token validation
- Multi-factor authentication support

### Compliance
- FIPS 140-2 certified encryption modules
- SOC 2 Type II certified infrastructure
- PCI DSS compliant data handling
- GDPR compliant data protection

## API Documentation

### Key Management
```http
POST /api/v1/keys
Content-Type: application/json
Authorization: Bearer <token>

{
    "userId": "string"
}
```

### Data Encryption
```http
POST /api/v1/encrypt
Content-Type: application/json
Authorization: Bearer <token>

{
    "userId": "string",
    "data": ["string"]
}
```

### Data Decryption
```http
POST /api/v1/decrypt
Content-Type: application/json
Authorization: Bearer <token>

{
    "userId": "string",
    "data": ["string"]
}
```

## Infrastructure

### AWS Services
- EKS for container orchestration
- RDS PostgreSQL for data storage
- ElastiCache Redis for caching
- KMS for key management
- Cognito for authentication
- CloudWatch for monitoring

### High Availability
- Multi-AZ deployment
- Automated failover
- Load balancing
- Auto-scaling
- Disaster recovery

## Development

### Local Setup
1. Install required tools
2. Clone repository
3. Configure AWS credentials
4. Run infrastructure locally
5. Start development server

### Security Guidelines
- Follow secure coding practices
- Implement input validation
- Use approved encryption methods
- Follow least privilege principle
- Enable audit logging

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our security-focused contribution process.

### Security Requirements
- Code security scanning
- Dependency vulnerability checks
- Infrastructure security review
- Compliance validation
- Security documentation

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- Security Issues: security@estatekit.com
- Technical Support: support@estatekit.com
- Documentation: docs.estatekit.com

## Acknowledgments

- AWS Security Team
- NIST Cryptographic Module Team
- Open Source Security Community