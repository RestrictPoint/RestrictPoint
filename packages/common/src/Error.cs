namespace RestrictPoint.Common;

/// <summary>
/// An expected failure with a stable machine-readable code and a human-readable message.
/// Error codes are part of the public API contract and must not change casually.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "'Error' collides only with a Visual Basic keyword; the platform is C#-only " +
        "and the name is the established convention for result-pattern error types.")]
public sealed record Error(string Code, string Message, ErrorKind Kind)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorKind.Unexpected);
}
