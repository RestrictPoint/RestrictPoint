namespace RestrictPoint.Common;

/// <summary>
/// Categorizes an <see cref="Error"/> so API layers can map failures to HTTP status codes
/// without inspecting error codes.
/// </summary>
public enum ErrorKind
{
    /// <summary>The request was malformed or failed validation (HTTP 400/422).</summary>
    Validation,

    /// <summary>The caller is not authenticated (HTTP 401).</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not permitted (HTTP 403).</summary>
    Forbidden,

    /// <summary>The requested resource does not exist (HTTP 404).</summary>
    NotFound,

    /// <summary>The request conflicts with current state (HTTP 409).</summary>
    Conflict,

    /// <summary>An unexpected failure occurred (HTTP 500).</summary>
    Unexpected,
}
