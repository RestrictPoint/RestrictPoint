using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Events;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Identity.Application.Common;

/// <summary>
/// Resolves the platform user for an authenticated caller, provisioning just-in-time on
/// first contact (docs/11: SPFx and portal callers map external identities to platform users).
/// Provisioning stages a UserRegistered event; the caller owns the SaveChanges boundary.
/// </summary>
public sealed class UserResolver
{
    public const string ExternalProviderName = "EntraExternalId";

    private readonly IdentityDbContext _dbContext;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public UserResolver(IdentityDbContext dbContext, IOutboxWriter outbox, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Finds the user for the caller, creating one when none exists. Returns
    /// <see cref="IdentityErrors.UserInactive"/> for deactivated accounts.
    /// </summary>
    public async Task<Result<User>> ResolveOrProvisionAsync(
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (context.ExternalObjectId is null || context.Email is null)
        {
            return Error.Unauthorized("auth.missing_claims", "The access token is missing required claims.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                u => u.ExternalProvider == ExternalProviderName && u.ExternalId == context.ExternalObjectId,
                cancellationToken)
            .ConfigureAwait(false);

        if (user is not null)
        {
            return user.IsActive ? user : IdentityErrors.UserInactive;
        }

        user = new User
        {
            Email = context.Email,
            DisplayName = context.DisplayName ?? context.Email,
            ExternalProvider = ExternalProviderName,
            ExternalId = context.ExternalObjectId,
        };

        _dbContext.Users.Add(user);

        _outbox.Stage(
            Topics.Identity,
            DomainEventEnvelope.Create(
                eventType: nameof(UserRegistered),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: Guid.Empty, // Registration precedes any organization scope.
                payload: new UserRegistered
                {
                    UserId = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    IdentityProvider = ExternalProviderName,
                    RegisteredUtc = _timeProvider.GetUtcNow(),
                },
                timeProvider: _timeProvider));

        return user;
    }

    /// <summary>
    /// Finds the user for the caller without provisioning. Returns
    /// <see cref="IdentityErrors.UserNotProvisioned"/> when the user does not exist.
    /// </summary>
    public async Task<Result<User>> ResolveAsync(RequestContext context, CancellationToken cancellationToken)
    {
        if (context.ExternalObjectId is null)
        {
            return Error.Unauthorized("auth.missing_claims", "The access token is missing required claims.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                u => u.ExternalProvider == ExternalProviderName && u.ExternalId == context.ExternalObjectId,
                cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return IdentityErrors.UserNotProvisioned;
        }

        return user.IsActive ? user : IdentityErrors.UserInactive;
    }
}
