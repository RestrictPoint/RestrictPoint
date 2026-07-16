using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Domain;

/// <summary>
/// Represents a searchable tag for marketplace listings.
/// </summary>
public sealed class Tag : BaseEntity
{
    /// <summary>
    /// Tag name (unique, case-insensitive).
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// URL-friendly slug.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>
    /// Usage count (for trending/popular tags).
    /// </summary>
    public int UsageCount { get; private set; }

    private Tag() { } // EF

    public static Tag Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64)
            throw new ArgumentException("Tag name must be 1-64 characters.", nameof(name));

        var slug = GenerateSlug(name);

        return new Tag
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug,
            UsageCount = 0,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public void IncrementUsage()
    {
        UsageCount++;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void DecrementUsage()
    {
        if (UsageCount > 0)
        {
            UsageCount--;
            UpdatedUtc = DateTime.UtcNow;
        }
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("#", "sharp")
            .Replace(".", "");
    }
}

/// <summary>
/// Join table for Listing-Tag many-to-many relationship.
/// </summary>
public sealed class ListingTag
{
    public Guid ListingId { get; set; }
    public Guid TagId { get; set; }

    public Listing Listing { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
