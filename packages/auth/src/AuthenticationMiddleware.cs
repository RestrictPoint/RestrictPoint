using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using RestrictPoint.Common;

namespace RestrictPoint.Auth;

/// <summary>
/// Functions worker middleware enforcing bearer-token authentication on every HTTP trigger.
/// Anonymous functions are not permitted (docs/08); the only exception is the health probe,
/// which platform infrastructure must reach without credentials.
/// On success, a <see cref="RequestContext"/> is stored in <see cref="FunctionContext.Items"/>.
/// </summary>
public sealed partial class AuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    /// <summary>Key under which the request context is stored in FunctionContext.Items.</summary>
    public const string RequestContextKey = "RestrictPoint.RequestContext";

    private const string CorrelationHeader = "x-correlation-id";

    private readonly IJwtValidator _jwtValidator;
    private readonly AuthenticationMiddlewareOptions _options;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    public AuthenticationMiddleware(
        IJwtValidator jwtValidator,
        AuthenticationMiddlewareOptions options,
        ILogger<AuthenticationMiddleware> logger)
    {
        _jwtValidator = jwtValidator;
        _options = options;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            // Non-HTTP trigger (timer, service bus): no user authentication applies.
            await next(context).ConfigureAwait(false);
            return;
        }

        var correlationId = ResolveCorrelationId(httpContext);
        httpContext.Response.Headers[CorrelationHeader] = correlationId;

        if (_options.AnonymousFunctions.Contains(context.FunctionDefinition.Name, StringComparer.OrdinalIgnoreCase))
        {
            context.Items[RequestContextKey] = new RequestContext { CorrelationId = correlationId };
            await next(context).ConfigureAwait(false);
            return;
        }

        var bearerToken = ExtractBearerToken(httpContext);
        if (bearerToken is null)
        {
            await WriteUnauthorizedAsync(httpContext, correlationId, "auth.missing_token",
                "An Authorization: Bearer header is required.").ConfigureAwait(false);
            return;
        }

        var validation = await _jwtValidator.ValidateAsync(bearerToken, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (validation.IsFailure)
        {
            LogAuthenticationFailed(_logger, validation.Error!.Code, correlationId);

            await WriteUnauthorizedAsync(httpContext, correlationId, validation.Error.Code, validation.Error.Message)
                .ConfigureAwait(false);
            return;
        }

        var principal = validation.Value;
        context.Items[RequestContextKey] = new RequestContext
        {
            CorrelationId = correlationId,
            ExternalObjectId = principal.ObjectId,
            Email = principal.Email,
            DisplayName = principal.DisplayName,
        };

        await next(context).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Authentication failed: {ErrorCode} correlationId={CorrelationId}")]
    private static partial void LogAuthenticationFailed(ILogger logger, string errorCode, string correlationId);

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        var incoming = httpContext.Request.Headers[CorrelationHeader].ToString();
        return string.IsNullOrWhiteSpace(incoming) || incoming.Length > 128
            ? Guid.NewGuid().ToString()
            : incoming;
    }

    private static string? ExtractBearerToken(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && header.Length > prefix.Length
            ? header[prefix.Length..].Trim()
            : null;
    }

    private static async Task WriteUnauthorizedAsync(
        HttpContext httpContext,
        string correlationId,
        string code,
        string message)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.Headers.WWWAuthenticate = "Bearer";

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                error = new { code, message },
                correlationId,
            },
            httpContext.RequestAborted).ConfigureAwait(false);
    }
}

/// <summary>Accessor for the request context established by <see cref="AuthenticationMiddleware"/>.</summary>
public static class FunctionContextExtensions
{
    /// <summary>
    /// Returns the authenticated request context. Throws when middleware has not run,
    /// which indicates a host configuration defect rather than a runtime condition.
    /// </summary>
    public static RequestContext GetRequestContext(this FunctionContext context) =>
        context.Items.TryGetValue(AuthenticationMiddleware.RequestContextKey, out var value)
        && value is RequestContext requestContext
            ? requestContext
            : throw new InvalidOperationException(
                "RequestContext is missing. Ensure AuthenticationMiddleware is registered.");
}
