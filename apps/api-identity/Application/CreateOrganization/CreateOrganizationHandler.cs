using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Abstractions;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Application.Events;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Identity.Application.CreateOrganization;

/// <summary>Input validation for POST /v1/identity/organizations (fail fast, docs/07).</summary>
public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(Organization.NameMaxLength);

        RuleFor(r => r.BillingEmail)
            .EmailAddress()
            .MaximumLength(Organization.BillingEmailMaxLength)
            .When(r => !string.IsNullOrWhiteSpace(r.BillingEmail));
    }
}

/// <summary>
/// POST /v1/identity/organizations — creates an organization with the caller as Owner and
/// stages the OrganizationCreated event that initializes all downstream services (docs/20).
/// </summary>
public sealed class CreateOrganizationHandler
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserResolver _userResolver;
    private readonly IOutboxWriter _outbox;
    private readonly IUserContextCache _cache;
    private readonly TimeProvider _timeProvider;

    public CreateOrganizationHandler(
        IdentityDbContext dbContext,
        UserResolver userResolver,
        IOutboxWriter outbox,
        IUserContextCache cache,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _userResolver = userResolver;
        _outbox = outbox;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<Result<OrganizationCreatedResponse>> HandleAsync(
        RequestContext context,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var resolution = await _userResolver.ResolveOrProvisionAsync(context, cancellationToken)
            .ConfigureAwait(false);

        if (resolution.IsFailure)
        {
            return resolution.Error!;
        }

        var user = resolution.Value;

        var slug = Slug.FromName(request.Name!);
        if (slug is null)
        {
            return IdentityErrors.InvalidOrganizationName;
        }

        var slugTaken = await _dbContext.Organizations
            .IgnoreQueryFilters() // Soft-deleted organizations still reserve their slug.
            .AnyAsync(o => o.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

        if (slugTaken)
        {
            slug = Slug.WithUniquenessSuffix(slug);
        }

        var organization = new Organization
        {
            Name = request.Name!.Trim(),
            Slug = slug,
            BillingEmail = string.IsNullOrWhiteSpace(request.BillingEmail)
                ? user.Email
                : request.BillingEmail.Trim(),
        };

        var membership = new Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = OrganizationRole.Owner,
        };

        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(membership);

        _outbox.Stage(
            Topics.Organization,
            DomainEventEnvelope.Create(
                eventType: nameof(OrganizationCreated),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: organization.Id,
                payload: new OrganizationCreated
                {
                    OrganizationId = organization.Id,
                    OwnerUserId = user.Id,
                    Name = organization.Name,
                    CreatedUtc = _timeProvider.GetUtcNow(),
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The caller's membership list changed; drop the cached context.
        await _cache.InvalidateAsync(user.ExternalId, cancellationToken).ConfigureAwait(false);

        return new OrganizationCreatedResponse
        {
            Id = organization.Id,
            Name = organization.Name,
            Slug = organization.Slug,
            BillingEmail = organization.BillingEmail,
        };
    }
}
