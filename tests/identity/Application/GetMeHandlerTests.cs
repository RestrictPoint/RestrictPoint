using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Abstractions;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Application.GetMe;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;
using Xunit;

namespace RestrictPoint.Api.Identity.Tests.Application;

public sealed class GetMeHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;

    public GetMeHandlerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Caller(string oid = "oid-123") => new()
    {
        CorrelationId = "corr-1",
        ExternalObjectId = oid,
        Email = "dev@contoso.com",
        DisplayName = "Dev User",
    };

    private GetMeHandler CreateHandler(IdentityDbContext context, IUserContextCache? cache = null) =>
        new(
            context,
            new UserResolver(context, new OutboxWriter(context), _time),
            cache ?? new NullUserContextCache());

    [Fact]
    public async Task Provisions_user_just_in_time_and_stages_UserRegistered()
    {
        using var context = _database.CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(Caller(), null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("dev@contoso.com", result.Value.Email);
        Assert.Empty(result.Value.Organizations);

        using var verification = _database.CreateContext();
        var user = await verification.Users.SingleAsync();
        Assert.Equal("oid-123", user.ExternalId);
        Assert.Equal(UserResolver.ExternalProviderName, user.ExternalProvider);

        var outbox = await verification.OutboxMessages.SingleAsync();
        Assert.Equal("UserRegistered", outbox.EventType);
        Assert.Equal("IdentityEvents", outbox.Topic);

        var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(
            outbox.Payload, DomainEventEnvelope.SerializerOptions);
        Assert.NotNull(envelope);
        Assert.Equal("corr-1", envelope.CorrelationId);
        Assert.Equal("identity", envelope.Publisher);
    }

    [Fact]
    public async Task Second_call_reuses_existing_user_without_new_events()
    {
        using (var first = _database.CreateContext())
        {
            await CreateHandler(first).HandleAsync(Caller(), null, CancellationToken.None);
        }

        using var second = _database.CreateContext();
        var result = await CreateHandler(second).HandleAsync(Caller(), null, CancellationToken.None);

        Assert.True(result.IsSuccess);

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.Users.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Returns_memberships_with_roles()
    {
        Guid userId;
        using (var seed = _database.CreateContext())
        {
            var provision = await CreateHandler(seed).HandleAsync(Caller(), null, CancellationToken.None);
            userId = provision.Value.UserId;

            var organization = new Organization
            {
                Name = "Contoso",
                Slug = "contoso",
                BillingEmail = "billing@contoso.com",
            };
            seed.Organizations.Add(organization);
            seed.Memberships.Add(new Membership
            {
                UserId = userId,
                OrganizationId = organization.Id,
                Role = OrganizationRole.Owner,
            });
            await seed.SaveChangesAsync();
        }

        using var context = _database.CreateContext();
        var result = await CreateHandler(context).HandleAsync(Caller(), null, CancellationToken.None);

        var organizationSummary = Assert.Single(result.Value.Organizations);
        Assert.Equal("contoso", organizationSummary.Slug);
        Assert.Equal("Owner", organizationSummary.Role);
        Assert.Equal(organizationSummary.Id, result.Value.ActiveOrganizationId);
    }

    [Fact]
    public async Task Cache_hit_skips_database_entirely()
    {
        var cached = new CachedUserContext
        {
            UserId = Guid.NewGuid(),
            Email = "cached@contoso.com",
            DisplayName = "Cached",
            IsActive = true,
            Organizations = [],
        };

        using var context = _database.CreateContext();
        var handler = CreateHandler(context, new StubCache(cached));

        var result = await handler.HandleAsync(Caller(), null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("cached@contoso.com", result.Value.Email);
        Assert.Equal(0, await context.Users.CountAsync()); // No JIT provisioning occurred.
    }

    [Fact]
    public async Task Inactive_user_is_rejected()
    {
        using (var seed = _database.CreateContext())
        {
            await CreateHandler(seed).HandleAsync(Caller(), null, CancellationToken.None);
            var user = await seed.Users.SingleAsync();
            user.IsActive = false;
            await seed.SaveChangesAsync();
        }

        using var context = _database.CreateContext();
        var result = await CreateHandler(context).HandleAsync(Caller(), null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.UserInactive.Code, result.Error!.Code);
    }

    private sealed class StubCache : IUserContextCache
    {
        private readonly CachedUserContext _context;

        public StubCache(CachedUserContext context) => _context = context;

        public Task<CachedUserContext?> GetAsync(string externalObjectId, CancellationToken cancellationToken) =>
            Task.FromResult<CachedUserContext?>(_context);

        public Task SetAsync(string externalObjectId, CachedUserContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task InvalidateAsync(string externalObjectId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
