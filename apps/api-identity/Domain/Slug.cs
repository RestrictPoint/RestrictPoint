using System.Text.RegularExpressions;

namespace RestrictPoint.Api.Identity.Domain;

/// <summary>
/// Generates URL-safe organization slugs. Slugs are lowercase alphanumerics and hyphens,
/// derived from the organization name, and must be globally unique.
/// </summary>
public static partial class Slug
{
    public const int MaxLength = Organization.SlugMaxLength;

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")]
    private static partial Regex ValidSlug();

    /// <summary>
    /// Derives a slug from an organization name. Returns null when the name contains
    /// no usable characters (e.g. entirely non-Latin punctuation).
    /// </summary>
    public static string? FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var slug = NonSlugCharacters()
            .Replace(name.Trim().ToLowerInvariant(), "-")
            .Trim('-');

        if (slug.Length == 0)
        {
            return null;
        }

        return slug.Length <= MaxLength ? slug : slug[..MaxLength].TrimEnd('-');
    }

    /// <summary>Appends a short random suffix to resolve a uniqueness collision.</summary>
    public static string WithUniquenessSuffix(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var suffix = $"-{Guid.NewGuid():N}"[..7]; // "-" + 6 hex chars
        var maxBaseLength = MaxLength - suffix.Length;
        var baseSlug = slug.Length <= maxBaseLength ? slug : slug[..maxBaseLength].TrimEnd('-');

        return baseSlug + suffix;
    }

    public static bool IsValid(string slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && ValidSlug().IsMatch(slug);
}
