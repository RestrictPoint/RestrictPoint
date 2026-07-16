using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Abstractions;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Identity.Application.GetMe;

/// <summary>
/// GET /v1/identity/me — returns the caller's identity context, provisioning the user
/// just-in-time on first call. Serves from the Redis cache when possible (docs/11).
/// </summary>
public sealed class GetMeHandler
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserResolver _userResolver;
    private readonly IUserContextCache _cache;

    public GetMeHandler(IdentityDbContext dbContext, UserResolver userResolver, IUserContextCache cache)
    {
        _dbContext = dbContext;
        _userResolver = userResolver;
        _cache = cache;
    }

    public async Task<Result<MeResponse>> HandleAsync(
        RequestContext context,
        Guid? activeOrganizationHint,
        CancellationToken cancellationToken)
    {
        if (context.ExternalObjectId is not null)
        {
            var cached = await _cache.GetAsync(context.ExternalObjectId, cancellationToken)
                .ConfigureAwait(false);

            if (cached is not null)
            {
                return cached.IsActive
                    ? BuildResponse(cached, activeOrganizationHint)
                    : IdentityErrors.UserInactive;
            }
        }

        var resolution = await _userResolver.ResolveOrProvisionAsync(context, cancellationToken)
            .ConfigureAwait(false);

        if (resolution.IsFailure)
        {
            return resolution.Error!;
        }

        var user = resolution.Value;

        // Persists JIT provisioning (user + UserRegistered outbox row) when it occurred.
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var organizations = await LoadOrganizationsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var userContext = new CachedUserContext
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            Organizations = organizations,
        };

        await _cache.SetAsync(user.ExternalId, userContext, cancellationToken).ConfigureAwait(false);

        return BuildResponse(userContext, activeOrganizationHint);
    }

    private async Task<IReadOnlyList<OrganizationSummary>> LoadOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await _dbContext.Memberships
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.CreatedUtc)
            .Select(m => new OrganizationSummary
            {
                Id = m.OrganizationId,
                Name = m.Organization!.Name,
                Slug = m.Organization.Slug,
                Role = m.Role.ToString(),
                Status = m.Organization.Status.ToString(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static MeResponse BuildResponse(CachedUserContext userContext, Guid? activeOrganizationHint)
    {
        var organizations = userContext.Organizations;

        Guid? activeOrganizationId = organizations.Count > 0 ? organizations[0].Id : null;
        if (activeOrganizationHint is not null && organizations.Any(o => o.Id == activeOrganizationHint))
        {
            activeOrganizationId = activeOrganizationHint;
        }

        return new MeResponse
        {
            UserId = userContext.UserId,
            Email = userContext.Email,
            DisplayName = userContext.DisplayName,
            Organizations = userContext.Organizations,
            ActiveOrganizationId = activeOrganizationId,
        };
    }
}
