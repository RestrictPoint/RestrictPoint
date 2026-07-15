# Terraform Modules

Reusable modules per docs/17-Terraform-Infrastructure.md:

| Module | Purpose |
|--------|---------|
| networking | VNet, subnets, private DNS, NSGs, private endpoints |
| api-management | APIM Premium, JWT validation, rate limiting |
| functions | Function App hosting, Managed Identity, VNet integration |
| sql | Azure SQL, RLS, auditing, geo-backup |
| redis | Redis Cache for license/identity projections |
| servicebus | Premium namespace, topics, subscriptions |
| keyvault | Key Vault, RBAC, ES256 (EC P-256) signing keys |
| storage | Storage accounts (tfstate, blobs) |
| frontdoor | Front Door, WAF |
| monitoring | Log Analytics, Application Insights, alerts |
| identity | Managed Identities, federated credentials, role assignments |
| marketplace | Marketplace-specific resources (search, CDN) |

Implemented during Implementation Plan Phase 0-1.
