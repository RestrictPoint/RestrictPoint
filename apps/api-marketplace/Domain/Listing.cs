using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Domain;

/// <summary>
/// Represents a product listing in the marketplace.
/// </summary>
public sealed class Listing : BaseEntity
{
    /// <summary>
    /// The project this listing is associated with (must exist in Identity).
    /// </summary>
    public Guid ProjectId { get; private set; }

    /// <summary>
    /// The organization that owns this listing.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>
    /// Public display title (max 256 characters).
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Full description (Markdown supported).
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Current lifecycle state.
    /// </summary>
    public ListingStatus Status { get; private set; }

    /// <summary>
    /// Primary category identifier.
    /// </summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// The SPFx web part GUID this listing represents.
    /// </summary>
    public Guid WebPartGuid { get; private set; }

    /// <summary>
    /// Whether this listing is featured (admin-controlled).
    /// </summary>
    public bool IsFeatured { get; private set; }

    /// <summary>
    /// Total install count (incremented on purchase).
    /// </summary>
    public int InstallCount { get; private set; }

    /// <summary>
    /// Average rating (0-5, computed from reviews).
    /// </summary>
    public decimal AverageRating { get; private set; }

    /// <summary>
    /// Total number of reviews.
    /// </summary>
    public int ReviewCount { get; private set; }

    /// <summary>
    /// Logo/icon URL (CDN path).
    /// </summary>
    public string? LogoUrl { get; private set; }

    /// <summary>
    /// Screenshot URLs (JSON array).
    /// </summary>
    public string? Screenshots { get; private set; }

    /// <summary>
    /// Publisher support URL.
    /// </summary>
    public string? SupportUrl { get; private set; }

    /// <summary>
    /// Publisher documentation URL.
    /// </summary>
    public string? DocumentationUrl { get; private set; }

    /// <summary>
    /// Associated pricing plans (1:many).
    /// </summary>
    public List<PricingPlan> PricingPlans { get; private set; } = [];

    /// <summary>
    /// Associated tags (many:many).
    /// </summary>
    public List<ListingTag> Tags { get; private set; } = [];

    /// <summary>
    /// Associated reviews.
    /// </summary>
    public List<Review> Reviews { get; private set; } = [];

    private Listing() { } // EF

    public static Listing Create(
        Guid projectId,
        Guid organizationId,
        string title,
        string description,
        Guid categoryId,
        Guid webPartGuid)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256)
            throw new ArgumentException("Title must be 1-256 characters.", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        return new Listing
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            OrganizationId = organizationId,
            Title = title.Trim(),
            Description = description.Trim(),
            Status = ListingStatus.Draft,
            CategoryId = categoryId,
            WebPartGuid = webPartGuid,
            IsFeatured = false,
            InstallCount = 0,
            AverageRating = 0,
            ReviewCount = 0,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public void Update(string title, string description, Guid categoryId, string? logoUrl, string? supportUrl, string? documentationUrl)
    {
        if (Status != ListingStatus.Draft && Status != ListingStatus.Published)
            throw new InvalidOperationException($"Cannot update listing in {Status} status.");

        if (string.IsNullOrWhiteSpace(title) || title.Length > 256)
            throw new ArgumentException("Title must be 1-256 characters.", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        Title = title.Trim();
        Description = description.Trim();
        CategoryId = categoryId;
        LogoUrl = logoUrl;
        SupportUrl = supportUrl;
        DocumentationUrl = documentationUrl;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Publish()
    {
        if (!ListingStateMachine.CanTransition(Status, ListingStatus.Published))
            throw new InvalidOperationException($"Cannot transition from {Status} to Published.");

        if (PricingPlans.Count == 0)
            throw new InvalidOperationException("Cannot publish listing without at least one pricing plan.");

        Status = ListingStatus.Published;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Suspend(string reason)
    {
        if (!ListingStateMachine.CanTransition(Status, ListingStatus.Suspended))
            throw new InvalidOperationException($"Cannot transition from {Status} to Suspended.");

        Status = ListingStatus.Suspended;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Deprecate()
    {
        if (!ListingStateMachine.CanTransition(Status, ListingStatus.Deprecated))
            throw new InvalidOperationException($"Cannot transition from {Status} to Deprecated.");

        Status = ListingStatus.Deprecated;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Remove()
    {
        if (!ListingStateMachine.CanTransition(Status, ListingStatus.Removed))
            throw new InvalidOperationException($"Cannot transition from {Status} to Removed.");

        Status = ListingStatus.Removed;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void MarkAsFeatured()
    {
        if (Status != ListingStatus.Published)
            throw new InvalidOperationException("Only published listings can be featured.");

        IsFeatured = true;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void UnmarkAsFeatured()
    {
        IsFeatured = false;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void IncrementInstallCount()
    {
        InstallCount++;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void RecalculateRating()
    {
        if (Reviews.Count == 0)
        {
            AverageRating = 0;
            ReviewCount = 0;
        }
        else
        {
            AverageRating = (decimal)Reviews.Average(r => r.Rating);
            ReviewCount = Reviews.Count;
        }
        UpdatedUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Listing lifecycle states per docs/13.
/// </summary>
public enum ListingStatus
{
    Draft,
    Published,
    Suspended,
    Deprecated,
    Removed
}

/// <summary>
/// State machine enforcing legal listing transitions.
/// </summary>
public static class ListingStateMachine
{
    private static readonly Dictionary<ListingStatus, HashSet<ListingStatus>> _allowedTransitions = new()
    {
        [ListingStatus.Draft] = [ListingStatus.Published, ListingStatus.Removed],
        [ListingStatus.Published] = [ListingStatus.Suspended, ListingStatus.Deprecated, ListingStatus.Removed],
        [ListingStatus.Suspended] = [ListingStatus.Published, ListingStatus.Removed],
        [ListingStatus.Deprecated] = [ListingStatus.Removed],
        [ListingStatus.Removed] = []
    };

    public static bool CanTransition(ListingStatus from, ListingStatus to)
    {
        return _allowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
