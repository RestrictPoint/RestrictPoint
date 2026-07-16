using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Application.InviteMember;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;
using Xunit;

namespace RestrictPoint.Api.Identity.Tests.Application;

public sealed class InviteMemberHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly Guid _organizationId;
    private readonly Guid _ownerUserId;

    public InviteMemberHandlerTests()
    {
        _database = new TestDatabase(_time);

        using var seed = _database.CreateContext();

        var owner = new User
        {
            Email = "owner@contoso.com",
            DisplayName = "Owner",
            ExternalProvider = UserResolver.ExternalProviderName,
            ExternalId = "oid-owner",
        };
        var readOnly = new User
        {
            Email = "viewer@contoso.com",
            DisplayName = "Viewer",
            ExternalProvider = UserResolver.ExternalProviderName,
            ExternalId = "oid-viewer",
        };
        var organization = new Organization
        {
            Name = "Contoso",
            Slug = "contoso",
            BillingEmail = "billing@contoso.com",
        };

        seed.Users.AddRange(owner, readOnly);
        seed.Organizations.Add(organization);
        seed.Memberships.Add(new Membership
        {
            UserId = owner.Id,
            OrganizationId = organization.Id,
            Role = OrganizationRole.Owner,
        });
        seed.Memberships.Add(new Membership
        {
            UserId = readOnly.Id,
            OrganizationId = organization.Id,
            Role = OrganizationRole.ReadOnly,
        });
        seed.SaveChanges();

        _organizationId = organization.Id;
        _ownerUserId = owner.Id;
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext CallerWithOid(string oid) => new()
    {
        CorrelationId = "corr-invite",
        ExternalObjectId = oid,
        Email = "caller@contoso.com",
        DisplayName = "Caller",
    };

    private InviteMemberHandler CreateHandler(IdentityDbContext context) =>
        new(context, new UserResolver(context, new OutboxWriter(context), _time), new OutboxWriter(context), _time);

    private static InviteMemberRequest Request(string email = "new@contoso.com", string role = "Developer") =>
        new() { Email = email, Role = role };

    [Fact]
    public async Task Owner_can_invite_and_token_hash_is_stored()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            CallerWithOid("oid-owner"), _organizationId, Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Developer", result.Value.Role);
        Assert.Equal(_time.UtcNow + Invitation.Lifetime, result.Value.ExpiresUtc);

        using var verification = _database.CreateContext();
        var invitation = await verification.Invitations.SingleAsync();
        Assert.Equal(_ownerUserId, invitation.InvitedByUserId);
        Assert.Equal(64, invitation.TokenHash.Length); // SHA-256 hex (32 bytes) — never the raw token.

        var outbox = await verification.OutboxMessages.SingleAsync(m => m.EventType == "UserInvited");
        Assert.Equal("IdentityEvents", outbox.Topic);
        Assert.DoesNotContain(invitation.TokenHash, outbox.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadOnly_member_is_forbidden()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            CallerWithOid("oid-viewer"), _organizationId, Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.NotAuthorizedForOrganization.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Non_member_receives_not_found()
    {
        using var seed = _database.CreateContext();
        seed.Users.Add(new User
        {
            Email = "outsider@other.com",
            DisplayName = "Outsider",
            ExternalProvider = UserResolver.ExternalProviderName,
            ExternalId = "oid-outsider",
        });
        await seed.SaveChangesAsync();

        using var context = _database.CreateContext();
        var result = await CreateHandler(context).HandleAsync(
            CallerWithOid("oid-outsider"), _organizationId, Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.OrganizationNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Owner_role_cannot_be_granted_by_invitation()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            CallerWithOid("oid-owner"), _organizationId, Request(role: "Owner"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.CannotInviteOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Unknown_role_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            CallerWithOid("oid-owner"), _organizationId, Request(role: "Wizard"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.InvalidRole.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Existing_member_email_conflicts()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            CallerWithOid("oid-owner"),
            _organizationId,
            Request(email: "viewer@contoso.com"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.AlreadyMember.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Pending_invitation_conflicts_but_expired_one_does_not()
    {
        using (var first = _database.CreateContext())
        {
            var initial = await CreateHandler(first).HandleAsync(
                CallerWithOid("oid-owner"), _organizationId, Request(), CancellationToken.None);
            Assert.True(initial.IsSuccess);
        }

        using (var duplicate = _database.CreateContext())
        {
            var conflict = await CreateHandler(duplicate).HandleAsync(
                CallerWithOid("oid-owner"), _organizationId, Request(), CancellationToken.None);
            Assert.Equal(IdentityErrors.InvitationAlreadyPending.Code, conflict.Error!.Code);
        }

        _time.UtcNow += Invitation.Lifetime + TimeSpan.FromMinutes(1);

        using var afterExpiry = _database.CreateContext();
        var retry = await CreateHandler(afterExpiry).HandleAsync(
            CallerWithOid("oid-owner"), _organizationId, Request(), CancellationToken.None);

        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public void Token_hashing_is_deterministic_and_one_way()
    {
        var hashA = InviteMemberHandler.HashToken("token-a");
        var hashB = InviteMemberHandler.HashToken("token-b");

        Assert.Equal(InviteMemberHandler.HashToken("token-a"), hashA);
        Assert.NotEqual(hashA, hashB);
        Assert.DoesNotContain("token-a", hashA, StringComparison.OrdinalIgnoreCase);
    }
}
