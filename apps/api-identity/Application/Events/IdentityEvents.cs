namespace RestrictPoint.Api.Identity.Application.Events;

/// <summary>Service Bus topics this service publishes to. Only Identity may publish these.</summary>
public static class Topics
{
    public const string Identity = "IdentityEvents";
    public const string Organization = "OrganizationEvents";
}

/// <summary>Event metadata constants.</summary>
public static class EventMetadata
{
    public const string Publisher = "identity";
    public const string Version10 = "1.0";
}

/// <summary>UserRegistered v1.0 payload (docs/20).</summary>
public sealed record UserRegistered
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public required string IdentityProvider { get; init; }

    public required DateTimeOffset RegisteredUtc { get; init; }
}

/// <summary>UserInvited v1.0 payload (docs/20).</summary>
public sealed record UserInvited
{
    public required Guid InvitationId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Email { get; init; }

    public required Guid InvitedByUserId { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }
}

/// <summary>OrganizationCreated v1.0 payload (docs/20). Initializes all downstream services.</summary>
public sealed record OrganizationCreated
{
    public required Guid OrganizationId { get; init; }

    public required Guid OwnerUserId { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
