using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Domain;

/// <summary>
/// Represents a user review for a marketplace listing.
/// </summary>
public sealed class Review : BaseEntity
{
    /// <summary>
    /// The listing being reviewed.
    /// </summary>
    public Guid ListingId { get; private set; }

    /// <summary>
    /// The user who wrote the review.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Rating (1-5 stars).
    /// </summary>
    public int Rating { get; private set; }

    /// <summary>
    /// Review comment (optional, Markdown supported).
    /// </summary>
    public string? Comment { get; private set; }

    /// <summary>
    /// Whether this review has been flagged for moderation.
    /// </summary>
    public bool IsFlagged { get; private set; }

    /// <summary>
    /// When the review was last edited (null if never edited).
    /// </summary>
    public DateTime? EditedUtc { get; private set; }

    private Review() { } // EF

    public static Review Create(Guid listingId, Guid userId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        if (comment?.Length > 4000)
            throw new ArgumentException("Comment cannot exceed 4000 characters.", nameof(comment));

        return new Review
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            UserId = userId,
            Rating = rating,
            Comment = comment?.Trim(),
            IsFlagged = false,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public void Update(int rating, string? comment)
    {
        var now = DateTime.UtcNow;

        if ((now - CreatedUtc).TotalHours > 24)
            throw new InvalidOperationException("Reviews can only be edited within 24 hours of creation.");

        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        if (comment?.Length > 4000)
            throw new ArgumentException("Comment cannot exceed 4000 characters.", nameof(comment));

        Rating = rating;
        Comment = comment?.Trim();
        EditedUtc = now;
        UpdatedUtc = now;
    }

    public void Flag()
    {
        IsFlagged = true;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Unflag()
    {
        IsFlagged = false;
        UpdatedUtc = DateTime.UtcNow;
    }
}
