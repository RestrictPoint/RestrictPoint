using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.Common;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Marketplace.Application.BrowseListings;

/// <summary>Query parameters for the public catalog endpoints.</summary>
public sealed record BrowseListingsQuery
{
    public Guid? CategoryId { get; init; }

    public string? Tag { get; init; }

    public Guid? OrganizationId { get; init; }

    public bool FeaturedOnly { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Public catalog queries (docs/13): list, detail, and search. Anonymous by design —
/// the marketplace is the public commercial surface. Visibility follows the state rules:
/// Published and Deprecated (with warning) are visible; everything else is hidden.
/// </summary>
public sealed class BrowseListingsHandler
{
    private const int MaxPageSize = 100;
    private const int SearchResultLimit = 50;
    private const int RecentReviewLimit = 10;

    private static readonly ListingStatus[] VisibleStatuses =
        [ListingStatus.Published, ListingStatus.Deprecated];

    private readonly MarketplaceDbContext _dbContext;

    public BrowseListingsHandler(MarketplaceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>GET /v1/marketplace/listings — filtered, ranked catalog page.</summary>
    public async Task<Result<IReadOnlyList<ListingSummary>>> ListAsync(
        BrowseListingsQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var listings = _dbContext.Listings
            .AsNoTracking()
            .Where(l => VisibleStatuses.Contains(l.Status));

        if (query.CategoryId is not null)
        {
            listings = listings.Where(l => l.CategoryId == query.CategoryId);
        }

        if (query.OrganizationId is not null)
        {
            listings = listings.Where(l => l.OrganizationId == query.OrganizationId);
        }

        if (query.FeaturedOnly)
        {
            listings = listings.Where(l => l.IsFeatured);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            listings = listings.Where(l => l.Tags.Any(lt => lt.Tag.Name == tag));
        }

        var results = await ApplyRanking(listings)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<ListingSummary>>(
            results.Select(ListingMapper.ToSummary).ToList());
    }

    /// <summary>GET /v1/marketplace/listings/{id} — full public detail.</summary>
    public async Task<Result<ListingDetail>> GetAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await _dbContext.Listings
            .AsNoTracking()
            .Include(l => l.PricingPlans)
            .Include(l => l.Tags).ThenInclude(lt => lt.Tag)
            .SingleOrDefaultAsync(
                l => l.Id == listingId && VisibleStatuses.Contains(l.Status),
                cancellationToken)
            .ConfigureAwait(false);

        if (listing is null)
        {
            return MarketplaceErrors.ListingNotFound;
        }

        var recentReviews = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.ListingId == listingId && !r.IsFlagged)
            .OrderByDescending(r => r.CreatedUtc)
            .Take(RecentReviewLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tags = listing.Tags.Select(lt => lt.Tag.Name).ToList();

        return ListingMapper.ToDetail(listing, tags, recentReviews);
    }

    /// <summary>GET /v1/marketplace/search — full-text search over title/description.</summary>
    public async Task<Result<IReadOnlyList<ListingSummary>>> SearchAsync(
        string? term,
        Guid? categoryId,
        string? tag,
        CancellationToken cancellationToken)
    {
        var listings = _dbContext.Listings
            .AsNoTracking()
            .Where(l => l.Status == ListingStatus.Published);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var text = term.Trim();
            listings = listings.Where(l => l.Title.Contains(text) || l.Description.Contains(text));
        }

        if (categoryId is not null)
        {
            listings = listings.Where(l => l.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagName = tag.Trim();
            listings = listings.Where(l => l.Tags.Any(lt => lt.Tag.Name == tagName));
        }

        var results = await ApplyRanking(listings)
            .Take(SearchResultLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<ListingSummary>>(
            results.Select(ListingMapper.ToSummary).ToList());
    }

    /// <summary>Discovery ranking (docs/13): featured > rating > installs > recency.</summary>
    private static IOrderedQueryable<Listing> ApplyRanking(IQueryable<Listing> listings) =>
        listings
            .OrderByDescending(l => l.IsFeatured)
            .ThenByDescending(l => l.AverageRating)
            .ThenByDescending(l => l.InstallCount)
            .ThenByDescending(l => l.CreatedUtc);
}
