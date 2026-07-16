using Microsoft.AspNetCore.Mvc;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Identity.Functions.Http;

/// <summary>
/// Maps application results to the standard response envelope (docs/16):
/// success → <c>{ data, correlationId, timestamp }</c>, failure → <c>{ error, correlationId }</c>
/// with the HTTP status derived from <see cref="ErrorKind"/>.
/// </summary>
public static class ApiResults
{
    public static IActionResult From<T>(Result<T> result, string correlationId, int successStatus = 200) =>
        result.Match(
            value => Success(value, correlationId, successStatus),
            error => Failure(error, correlationId));

    public static IActionResult Success<T>(T data, string correlationId, int status = 200) =>
        new ObjectResult(new
        {
            data,
            correlationId,
            timestamp = DateTimeOffset.UtcNow,
        })
        {
            StatusCode = status,
        };

    public static IActionResult Failure(Error error, string correlationId) =>
        new ObjectResult(new
        {
            error = new { code = error.Code, message = error.Message },
            correlationId,
        })
        {
            StatusCode = StatusCodeFor(error.Kind),
        };

    public static IActionResult ValidationFailure(
        IReadOnlyDictionary<string, string[]> details,
        string correlationId) =>
        new ObjectResult(new
        {
            error = new
            {
                code = "request.validation_failed",
                message = "The request body failed validation.",
                details,
            },
            correlationId,
        })
        {
            StatusCode = 400,
        };

    private static int StatusCodeFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => 400,
        ErrorKind.Unauthorized => 401,
        ErrorKind.Forbidden => 403,
        ErrorKind.NotFound => 404,
        ErrorKind.Conflict => 409,
        _ => 500,
    };
}
