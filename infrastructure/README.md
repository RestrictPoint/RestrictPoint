# Infrastructure

All Azure resources are provisioned via Terraform per docs/17-Terraform-Infrastructure.md.

## Layout

```
modules/        Reusable Terraform modules (implemented in Phase 0-1)
environments/   Per-environment root configurations (dev, test, prod)
```

## Rules

- No manual resource creation in production.
- GitHub Actions authenticates via OIDC federated identity — no CI/CD secrets.
- State is stored in Azure Storage (`restrictpointtfstate`) with locking, one key per environment.
- Every module: Managed Identity first, private endpoints, diagnostic settings to Log Analytics.

## Modules (per docs/17)

networking, api-management, functions, sql, redis, servicebus, keyvault,
storage, frontdoor, monitoring, identity, marketplace

Module implementations land in Phase 0-1 of the Implementation Plan
once deployment credentials are available (see repository-root credentials.md).
