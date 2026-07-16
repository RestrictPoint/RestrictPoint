using RestrictPoint.Database;

namespace RestrictPoint.Api.Billing.Domain;

/// <summary>Subscription lifecycle states (docs/12).</summary>
public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Paused,
    Canceled,
    Expired,
    Refunded,
}

/// <summary>
/// A customer subscription for a project (docs/12 subscription entity). Billing is the
/// system of financial truth; Stripe is the merchant of record. Only Active subscriptions
/// may trigger license issuance — enforced by the state machine, never bypassed.
/// </summary>
public sealed class Subscription : BaseEntity
{
    public const int StripeIdMaxLength = 256;
    public const int PlanMaxLength = 50;

    public required Guid CustomerOrganizationId { get; set; }

    public required Guid DeveloperOrganizationId { get; set; }

    public required Guid ProjectId { get; set; }

    /// <summary>The customer SharePoint tenant licenses will bind to.</summary>
    public required Guid CustomerTenantId { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public string? StripeSubscriptionId { get; set; }

    public string? StripeCustomerId { get; set; }

    public required string Plan { get; set; }

    public DateTimeOffset? CurrentPeriodStart { get; set; }

    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// License template captured at checkout, applied by Licensing on activation.
    /// Serialized JSON: { licenseType, features, limits, webPartGuids }.
    /// </summary>
    public required string LicenseTemplate { get; set; }

    /// <summary>The license issued for this subscription, once Licensing reports back.</summary>
    public Guid? LicenseId { get; set; }
}

/// <summary>
/// The subscription state machine (docs/12). Transitions not listed here are illegal and
/// are rejected — webhook handlers must never force a state.
/// </summary>
public static class SubscriptionStateMachine
{
    private static readonly Dictionary<SubscriptionStatus, SubscriptionStatus[]> Allowed = new()
    {
        [SubscriptionStatus.Trialing] =
            [SubscriptionStatus.Active, SubscriptionStatus.Canceled, SubscriptionStatus.Expired],
        [SubscriptionStatus.Active] =
            [SubscriptionStatus.PastDue, SubscriptionStatus.Paused, SubscriptionStatus.Canceled, SubscriptionStatus.Refunded],
        [SubscriptionStatus.PastDue] =
            [SubscriptionStatus.Active, SubscriptionStatus.Canceled],
        [SubscriptionStatus.Paused] =
            [SubscriptionStatus.Active, SubscriptionStatus.Canceled],
        [SubscriptionStatus.Canceled] =
            [SubscriptionStatus.Expired],
        [SubscriptionStatus.Expired] = [],
        [SubscriptionStatus.Refunded] = [],
    };

    /// <summary>Returns true when the transition is legal. Self-transitions are no-ops and allowed.</summary>
    public static bool CanTransition(SubscriptionStatus from, SubscriptionStatus to) =>
        from == to || Allowed[from].Contains(to);
}
