using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Identity.Application.ListOrganizations;

/// <summary>GET /v1/identity/organizations — lists the caller's active memberships.</summary>
public sealed class ListOrganizationsHandler
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserResolver _userResolver;

    public ListOrganizationsHandler(IdentityDbContext dbContext, UserResolver userResolver)
    {
        _dbContext = dbContext;
        _userResolver = userResolver;
    }

    public async Task<Result<IReadOnlyList<OrganizationSummary>>> HandleAsync(
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var resolution = await _userResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return resolution.Error!;
        }

        IReadOnlyList<OrganizationSummary> organizations = await _dbContext.Memberships
            .Where(m => m.UserId == resolution.Value.Id && m.Status == MembershipStatus.Active)
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

        return Result.Success(organizations);
    }
}
