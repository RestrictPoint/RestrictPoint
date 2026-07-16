using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Abstractions;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Application.Events;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Identity.Application.InviteMember;

/// <summary>Input validation for POST /v1/identity/organizations/{id}/invite.</summary>
public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(r => r.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(Invitation.EmailMaxLength);

        RuleFor(r => r.Role)
            .NotEmpty();
    }
}

/// <summary>
/// POST /v1/identity/organizations/{id}/invite — invites an email address to join an
/// organization. Requires the CanManageMembers policy. The invitation token is returned
/// exactly once; only its SHA-256 hash is persisted.
/// </summary>
public sealed class InviteMemberHandler
{
    private const int TokenSizeBytes = 32;

    private readonly IdentityDbContext _dbContext;
    private readonly UserResolver _userResolver;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public InviteMemberHandler(
        IdentityDbContext dbContext,
        UserResolver userResolver,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _userResolver = userResolver;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<Result<InvitationCreatedResponse>> HandleAsync(
        RequestContext context,
        Guid organizationId,
        InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        var resolution = await _userResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return resolution.Error!;
        }

        var caller = resolution.Value;

        var authorization = await AuthorizeAsync(caller.Id, organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        if (!OrganizationRoleExtensions.TryParse(request.Role, out var role))
        {
            return IdentityErrors.InvalidRole;
        }

        if (role == OrganizationRole.Owner)
        {
            return IdentityErrors.CannotInviteOwner;
        }

        var email = request.Email!.Trim();
        var utcNow = _timeProvider.GetUtcNow();

        var conflict = await CheckConflictsAsync(organizationId, email, utcNow, cancellationToken)
            .ConfigureAwait(false);

        if (conflict is not null)
        {
            return conflict;
        }

        var token = GenerateToken();

        var invitation = new Invitation
        {
            OrganizationId = organizationId,
            Email = email,
            Role = role,
            TokenHash = HashToken(token),
            InvitedByUserId = caller.Id,
            ExpiresUtc = utcNow + Invitation.Lifetime,
        };

        _dbContext.Invitations.Add(invitation);

        _outbox.Stage(
            Topics.Identity,
            DomainEventEnvelope.Create(
                eventType: nameof(UserInvited),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: organizationId,
                payload: new UserInvited
                {
                    InvitationId = invitation.Id,
                    OrganizationId = organizationId,
                    Email = email,
                    InvitedByUserId = caller.Id,
                    ExpiresUtc = invitation.ExpiresUtc,
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new InvitationCreatedResponse
        {
            InvitationId = invitation.Id,
            Email = email,
            Role = role.ToString(),
            ExpiresUtc = invitation.ExpiresUtc,
        };
    }

    /// <summary>Computes the SHA-256 hash of an invitation token for storage or lookup.</summary>
    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenSizeBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private async Task<Result<Result.Unit>> AuthorizeAsync(
        Guid callerId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var membership = await _dbContext.Memberships
            .Include(m => m.Organization)
            .SingleOrDefaultAsync(
                m => m.UserId == callerId
                    && m.OrganizationId == organizationId
                    && m.Status == MembershipStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);

        // Non-members receive NotFound, not Forbidden, to avoid disclosing organization existence.
        if (membership is null)
        {
            return IdentityErrors.OrganizationNotFound;
        }

        if (membership.Organization!.Status != OrganizationStatus.Active)
        {
            return IdentityErrors.OrganizationSuspended;
        }

        if (!Domain.Policies.Grants(Domain.Policies.CanManageMembers, membership.Role))
        {
            return IdentityErrors.NotAuthorizedForOrganization;
        }

        return Result.Success();
    }

    private async Task<Error?> CheckConflictsAsync(
        Guid organizationId,
        string email,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var alreadyMember = await _dbContext.Memberships
            .AnyAsync(
                m => m.OrganizationId == organizationId
                    && m.Status == MembershipStatus.Active
                    && m.User!.Email == email,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyMember)
        {
            return IdentityErrors.AlreadyMember;
        }

        var pendingInvitation = await _dbContext.Invitations
            .AnyAsync(
                i => i.OrganizationId == organizationId
                    && i.Email == email
                    && i.AcceptedUtc == null
                    && i.ExpiresUtc > utcNow,
                cancellationToken)
            .ConfigureAwait(false);

        return pendingInvitation ? IdentityErrors.InvitationAlreadyPending : null;
    }
}
