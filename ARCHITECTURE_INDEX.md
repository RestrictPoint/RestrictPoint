# ARCHITECTURE_INDEX.md

**AI Working Memory — compact, always-current map of the RestrictPoint system.**

> Regenerate this file whenever the architecture changes (new table, endpoint, event, contract, or dependency).
> Sources of truth: docs/02 (Domain), docs/05 (Structure), docs/09 (Database), docs/16 (API), docs/17 (Terraform), docs/20 (Events).
> Rules: one publisher per event; only the owning context writes its schema; cross-context communication is event-driven; every table carries Id/TenantId/CreatedUtc/UpdatedUtc/DeletedUtc/RowVersion; every context has an OutboxEvents table.

---

## Identity

    App: apps/api-identity (RestrictPoint.Api.Identity)
    Service Bus Topic: identity

    Database (Identity schema):
        Users
        UserOrganizations
        RefreshTokens
        OutboxEvents

    APIs:
        GET  /v1/identity/me
        GET  /v1/identity/organizations
        POST /v1/identity/organizations
        POST /v1/identity/organizations/{id}/invite

    Events (published):
        UserRegistered
        UserAuthenticated
        UserProfileUpdated
        UserInvited
        UserInvitationAccepted
        UserRemoved
        UserRoleChanged

    Functions:
        GetCurrentUser
        RegisterUser
        AuthenticateUser
        InviteUser
        AcceptInvitation
        RemoveUser
        ChangeUserRole

    Dependencies:
        Entra External ID (authentication)
        Redis (identity cache)

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
    Service Bus Topic: licensing
    Hot path: license validation <50ms, no multi-table joins, Redis-first

    Database (Licensing schema):
        Licenses
        LicenseFeatures
        LicenseTokens
        Installations
        OutboxEvents

    APIs:
        POST /v1/licenses/validate       (SDK critical path)
        POST /v1/licenses/issue          (internal, billing-triggered)
        POST /v1/licenses/revoke
        GET  /v1/licenses
        GET  /v1/licenses/{id}

    Events (published):
        LicenseIssued
        LicenseActivated
        LicenseValidationSucceeded
        LicenseValidationFailed
        LicenseRefreshed
        LicenseRenewed
        LicenseExpired
        LicenseRevoked
        LicenseSuspended
        LicenseReactivated
        FeatureEntitlementAdded
        FeatureEntitlementRemoved
        LicenseSigningKeyRotated
        LicenseInstallationRegistered
        LicenseInstallationRemoved

    Events (consumed):
        SubscriptionActivated → IssueLicense
        SubscriptionCanceled/Expired → RevokeLicense/ExpireLicense
        PaymentFailed → SuspendLicense (grace policy)
        OrganizationSuspended

    Functions:
        ValidateLicense
        IssueLicense
        RevokeLicense
        RenewLicense
        RotateSigningKey
        RegisterInstallation

    Dependencies:
        Billing (subscription events)
        Projects (signing keys, config)
        Key Vault (ES256 / ECDSA P-256 signing)
        Redis (license cache)

    Shared Contracts:
        LicenseDto
        LicenseValidationRequest
        LicenseValidationResponse
        License JWT (offline-capable, ES256-signed)

---

## Billing

    App: apps/api-billing (RestrictPoint.Api.Billing)
    Service Bus Topic: billing
    Only Billing may publish PaymentSucceeded.

    Database (Billing schema):
        Customers
        Subscriptions
        Payments
        Invoices
        OutboxEvents

    APIs:
        POST /v1/billing/checkout
        POST /v1/billing/webhook          (Stripe; idempotent)
        GET  /v1/billing/subscriptions
        POST /v1/billing/subscriptions/cancel
        POST /v1/billing/stripe/connect
        GET  /v1/billing/invoices

    Events (published):
        StripeAccountConnected
        StripeAccountDisconnected
        SubscriptionCreated              (ordering required with Activated/Canceled)
        SubscriptionActivated
        SubscriptionRenewed
        SubscriptionCanceled
        SubscriptionExpired
        PaymentSucceeded
        PaymentFailed
        RefundIssued
        PlatformFeeCollected
        DeveloperRevenueGenerated
        DeveloperPayoutCompleted
        InvoiceGenerated

    Events (consumed):
        OrganizationCreated → create billing account
        OrganizationSuspended

    Functions:
        CreateCheckoutSession
        ProcessStripeWebhook
        CancelSubscription
        StartStripeConnectOnboarding
        GenerateInvoice
        ProcessDunning

    Dependencies:
        Stripe (Connect)
        Organizations
        Licensing (downstream consumer)

    Shared Contracts:
        SubscriptionDto
        InvoiceDto
        Subscription states: Trial, Active, Grace, PastDue, Canceled, Expired, Suspended

---

## Marketplace

    App: apps/api-marketplace (RestrictPoint.Api.Marketplace)
    Service Bus Topic: marketplace

    Database (Marketplace schema):
        Listings
        ListingPricing
        OutboxEvents

    APIs:
        GET  /v1/marketplace/listings     (filters: category, tag, rating, priceType)
        POST /v1/marketplace/listings
        POST /v1/marketplace/listings/{id}/publish
        POST /v1/marketplace/listings/{id}/review
        GET  /v1/marketplace/search

    Events (published):
        MarketplaceListingCreated
        MarketplaceListingPublished
        MarketplaceListingUpdated
        MarketplaceListingUnpublished
        MarketplaceListingRemoved
        PricingModelCreated
        PricingModelUpdated

    Events (consumed):
        OrganizationCreated / OrganizationSuspended
        ProjectArchived / ProjectDeleted

    Functions:
        CreateListing
        PublishListing
        UnpublishListing
        SubmitReview
        SearchListings

    Dependencies:
        Projects
        Billing (purchase flow)
        Organizations

    Shared Contracts:
        ListingDto
        PricingDto

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
