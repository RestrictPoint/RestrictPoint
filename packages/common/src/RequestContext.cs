namespace RestrictPoint.Common;

/// <summary>
/// Ambient request context flowing through every operation: correlation, caller identity,
/// and tenant scope. Constructed once per request by the authentication middleware.
/// </summary>
public sealed record RequestContext
{
    /// <summary>Correlation ID propagated across services and into published events.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>The authenticated platform user, when the request is user-initiated.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Entra object id of the caller (external identity key).</summary>
    public string? ExternalObjectId { get; init; }

    /// <summary>Email claim of the caller.</summary>
    public string? Email { get; init; }

    /// <summary>Display name claim of the caller.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Active organization scope for the request, when resolved.</summary>
    public Guid? OrganizationId { get; init; }
}
