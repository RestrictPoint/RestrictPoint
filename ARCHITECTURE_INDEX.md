# ARCHITECTURE_INDEX.md

**AI Working Memory — compact, always-current map of the RestrictPoint system.**

> Regenerate this file whenever the architecture changes (new table, endpoint, event, contract, or dependency).
> Sources of truth: docs/02 (Domain), docs/05 (Structure), docs/09 (Database), docs/16 (API), docs/17 (Terraform), docs/20 (Events).
> Rules: one publisher per event; only the owning context writes its schema; cross-context communication is event-driven; every table carries Id/TenantId/CreatedUtc/UpdatedUtc/DeletedUtc/RowVersion; every context has an OutboxEvents table.

---

## 🏗️ Phase 0 Infrastructure — DEPLOYED (2026-07-15)

    GitHub Repository:
        Org: RestrictPoint
        Repo: RestrictPoint
        URL: https://github.com/RestrictPoint/RestrictPoint
        Main branch: main

    Resource Group Strategy (restructured 2026-07-16):
        RestrictPoint-Shared (eastus): cross-environment resources only
            - DNS zone restrictpoint.com
            - Terraform state storage rpdevtfstate5507
            - Entra External ID tenant link (restrictpointext.onmicrosoft.com)
        RestrictPoint-Dev (eastus): all dev environment resources (managed by Terraform)

    Azure Resources (bootstrap):
        Storage Account: rpdevtfstate5507 (RestrictPoint-Shared)
            Container: tfstate (Terraform state backend)
            Versioning: enabled
            Soft delete: 30 days
            RBAC: SP has Storage Blob Data Contributor
        
        Key Vault: rp-dev-kv-301106 (RestrictPoint-Dev)
            URI: https://rp-dev-kv-301106.vault.azure.net/
            RBAC: enabled (no access policies)
            Soft delete: 30 days
            Purge protection: disabled (dev only)
        
        Log Analytics: rp-dev-logs (RestrictPoint-Dev)
            Retention: 30 days
            Daily quota: 1 GB (free tier cap)
        
        Budget: RestrictPoint-Dev
            Amount: $150/month (MPN credit cap)
            Alerts: $50 (33%), $100 (67%), $140 (93%)
            Email: rajiv@restrictpoint.com

    DNS:
        Zone: restrictpoint.com (RestrictPoint-Shared RG)
        Delegation: live (Azure DNS nameservers)
        Planned subdomains: portal, admin, marketplace, docs, api

    GitHub Actions:
        OIDC Federation: configured (main branch + pull requests)
        Variables: AZURE_CLIENT_ID, AZURE_SUBSCRIPTION_ID, AZURE_TENANT_ID
        Terraform workflow: .github/workflows/terraform.yml
            Triggers: push to main, PRs (infrastructure/** paths)
            Plan on PR, apply on main push

    Service Principal:
        App ID: 1c11f175-489a-4765-8584-d8ce517e0a3e
        Multi-tenant: enabled
        Roles: Contributor, User Access Administrator, Key Vault Administrator
        Graph API: Application.ReadWrite.All (both tenants)
        Federated credentials: github-actions-main, github-actions-pr

    Entra External ID:
        Tenant: Restrict Point Customers (66162195-1310-42eb-86e5-94dbef0e027c)
        Pre-created app: Restrict Point Portal (c607b690-8e90-4e29-8c9a-03960055e726)
        Domain: restrictpointext.ciamlogin.com (default)

    Next Phase: Phase 1 — Core Infrastructure (VNet/firewalls, APIM, Service Bus, Redis, SQL, App Config, Front Door)

---

## 🏗️ Phase 1 Infrastructure — DEPLOYED (2026-07-15)

    API Management:
        Name: rp-dev-apim
        SKU: Consumption_0 (dev)
        Gateway URL: https://rp-dev-apim.azure-api.net
        Developer Portal: https://rp-dev-apim.developer.azure-api.net
        Managed Identity: 18982e83-10db-4052-86d8-0026aa984adc
        Features: System-assigned identity, JWT validation ready, rate limiting
    
    Service Bus:
        Name: rp-dev-servicebus
        SKU: Standard
        Endpoint: https://rp-dev-servicebus.servicebus.windows.net:443/
        Authentication: Managed Identity only (local auth disabled)
        Topics (8): OrganizationEvents, ProjectEvents, LicenseEvents, BillingEvents, MarketplaceEvents, NotificationEvents, AnalyticsEvents, AuditEvents
        Queues (7): Email, Webhook, InvoiceGeneration, UsageAggregation, Cleanup, Retry, DeadLetterProcessing
    
    Redis Cache:
        Name: rp-dev-redis-301106
        SKU: Basic C0 (250MB)
        Endpoint: rp-dev-redis-301106.redis.cache.windows.net:6380 (SSL)
        Location: eastus
        Features: TLS 1.2, maxmemory-policy=volatile-lru
    
    Azure SQL:
        Server: rp-dev-sql-301106
        FQDN: rp-dev-sql-301106.database.windows.net
        Location: centralus (eastus/eastus2/westus2 provisioning restricted for MPN subscriptions)
        Database: RestrictPoint (32GB, GP_S_Gen5_1 serverless)
        SKU: Serverless (0.5-1 vCore, auto-pause 60min)
        Authentication: Entra ID only (no SQL auth)
        Admin: RestrictPoint Service Principal (0fdf5874-f3ed-425e-a267-d8c8218d2444)
        Features: TLS 1.2, TDE enabled, backup retention 7 days
    
    App Configuration:
        Name: rp-dev-appconfig
        SKU: Free
        Endpoint: https://rp-dev-appconfig.azconfig.io
        Authentication: Managed Identity only (local auth disabled)
        Features: Feature flags, soft delete 7 days
    
    Front Door:
        Name: rp-dev-fd
        SKU: Standard_AzureFrontDoor
        Endpoint: placeholder-bghmfff3hsfnhqcy.z01.azurefd.net (placeholder until backends exist)
        WAF Policy: rpdevfdwaf (Prevention mode)
        Features: Custom block response, rate limiting ready
    
    Terraform State:
        Backend: azurerm (rpdevtfstate5507)
        State file: tfstate/dev.tfstate
        Lock: Azure Storage blob lease
        Modules: apim, servicebus, redis, sql, appconfig, frontdoor
    
    Dev Environment Notes:
        Total services: 6 core infrastructure components
        Estimated monthly cost: ~$72-80 (within $150 MPN budget)
        All dev resources in RestrictPoint-Dev RG (Shared holds only DNS/tfstate/Entra)
        Public endpoints enabled (dev only; prod will use private endpoints)
        SQL placed in centralus due to MPN regional restrictions

    Next Phase: Phase 3 — Identity System Implementation (business logic, Entra integration, JWT validation)

---

## 🚀 Phase 2 Function Apps — DEPLOYED (2026-07-15)

    Overview:
        6 Function Apps deployed with System-assigned Managed Identity
        Consumption plan (Y1) for serverless execution
        Location: centralus (Y1 quota unavailable in eastus for MPN subscriptions)
        All apps linked to Application Insights + Log Analytics
        Diagnostic settings configured for all resources
        RBAC role assignments configured for Managed Identity access

    Identity Function App:
        Name: rp-dev-func-identity
        Hostname: rp-dev-func-identity.azurewebsites.net
        Managed Identity: 7ae58462-9272-44b8-b625-efd3d1aa719e
        Storage: rpdevstidentity
        App Insights: rp-dev-ai-identity
        Service Plan: rp-dev-plan-identity (Y1 Consumption)
        RBAC: Key Vault Secrets User, SQL DB Contributor, Service Bus Sender/Receiver, App Config Reader, Redis Contributor

    Licensing Function App:
        Name: rp-dev-func-licensing
        Hostname: rp-dev-func-licensing.azurewebsites.net
        Managed Identity: 4db6cdef-ae44-4c8a-81ba-ba6284f9efe8
        Storage: rpdevstlicensing
        App Insights: rp-dev-ai-licensing
        Service Plan: rp-dev-plan-licensing (Y1 Consumption)
        RBAC: Key Vault Secrets User, Key Vault Crypto User (for ES256 signing), SQL DB Contributor, Service Bus Sender/Receiver, App Config Reader, Redis Contributor

    Billing Function App:
        Name: rp-dev-func-billing
        Hostname: rp-dev-func-billing.azurewebsites.net
        Managed Identity: cdc19888-270c-491f-987b-10989db502d8
        Storage: rpdevstbilling
        App Insights: rp-dev-ai-billing
        Service Plan: rp-dev-plan-billing (Y1 Consumption)
        RBAC: Key Vault Secrets User, SQL DB Contributor, Service Bus Sender/Receiver, App Config Reader

    Marketplace Function App:
        Name: rp-dev-func-marketplace
        Hostname: rp-dev-func-marketplace.azurewebsites.net
        Managed Identity: 684e023b-bf54-4b5d-ac49-3d319ac76960
        Storage: rpdevstmarketplace
        App Insights: rp-dev-ai-marketplace
        Service Plan: rp-dev-plan-marketplace (Y1 Consumption)
        RBAC: Key Vault Secrets User, SQL DB Contributor, Service Bus Sender/Receiver, App Config Reader

    Notifications Function App:
        Name: rp-dev-func-notifications
        Hostname: rp-dev-func-notifications.azurewebsites.net
        Managed Identity: b865f45e-c7ae-4767-8a47-de7315de1039
        Storage: rpdevstnotifications
        App Insights: rp-dev-ai-notifications
        Service Plan: rp-dev-plan-notifications (Y1 Consumption)
        RBAC: Key Vault Secrets User, SQL DB Contributor, Service Bus Sender/Receiver, App Config Reader

    Analytics Function App:
        Name: rp-dev-func-analytics
        Hostname: rp-dev-func-analytics.azurewebsites.net
        Managed Identity: b1a10749-6746-424b-9544-0925aaea778c
        Storage: rpdevstanalytics
        App Insights: rp-dev-ai-analytics
        Service Plan: rp-dev-plan-analytics (Y1 Consumption)
        RBAC: Key Vault Secrets User, SQL DB Contributor, Service Bus Sender/Receiver, App Config Reader

    Terraform State:
        Modules added: functionapp, rbac
        Resources added: 69 (6 Function Apps + storage + App Insights + service plans + diagnostic settings + RBAC assignments)
    
    Phase 2 Notes:
        Flex Consumption (FC1) attempted but failed (requires function_app_config block not yet supported in Terraform provider)
        Switched to traditional Consumption (Y1) plan
        Y1 quota unavailable in eastus for MPN subscriptions, deployed to centralus
        All Function Apps have System-assigned Managed Identity configured
        No connection strings used - all access via Managed Identity
        Diagnostic settings enabled for Function Apps and Application Insights
        Total cost addition: ~$0-5/month (Consumption plan only charges for execution)

    Next Phase: Phase 3 — Identity System Implementation (Entra integration, JWT validation, user/org management)

---

## Identity

    App: apps/api-identity (RestrictPoint.Api.Identity)
    Service Bus Topics: IdentityEvents, OrganizationEvents (only Identity publishes these)

    ✅ PHASE 3 IMPLEMENTED (2026-07-16):

    Architecture:
        Clean Architecture: Functions → Application (vertical slices) → Domain → Infrastructure
        Shared packages: RestrictPoint.Common (Result/Error), RestrictPoint.Auth (JWT middleware),
            RestrictPoint.Messaging (event envelope + SB publisher), RestrictPoint.Database (BaseEntity/outbox/auditing)
        Authentication: Entra External ID JWTs validated via OIDC discovery (RS256, key rollover safe)
        Authorization: policy-based (Policies.CanManageMembers etc.), roles resolved from DB not token claims
        Events: transactional outbox (Identity.OutboxMessages) → timer dispatcher (30s) → Service Bus
        Caching: Redis user-context cache (10 min TTL, Entra auth, silent DB fallback)
        JIT provisioning: GET /me creates the user on first authenticated call → UserRegistered

    Entra External ID app registrations (tenant 66162195-1310-42eb-86e5-94dbef0e027c):
        RestrictPoint API: appId 13db69ee-e73b-45c6-a7e3-5f08b194094d (audience for all API JWTs)
            Scope: api://13db69ee-e73b-45c6-a7e3-5f08b194094d/access_as_user (v2 tokens)
        Restrict Point Portal (SPA): c607b690-8e90-4e29-8c9a-03960055e726 (wired to API scope;
            admin consent pending — needs one-time grant in admin center)

    Database (SQL schemas Identity + Organizations, EF Core migration InitialIdentitySchema applied):
        Identity.Users (JIT-provisioned, unique ExternalProvider+ExternalId)
        Identity.UserOrganizations (memberships: role + status, unique UserId+OrganizationId)
        Identity.OutboxMessages (transactional outbox)
        Organizations.Organizations (name, unique slug, status, billing email)
        Organizations.Invitations (SHA-256 token hash only, 7-day expiry)
        All tables: soft delete + rowversion + audit timestamps (AuditingSaveChangesInterceptor)
        MI DB user: rp-dev-func-identity (db_datareader + db_datawriter, created WITH SID)

    APIs (implemented):
        GET  /v1/identity/me                          → MeResponse (JIT provision, cached)
        GET  /v1/identity/organizations               → membership list
        POST /v1/identity/organizations               → create org, caller becomes Owner (201)
        POST /v1/identity/organizations/{id}/invite   → invite member, CanManageMembers policy (201)
        GET  /health/live, /health/ready              → anonymous probes

    Events (implemented):
        UserRegistered v1.0 → IdentityEvents
        UserInvited v1.0 → IdentityEvents
        OrganizationCreated v1.0 → OrganizationEvents
    Events (defined in catalog, later phases):
        UserAuthenticated
        UserProfileUpdated
        UserInvitationAccepted
        UserRemoved
        UserRoleChanged

    Functions (HTTP triggers + DispatchOutbox timer):
        GetMe
        ListOrganizations
        CreateOrganization
        InviteMember
        DispatchOutbox (outbox → Service Bus every 30s)
        HealthLive / HealthReady

    Dependencies:
        Entra External ID (authentication)
        Azure SQL (Identity + Organizations schemas, Managed Identity auth)
        Service Bus (IdentityEvents/OrganizationEvents topics, Managed Identity)
        Redis (user context cache, Entra token auth + access policy)

    Tests: tests/identity (53 passing — domain, handlers via SQLite, Result contracts)

    Shared Contracts:
        UserDto
        JWT claims: sub, tid, oid, roles

---

## Organizations

    App: apps/api-identity (owned/published by Identity Service)
    Service Bus Topic: organization

    Database (Organizations schema):
        Organizations
        Invitations
        OutboxEvents

    APIs:
        GET  /v1/org
        PUT  /v1/org
        GET  /v1/org/members
        POST /v1/org/members/{id}/role

    Events (published):
        OrganizationCreated          (ordering required; initializes downstream services)
        OrganizationUpdated
        OrganizationSuspended
        OrganizationReactivated
        OrganizationDeleted
        OrganizationOwnershipTransferred
        OrganizationSettingsUpdated

    Functions:
        CreateOrganization
        UpdateOrganization
        SuspendOrganization
        TransferOwnership
        ManageMembers

    Dependencies:
        Identity

    Shared Contracts:
        OrganizationDto
        Roles: Owner, Admin, Developer, Billing, Support, ReadOnly

---

## Projects

    App: apps/api-identity or dedicated slice (publisher: Project Service)
    Service Bus Topic: project

    Database (Projects schema):
        Projects
        ProjectApiKeys
        ProjectSettings
        OutboxEvents

    APIs:
        GET    /v1/projects
        POST   /v1/projects
        GET    /v1/projects/{id}
        PUT    /v1/projects/{id}
        DELETE /v1/projects/{id}          (soft delete)
        POST   /v1/projects/{id}/apikeys

    Events (published):
        ProjectCreated
        ProjectUpdated
        ProjectConfigured
        ProjectApiKeyGenerated
        ProjectArchived
        ProjectDeleted

    Functions:
        CreateProject
        UpdateProject
        ConfigureProject
        GenerateApiKey
        ArchiveProject

    Dependencies:
        Organizations
        Identity

    Shared Contracts:
        ProjectDto
        ApiKeyDto (public key only; private key hashed)

---

## Licensing

    App: apps/api-licensing (RestrictPoint.Api.Licensing)
    Service Bus Topic: LicenseEvents
    Hot path: license validation <50ms, no multi-table joins, Redis-first

    ✅ PHASE 4 IMPLEMENTED (2026-07-16):

    Cryptography:
        Signing: ES256 (ECDSA P-256) via Key Vault sign op (key "license-signing",
            rp-dev-kv-301106, auto-rotation P180D, private key never leaves vault)
        Token format: JWS compact — header {alg:ES256, typ:JWT, kid:<KV key version>}
        Verification: cached public keys (KeyClient, per-version immutable cache);
            hot path never round-trips to Key Vault
        Hardening tested: payload tampering, signature tampering, alg substitution
            (alg=none forgery), unknown kid, malformed tokens

    Validation pipeline (POST /v1/licenses/validate — anonymous; the signed token IS
    the credential):
        1. Replay protection: timestamp ±5 min + Redis nonce dedupe (SETNX, 10 min window;
           degrades open on Redis outage — availability over replay hardening)
        2. ES256 signature verification
        3. Binding: tenant + project + webPart GUID must all match payload
        4. State: Redis license cache (12h TTL) → SQL fallback; revoked/suspended/expired
           return 200 {isValid:false, status}
        5. Installation tracking: first contact = activation (LicenseActivated event)
        6. Async events on every outcome via outbox (SDK never waits)

    Database (Licensing schema, migration InitialLicensingSchema applied):
        Licenses (type, status, dev/customer org, customer tenant, subscription link, version)
        LicenseFeatures / LicenseLimits / LicenseWebParts (payload source of truth)
        LicenseTokens (jti + KV key version; per-token revocation)
        Installations (unique LicenseId+InstallationId, LastValidatedUtc)
        OutboxMessages
        MI DB user: rp-dev-func-licensing (db_datareader + db_datawriter, WITH SID)

    APIs (implemented):
        POST /v1/licenses/validate  → anonymous, replay-protected (SDK critical path)
        POST /v1/licenses/issue     → 201, roles Owner/Admin/Developer in dev org
        POST /v1/licenses/revoke    → roles Owner/Admin; tokens revoked + cache invalidated
        GET  /v1/licenses?organizationId=&projectId=  → member of dev org
        GET  /v1/licenses/{id}      → member of dev org (404 for non-members)

    Cross-service authorization:
        IdentityOrganizationAuthorizer → GET /v1/identity/me with caller's bearer token
        (REST between bounded contexts; Licensing never reads Identity tables)
        Identity__BaseUrl = https://rp-dev-func-identity.azurewebsites.net/api/

    Events (implemented): LicenseIssued, LicenseActivated, LicenseValidationSucceeded,
        LicenseValidationFailed, LicenseRevoked (all v1.0, via outbox → LicenseEvents)
    Events (catalog, later phases): LicenseRefreshed, LicenseRenewed, LicenseExpired,
        LicenseSuspended, LicenseReactivated, FeatureEntitlement*, LicenseSigningKeyRotated

    Events (consumed — Phase 5 Billing integration):
        SubscriptionActivated → IssueLicense
        SubscriptionCanceled/Expired → RevokeLicense/ExpireLicense
        PaymentFailed → SuspendLicense (grace policy)

    Functions:
        ValidateLicense, IssueLicense, RevokeLicense, ListLicenses, GetLicense,
        DispatchOutbox (timer 30s), HealthLive/HealthReady

    Shared package changes (Phase 4 refactor):
        OutboxWriter/OutboxDispatcher generalized into RestrictPoint.Database
        ApiResults moved to RestrictPoint.Auth.Http
        AuthenticationMiddlewareOptions: configurable anonymous-function allowlist

    Tests: tests/licensing (35 passing — crypto attacks, validation pipeline,
        issuance authorization matrix, revocation propagation)

---

## Billing

    App: apps/api-billing (RestrictPoint.Api.Billing)
    Service Bus Topic: BillingEvents
    Only Billing may publish PaymentSucceeded.

    ✅ PHASE 5 IMPLEMENTED (2026-07-16):

    Architecture:
        Stripe is merchant of record; Billing orchestrates (docs/12)
        IPaymentProvider / IWebhookVerifier abstractions — Stripe.net isolated to
            infrastructure; handlers are provider-agnostic and fully testable
        Stripe secrets (stripe-api-key, stripe-webhook-secret) in rp-dev-kv-301106,
            fetched at startup via Managed Identity — never in app settings
        Platform fee: 10% (Billing__PlatformFeePercent, docs/12 range 5-15%)

    Webhook processor (POST /v1/billing/webhook — anonymous; Stripe signature is
    the credential, verified before any processing):
        1. Signature verification (EventUtility.ConstructEvent, whsec from KV)
        2. Idempotency: ProcessedWebhookEvents unique StripeEventId index;
           marker commits atomically with the state change (race-safe)
        3. State machine (SubscriptionStateMachine): out-of-order/stale events can
           never force illegal transitions (e.g. resurrect a canceled subscription)
        4. Outbox-atomic event emission — financial state and events never diverge
        Handled: customer.subscription.{created,updated,deleted}, invoice.paid,
            invoice.payment_failed; dunning Active→PastDue; recovery PastDue→Active
        Stripe webhook endpoint: we_1TtoohDpmWKTUQn3aWO3e0cE (test mode) →
            https://rp-dev-func-billing.azurewebsites.net/api/v1/billing/webhook

    State machine (docs/12): Trialing→{Active,Canceled,Expired};
        Active→{PastDue,Paused,Canceled,Refunded}; PastDue→{Active,Canceled};
        Paused→{Active,Canceled}; Canceled→Expired; Expired/Refunded terminal

    License issuance saga (Billing NEVER issues directly):
        Checkout captures a validated LicenseTemplate onto the subscription
        → webhook activation → SubscriptionActivated v1.1 (self-contained: template +
          org/tenant context) → SB topic BillingEvents → subscription "licensing"
        → Licensing SubscriptionActivatedConsumer issues idempotently (dedupe by
          subscriptionId); malformed events throw → retry → dead-letter

    Database (Billing schema, migration pending deploy):
        Subscriptions (orgs, tenant, Stripe ids, plan, period, license template, licenseId)
        Invoices / Payments (mirrored from Stripe, decimal amounts)
        ProcessedWebhookEvents (idempotency), OutboxMessages

    APIs (implemented):
        POST /v1/billing/checkout             → 201, pending subscription + Stripe session
        POST /v1/billing/webhook              → anonymous, signature-verified, idempotent
        POST /v1/billing/subscriptions/cancel → customer org Owner/Admin/Billing;
                                                Stripe-first (no local state on provider failure)
        GET  /v1/billing/subscriptions?organizationId=  → member (customer or developer org)
        GET  /v1/billing/invoices?organizationId=       → member
        POST /v1/billing/stripe/connect       → 201, dev org Owner/Admin, Express onboarding

    Events (implemented): SubscriptionCreated, SubscriptionActivated v1.1,
        SubscriptionCanceled, SubscriptionPastDue, SubscriptionRenewed,
        PaymentSucceeded, PaymentFailed, InvoicePaid
    Events (catalog, later): SubscriptionExpired, RefundIssued, PlatformFeeCollected,
        DeveloperRevenue*, InvoiceGenerated, StripeAccount*

    Functions:
        CreateCheckout, StripeWebhook, CancelSubscription, ListSubscriptions,
        ListInvoices, ConnectOnboarding, DispatchOutbox (30s), HealthLive/HealthReady

    Shared refactor (Phase 5): IOrganizationRoleResolver promoted to RestrictPoint.Auth
        (used by licensing + billing)

    Tests: tests/billing (31 passing — state machine matrix, webhook idempotency +
        out-of-order attacks, dunning/recovery, checkout template validation);
        licensing consumer saga tests in tests/licensing (4)

---

## Marketplace

    App: apps/api-marketplace (RestrictPoint.Api.Marketplace)
    Service Bus Topic: MarketplaceEvents

    ✅ PHASE 6 FOUNDATION IMPLEMENTED (2026-07-16):

    Architecture:
        Publisher-centric marketplace for SharePoint/Teams web parts (docs/13)
        Listings aggregate state with 7-state lifecycle (Draft→Published→Suspended/Deprecated→Removed)
        ListingStateMachine enforces valid state transitions with Dictionary lookup
        Review system with 24hr edit window and fraud prevention (no self-review)
        Dynamic rating aggregation on Review creation
        Hierarchical category taxonomy with slug-based routing
        Tag system with usage tracking for trending discovery
        Multiple pricing plans per listing (Free, OneTimePurchase, MonthlySubscription, AnnualSubscription)
        Stripe integration via StripePriceId sync for billing
        LicenseTemplate JSON field on PricingPlan for automatic license provisioning

    Domain Entities (Phase 6):
        Listing: Core marketplace aggregate with 7 states (Draft, Published, Suspended, Deprecated, Removed, Rejected, UnderReview)
            - State machine: Draft→Published/Removed; Published→Suspended/Deprecated/Removed; Suspended→Published/Removed; Deprecated→Removed
            - Validation: Title 1-256 chars; Publish requires ≥1 active PricingPlan
            - InstallCount increment, AverageRating/ReviewCount aggregation
            - RecalculateRating() computes from Reviews collection
        PricingPlan: Business rules enforced via Create() method
            - Free: Price must be 0
            - Subscription: Requires BillingInterval (Monthly/Annual)
            - TrialDays: 0-365 validation
            - IsActive flag for soft archive
        Category: Hierarchical taxonomy (nullable ParentCategoryId)
            - Slug auto-generation from name
            - DisplayOrder for UI sorting
        Tag: Many-to-many with Listing via ListingTag join table
            - UsageCount tracking for trending
            - IncrementUsage/DecrementUsage methods
        Review: User feedback with moderation
            - Rating 1-5 validation
            - Comment max 4000 chars
            - Update() enforces 24hr edit window via (now - CreatedUtc).TotalHours check
            - IsFlagged for moderation, EditedUtc tracking

    Database (Marketplace schema, migration InitialMarketplaceSchema created but not applied):
        Listings (Id, ProjectId unique index, Status, AverageRating precision(3,2), composite index on Status+IsFeatured+AverageRating)
        PricingPlans (ListingId FK, PricingType/BillingInterval as string enum, StripePriceId indexed, LicenseTemplate JSON)
        Categories (Slug unique index, ParentCategoryId, DisplayOrder)
        Tags (Name/Slug both unique, UsageCount indexed)
        Reviews (Composite unique index ListingId+UserId, Rating indexed)
        ListingTags (Composite PK ListingId+TagId, cascade delete)
        OutboxMessages
        MI DB user: pending creation (will be rp-dev-func-marketplace db_datareader+db_datawriter WITH SID)

    APIs (handlers removed for MVP — Phase 6.1 implementation pending):
        Health endpoints only:
        GET  /health/live  → anonymous, {status:"healthy", service:"marketplace"}
        GET  /health/ready → anonymous, {status:"ready", service:"marketplace"}

    Events (catalog — Phase 6.1):
        ListingCreated, ListingPublished, ListingSuspended, ListingDeprecated, ListingRemoved,
        PricingPlanAdded, PricingPlanUpdated, ReviewCreated, ReviewUpdated

    Events (consumed — Phase 6.1):
        ProjectArchived/ProjectDeleted → unpublish/remove listings
        SubscriptionActivated → install count

    Functions (MVP — Phase 6.1 pending):
        HealthLive/HealthReady only; handlers deferred to avoid API compatibility issues

    Shared package usage:
        RestrictPoint.Common (Result<T>, Error.NotFound/Conflict/Validation/Forbidden)
        RestrictPoint.Database (AuditingSaveChangesInterceptor, OutboxMessage, BaseEntity)
        Domain errors use static factory methods (Error.NotFound vs new Error)

    Tests (created but not yet run — Phase 6.1):
        tests/marketplace/Domain: 31 test cases (ListingTests, ListingStateMachineTests, PricingPlanTests, ReviewTests)
        tests/marketplace/Application: Integration tests with SQLite in-memory, TestOutboxWriter, reflection-based private method testing
        Not executed: handler tests require completion of Phase 6.1 API layer

    Next Steps (Phase 6.1 — deferred pending API standardization):
        1. Complete handler implementations matching established billing/licensing pattern
        2. Run marketplace tests and verify domain logic
        3. Apply migration InitialMarketplaceSchema
        4. Provision MI database user rp-dev-func-marketplace
        5. Deploy to Azure Functions
        6. Implement featured listing curation workflow
        7. Add full-text search (Azure Cognitive Search or SQL Server FTS)

---

## Analytics

    App: apps/api-analytics (RestrictPoint.Api.Analytics)
    Service Bus Topic: analytics (plus consumes most other topics)

    Database (Analytics schema):
        UsageEvents
        AggregatedMetrics

    APIs:
        GET /v1/analytics/dashboard
        GET /v1/analytics/revenue
        GET /v1/analytics/licenses

    Events (consumed):
        Nearly all platform events (licensing, billing, identity, marketplace, SDK/telemetry)
        Ordering not required; consumers idempotent

    Functions:
        IngestUsageEvent
        AggregateMetrics
        GetDashboardMetrics
        GetRevenueMetrics
        GetLicenseMetrics

    Dependencies:
        All publishing contexts (read-only, event-driven)

    Shared Contracts:
        MetricDto
        DashboardDto

---

## Notifications

    App: apps/api-notifications (RestrictPoint.Api.Notifications)
    Service Bus Topic: notification

    Database (Notifications schema):
        NotificationQueue

    APIs:
        POST /v1/notifications/send
        GET  /v1/notifications

    Events (published):
        NotificationRequested
        NotificationSent
        NotificationFailed
        WebhookDeliveryRequested

    Events (consumed):
        UserInvited → invitation email
        PaymentFailed → dunning email
        SubscriptionActivated / LicenseIssued → welcome email
        OrganizationSuspended

    Functions:
        SendNotification
        ProcessNotificationQueue
        DeliverWebhook

    Dependencies:
        All contexts (event-driven fan-in)
        Email provider

    Shared Contracts:
        NotificationDto

---

## Audit

    Ownership: cross-cutting consumer service
    Service Bus Topic: audit (90-day retention)

    Database (Audit schema):
        AuditEvents                      (immutable, append-only)

    APIs:
        (none public; admin queries via Administration)

    Events (published):
        AuditEntryCreated
        SecurityViolationDetected
        APIKeyCreated
        APIKeyRevoked

    Events (consumed):
        All security-relevant events platform-wide

    Functions:
        RecordAuditEntry
        DetectSecurityViolation

    Dependencies:
        All contexts (subscribe-only)

    Shared Contracts:
        AuditEntryDto

---

## SDK / Telemetry

    Packages: packages/sdk-core, packages/sdk-spfx
    Service Bus Topics: telemetry (7-day retention)
    SDK hides all licensing complexity — no manual API calls by developers.

    Database:
        (none owned; writes flow to Licensing.Installations and Analytics.UsageEvents via events)

    APIs:
        POST /v1/sdk/bootstrap           (<150ms target)
        POST /v1/sdk/telemetry           (batch ingestion)

    Events (published):
        SDKInstalled
        SDKInitialized
        SDKValidationRequested
        SDKValidationCached
        SDKValidationFailed
        SDKVersionReported
        FeatureUsed
        ApplicationStarted
        ApplicationErrorReported
        InstallationHeartbeatReceived
        SDKConfigurationRetrieved
        SDKConfigurationFailed

    Functions:
        BootstrapSdk
        IngestTelemetry

    Dependencies:
        Licensing (validation)
        Projects (configuration)

    Shared Contracts:
        RestrictPointClientOptions
        SpfxLicenseContext
        LicenseValidationResponse

---

## Platform Administration

    App: apps/admin (frontend) + platform operations
    Service Bus Topic: platform

    Database:
        (platform configuration via App Configuration; no dedicated schema in v1)

    APIs:
        GET /health/live                  (every service)
        GET /health/ready                 (every service)

    Events (published):
        PlatformConfigurationChanged
        PlatformFeatureEnabled
        PlatformFeatureDisabled
        ServiceHealthChanged
        DataExportRequested
        DataDeletionRequested

    Functions:
        ChangePlatformConfiguration
        TogglePlatformFeature
        RequestDataExport
        RequestDataDeletion

    Dependencies:
        All contexts (administrative reach)

    Shared Contracts:
        PlatformConfigDto

---

## Cross-Cutting Reference

    Event Envelope (all events):
        eventId, eventType, eventVersion, occurredUtc, correlationId,
        causationId?, organizationId, tenantId?, publisher, payload
        Max size 256 KB; no secrets in payloads; TS type: @restrictpoint/contracts DomainEvent<T>

    Service Bus Topics:
        identity, organization, project, billing, licensing, marketplace,
        analytics, notification, audit, telemetry, platform
        Subscriptions: {consumer}-{purpose}
        Retry: 1m → 5m → 15m → 30m → DLQ (max 5); duplicate detection 10 min

    API Standards:
        Base: https://api.restrictpoint.com/v1/
        Auth: Bearer JWT (claims: sub, tid, oid, roles); all requests tenant-scoped
        Success: { data, correlationId, timestamp } | Error: { error{code,message,details}, correlationId }
        Idempotency-Key header required: billing/webhook, license issuance, subscription changes
        Error codes: AUTH_001/002, LIC_001/002, BILL_001, ORG_001, PRJ_001
        Rate limits: Free 100/min, Pro 1k/min, Enterprise 10k/min

    Performance Targets:
        License validation <50ms | SDK bootstrap <150ms | Read APIs <100ms
        Write APIs <250ms | Billing APIs <500ms | API P99 <150ms | Portal pages <200ms

    Database Standards:
        Shared DB, shared schema, RLS tenant isolation; GUID PKs (NEWSEQUENTIALID)
        Soft deletes only; EF Core migrations per context via CI/CD only
        Indexes: Id (clustered), TenantId, TenantId+ProjectId, TenantId+CreatedUtc, TenantId+Status

    Infrastructure:
        Terraform-only provisioning; GitHub OIDC (no CI secrets); Managed Identity everywhere
        Key Vault for secrets + ES256 (EC P-256) signing keys; APIM fronts all APIs; Front Door + WAF
        Environments: dev, test, staging, prod — separate state, RGs, secrets
