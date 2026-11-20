using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Security;

namespace FlowDesk.API.Infrastructure;

/// <summary>Identidad del usuario autenticado a partir de los claims del JWT.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId =>
        Guid.TryParse(Context?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? FullName => Context?.User.FindFirst("name")?.Value;

    public IReadOnlyCollection<string> Permissions =>
        Context?.User.FindAll(FlowDeskClaims.Permission).Select(c => c.Value).ToHashSet() ?? [];

    public string? CorrelationId =>
        Context?.Items[CorrelationIdMiddleware.ItemKey] as string ?? Context?.TraceIdentifier;
}

/// <summary>Propaga un identificador de correlación entre clientes, API, Outbox y Worker.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault() is { Length: > 0 and <= 64 } incoming
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await next(context);
    }
}
