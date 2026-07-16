using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Application.CreateOrganization;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;
using Xunit;

namespace RestrictPoint.Api.Identity.Tests.Application;

public sealed class CreateOrganizationHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;

    public CreateOrganizationHandlerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Caller() => new()
    {
        CorrelationId = "corr-create",
        ExternalObjectId = "oid-owner",
        Email = "owner@contoso.com",
        DisplayName = "Owner",
    };

    private CreateOrganizationHandler CreateHandler(IdentityDbContext context) =>
        new(
            context,
            new UserResolver(context, new OutboxWriter(context), _time),
            new OutboxWriter(context),
            new NullUserContextCache(),
            _time);

    [Fact]
    public async Task Creates_organization_with_caller_as_owner()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Caller(),
            new CreateOrganizationRequest { Name = "Contoso Software" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("contoso-software", result.Value.Slug);
        Assert.Equal("owner@contoso.com", result.Value.BillingEmail); // Defaults to creator email.

        using var verification = _database.CreateContext();
        var membership = await verification.Memberships.SingleAsync();
        Assert.Equal(OrganizationRole.Owner, membership.Role);
        Assert.Equal(result.Value.Id, membership.OrganizationId);
    }

    [Fact]
    public async Task Stages_OrganizationCreated_event_with_organization_scope()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Caller(),
            new CreateOrganizationRequest { Name = "Contoso" },
            CancellationToken.None);

        using var verification = _database.CreateContext();
        var outbox = await verification.OutboxMessages
            .SingleAsync(m => m.EventType == "OrganizationCreated");

        Assert.Equal("OrganizationEvents", outbox.Topic);

        var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(
            outbox.Payload, DomainEventEnvelope.SerializerOptions)!;
        Assert.Equal(result.Value.Id, envelope.OrganizationId);
        Assert.Equal("corr-create", envelope.CorrelationId);
    }

    [Fact]
    public async Task Slug_collision_resolves_with_suffix()
    {
        using (var first = _database.CreateContext())
        {
            await CreateHandler(first).HandleAsync(
                Caller(),
                new CreateOrganizationRequest { Name = "Contoso" },
                CancellationToken.None);
        }

        using var second = _database.CreateContext();
        var result = await CreateHandler(second).HandleAsync(
            Caller(),
            new CreateOrganizationRequest { Name = "Contoso" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("contoso-", result.Value.Slug, StringComparison.Ordinal);
        Assert.NotEqual("contoso", result.Value.Slug);
        Assert.True(Slug.IsValid(result.Value.Slug));
    }

    [Fact]
    public async Task Unusable_name_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Caller(),
            new CreateOrganizationRequest { Name = "!!!" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.InvalidOrganizationName.Code, result.Error!.Code);
    }

    [Fact]
    public void Request_validator_rejects_missing_name_and_bad_email()
    {
        var validator = new CreateOrganizationRequestValidator();

        Assert.False(validator.Validate(new CreateOrganizationRequest()).IsValid);
        Assert.False(validator.Validate(
            new CreateOrganizationRequest { Name = "Ok", BillingEmail = "not-an-email" }).IsValid);
        Assert.True(validator.Validate(
            new CreateOrganizationRequest { Name = "Ok", BillingEmail = "a@b.com" }).IsValid);
    }
}
