using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Domain;

/// <summary>
/// Represents a hierarchical product category.
/// </summary>
public sealed class Category : BaseEntity
{
    /// <summary>
    /// Category display name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Parent category for hierarchical organization (null = root).
    /// </summary>
    public Guid? ParentCategoryId { get; private set; }

    /// <summary>
    /// URL-friendly slug for routing.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>
    /// Sort order within parent.
    /// </summary>
    public int DisplayOrder { get; private set; }

    private Category() { } // EF

    public static Category Create(string name, Guid? parentCategoryId, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
            throw new ArgumentException("Name must be 1-128 characters.", nameof(name));

        var slug = GenerateSlug(name);

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ParentCategoryId = parentCategoryId,
            Slug = slug,
            DisplayOrder = displayOrder,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public void Update(string name, Guid? parentCategoryId, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
            throw new ArgumentException("Name must be 1-128 characters.", nameof(name));

        Name = name.Trim();
        ParentCategoryId = parentCategoryId;
        DisplayOrder = displayOrder;
        Slug = GenerateSlug(name);
        UpdatedUtc = DateTime.UtcNow;
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("&", "and");
    }
}
