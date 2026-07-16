using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Domain;

/// <summary>
/// Represents a pricing configuration for a marketplace listing.
/// </summary>
public sealed class PricingPlan : BaseEntity
{
    /// <summary>
    /// The listing this pricing plan belongs to.
    /// </summary>
    public Guid ListingId { get; private set; }

    /// <summary>
    /// Display name for this pricing tier (e.g., "Standard", "Enterprise").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The pricing model.
    /// </summary>
    public PricingType PricingType { get; private set; }

    /// <summary>
    /// Price amount (0 for Free).
    /// </summary>
    public decimal Price { get; private set; }

    /// <summary>
    /// Currency code (ISO 4217, e.g., "USD").
    /// </summary>
    public string Currency { get; private set; } = "USD";

    /// <summary>
    /// Billing interval for subscriptions (null for one-time).
    /// </summary>
    public BillingInterval? BillingInterval { get; private set; }

    /// <summary>
    /// Trial period in days (0 = no trial).
    /// </summary>
    public int TrialDays { get; private set; }

    /// <summary>
    /// Whether this plan is currently available for purchase.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Stripe Price ID (set when synced to Stripe).
    /// </summary>
    public string? StripePriceId { get; private set; }

    /// <summary>
    /// License template JSON (defines features, quotas, expiry).
    /// </summary>
    public string? LicenseTemplate { get; private set; }

    private PricingPlan() { } // EF

    public static PricingPlan Create(
        Guid listingId,
        string name,
        PricingType pricingType,
        decimal price,
        string currency,
        BillingInterval? billingInterval,
        int trialDays,
        string? licenseTemplate)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
            throw new ArgumentException("Name must be 1-128 characters.", nameof(name));

        if (price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));

        if (pricingType == PricingType.Free && price != 0)
            throw new ArgumentException("Free pricing must have price = 0.", nameof(price));

        if (pricingType is PricingType.MonthlySubscription or PricingType.AnnualSubscription && billingInterval is null)
            throw new ArgumentException("Subscription pricing requires a billing interval.", nameof(billingInterval));

        if (trialDays < 0 || trialDays > 365)
            throw new ArgumentException("Trial days must be 0-365.", nameof(trialDays));

        return new PricingPlan
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            Name = name.Trim(),
            PricingType = pricingType,
            Price = price,
            Currency = currency.ToUpperInvariant(),
            BillingInterval = billingInterval,
            TrialDays = trialDays,
            IsActive = true,
            LicenseTemplate = licenseTemplate,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public void Update(string name, decimal price, int trialDays, string? licenseTemplate)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
            throw new ArgumentException("Name must be 1-128 characters.", nameof(name));

        if (price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));

        if (trialDays < 0 || trialDays > 365)
            throw new ArgumentException("Trial days must be 0-365.", nameof(trialDays));

        Name = name.Trim();
        Price = price;
        TrialDays = trialDays;
        LicenseTemplate = licenseTemplate;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetStripePriceId(string stripePriceId)
    {
        if (string.IsNullOrWhiteSpace(stripePriceId))
            throw new ArgumentException("Stripe Price ID cannot be empty.", nameof(stripePriceId));

        StripePriceId = stripePriceId;
        UpdatedUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Supported pricing models per docs/13.
/// </summary>
public enum PricingType
{
    Free,
    OneTimePurchase,
    MonthlySubscription,
    AnnualSubscription
}

/// <summary>
/// Billing interval for subscription plans.
/// </summary>
public enum BillingInterval
{
    Monthly,
    Annual
}
