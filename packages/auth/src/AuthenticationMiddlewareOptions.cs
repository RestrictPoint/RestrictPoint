namespace RestrictPoint.Auth;

/// <summary>
/// Options for <see cref="AuthenticationMiddleware"/>. Anonymous functions are limited to
/// platform health probes by default; endpoints credentialed by other means (e.g. the SDK
/// license validation path, where the signed license token is the credential) opt in here.
/// </summary>
public sealed record AuthenticationMiddlewareOptions
{
    /// <summary>Function names (case-insensitive) that bypass bearer-token authentication.</summary>
    public IReadOnlyList<string> AnonymousFunctions { get; init; } = ["HealthLive", "HealthReady"];
}
