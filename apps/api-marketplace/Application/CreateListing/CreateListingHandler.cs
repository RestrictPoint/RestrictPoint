using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.Common;
using RestrictPoint.Api.Marketplace.Application.Events;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Marketplace.Application.CreateListing;

/// <summary>Input validation for POST /v1/marketplace/listings.</summary>
public sealed class CreateListingRequestValidator : AbstractValidator<CreateListingRequest>
{
    public CreateListingRequestValidator()
    {
        RuleFor(r => r.ProjectId).NotEmpty();
        RuleFor(r => r.OrganizationId).NotEmpty();
        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).NotEmpty();
        RuleFor(r => r.CategoryId).NotEmpty();
        RuleFor(r => r.WebPartGuid).NotEmpty();
        RuleFor(r => r.LogoUrl).MaximumLength(500).Must(BeHttpsOrNull)
            .WithMessage("LogoUrl must be an absolute https URL.");
        RuleFor(r => r.SupportUrl).MaximumLength(500).Must(BeHttpsOrNull)
            .WithMessage("SupportUrl must be an absolute https URL.");
        RuleFor(r => r.DocumentationUrl).MaximumLength(500).Must(BeHttpsOrNull)
            .WithMessage("DocumentationUrl must be an absolute https URL.");
        RuleFor(r => r.Tags).Must(t => t is null || t.Count <= 10)
            .WithMessage("A listing may carry at most 10 tags.");
        RuleForEach(r => r.Tags).NotEmpty().MaximumLength(64);

        static bool BeHttpsOrNull(string? url) =>
            url is null || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// POST /v1/marketplace/listings — creates a Draft listing for a project. Only members of
/// the owning organization with a publishing role may create listings (docs/13 security model).
/// </summary>
public sealed class CreateListingHandler
{
    private static readonly string[] PublishingRoles = ["Owner", "Admin", "Developer"];

    private readonly MarketplaceDbContext _dbContext;
    private readonly IOrganizationRoleResolver _roleResolver;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public CreateListingHandler(
        MarketplaceDbContext dbContext,
        IOrganizationRoleResolver roleResolver,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _roleResolver = roleResolver;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ListingDetail>> HandleAsync(
        RequestContext context,
        string bearerToken,
        CreateListingRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = request.OrganizationId!.Value;

        var role = await _roleResolver
            .GetCallerRoleAsync(bearerToken, organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        if (role.Value is null || !PublishingRoles.Contains(role.Value, StringComparer.OrdinalIgnoreCase))
        {
            return MarketplaceErrors.NotAuthorizedForOrganization;
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == request.CategoryId!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (!categoryExists)
        {
            return MarketplaceErrors.CategoryNotFound;
        }

        var duplicate = await _dbContext.Listings
            .IgnoreQueryFilters()
            .AnyAsync(l => l.ProjectId == request.ProjectId!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate)
        {
            return MarketplaceErrors.ListingAlreadyExists;
        }

        var listing = Listing.Create(
            request.ProjectId!.Value,
            organizationId,
            request.Title!,
            request.Description!,
            request.CategoryId!.Value,
            request.WebPartGuid!.Value);

        if (request.LogoUrl is not null || request.SupportUrl is not null || request.DocumentationUrl is not null)
        {
            listing.Update(
                listing.Title, listing.Description, listing.CategoryId,
                request.LogoUrl, request.SupportUrl, request.DocumentationUrl);
        }

        var tagNames = await AttachTagsAsync(listing, request.Tags, cancellationToken).ConfigureAwait(false);

        _dbContext.Listings.Add(listing);

        _outbox.Stage(
            Topics.Marketplace,
            DomainEventEnvelope.Create(
                eventType: nameof(MarketplaceListingCreated),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: organizationId,
                payload: new MarketplaceListingCreated
                {
                    ListingId = listing.Id,
                    ProjectId = listing.ProjectId,
                    OrganizationId = listing.OrganizationId,
                    CreatedByUserId = context.CallerUserId(),
                    CreatedUtc = _timeProvider.GetUtcNow(),
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ListingMapper.ToDetail(listing, tagNames, []);
    }

    /// <summary>Resolves or creates tags for the listing, incrementing usage counts.</summary>
    private async Task<IReadOnlyList<string>> AttachTagsAsync(
        Listing listing,
        IReadOnlyList<string>? tags,
        CancellationToken cancellationToken)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        var names = tags
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _dbContext.Tags
            .Where(t => names.Contains(t.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var attached = new List<string>(names.Count);

        foreach (var name in names)
        {
            var tag = existing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tag is null)
            {
                tag = Tag.Create(name);
                _dbContext.Tags.Add(tag);
            }

            tag.IncrementUsage();
            listing.Tags.Add(new ListingTag { ListingId = listing.Id, TagId = tag.Id, Listing = listing, Tag = tag });
            attached.Add(tag.Name);
        }

        return attached;
    }
}
