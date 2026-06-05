using System.Text.Json;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class CompanyAccessMiddleware
{
    public const string ContextItemKey = "AuthenticatedCompanyContext";

    private readonly RequestDelegate _next;

    public CompanyAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AccessStoreService accessStoreService)
    {
        var isPublicAuthEndpoint = context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Equals("/api/auth/activate-invitation", StringComparison.OrdinalIgnoreCase);

        if (!context.Request.Path.StartsWithSegments("/api")
            || isPublicAuthEndpoint
            || context.Request.Path.StartsWithSegments("/api/platform"))
        {
            await _next(context);
            return;
        }

        try
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_required", "Bearer session token is required.");
                return;
            }

            var sessionContext = await accessStoreService.ResolveSessionAsync(authorization["Bearer ".Length..].Trim(), context.RequestAborted);
            if (sessionContext is null)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "invalid_session", "Session is invalid or expired.");
                return;
            }

            context.Items[ContextItemKey] = sessionContext;
            await _next(context);
        }
        catch (AccessDeniedException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, ex.Code, ex.Message);
        }
    }

    public static Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { code, message }));
    }
}
