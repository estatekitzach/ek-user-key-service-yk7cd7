# Pull Request Description

## Description
<!-- Provide a detailed description of your changes -->

**Summary of changes:**
- 

**Related issue number:** 
- 

**Type of change:**
- [ ] Feature
- [ ] Bug fix
- [ ] Security enhancement
- [ ] Performance improvement
- [ ] Infrastructure change
- [ ] Documentation update

**Impact assessment:**
- 

**Breaking changes:**
- [ ] Yes (describe below)
- [ ] No

## Security Checklist
<!-- All security-related changes require review from @estatekit/security-team -->
- [ ] AWS KMS integration verified
- [ ] Encryption implementation follows FIPS 140-2 compliance
- [ ] Key rotation mechanism validated
- [ ] Access control and RBAC implementation verified
- [ ] Data protection assessment completed (GDPR compliance)
- [ ] Security scan results reviewed (OWASP/SonarQube)
- [ ] Sensitive data handling reviewed
- [ ] API authentication verified
- [ ] Network security validated
- [ ] Audit logging implementation verified

## Quality Checklist
- [ ] Unit test coverage meets minimum 80% requirement
- [ ] Integration tests added and passing
- [ ] Performance impact assessed
- [ ] Code quality metrics reviewed (SonarQube)
- [ ] API documentation updated
- [ ] Technical documentation updated
- [ ] Error handling implemented
- [ ] Logging implemented
- [ ] Circuit breaker configured
- [ ] Resource cleanup verified

## Infrastructure Changes
<!-- Required if infrastructure changes are included -->
- [ ] AWS resource modifications documented
- [ ] EKS configuration updates verified
- [ ] Database schema changes reviewed
- [ ] Cache configuration updates validated
- [ ] Environment variables changes documented
- [ ] Deployment impact assessed
- [ ] Scaling configuration reviewed
- [ ] Backup/recovery implications considered
- [ ] Multi-region impact evaluated
- [ ] Cost impact assessed

## API Changes
<!-- Required if API changes are included -->
- [ ] New/modified endpoints documented
- [ ] Request/response schema changes validated
- [ ] Backward compatibility verified
- [ ] API documentation updated
- [ ] Rate limiting adjustments reviewed
- [ ] Error response updates documented
- [ ] Authentication changes verified
- [ ] Performance implications assessed
- [ ] API versioning impact evaluated
- [ ] Client notification requirements identified

## Testing Instructions
**Environment requirements:**
- 

**Test data setup:**
- 

**Test execution steps:**
1. 
2. 
3. 

**Expected results:**
- 

**Verification checklist:**
- [ ] Performance test results reviewed
- [ ] Security test results validated
- [ ] Integration test coverage verified
- [ ] Rollback procedure documented
- [ ] Monitoring requirements specified
- [ ] Post-deployment verification steps documented

## Review Requirements
<!-- Minimum review requirements based on change type -->

**Security changes:**
- [ ] 2 approvals from @estatekit/security-team required
- [ ] Security scan passed
- [ ] KMS integration test passed
- [ ] Encryption validation completed

**Infrastructure changes:**
- [ ] 2 approvals from @estatekit/infrastructure-team required
- [ ] Terraform plan reviewed
- [ ] Resource validation completed
- [ ] Cost assessment approved

**API changes:**
- [ ] 1 approval from @estatekit/api-team required
- [ ] API compatibility verified
- [ ] Schema validation passed
- [ ] Documentation updates completed

---
<!-- Add any additional notes or context below -->