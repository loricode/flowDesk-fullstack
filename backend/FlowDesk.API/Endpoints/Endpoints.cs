using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Documents;
using FlowDesk.Application.Expedients;
using FlowDesk.Application.Identity;
using FlowDesk.Application.Notifications;
using FlowDesk.Application.Reporting;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Shared.Contracts;
using FlowDesk.Shared.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using P = FlowDesk.Shared.Contracts.FlowDeskPermissions;

namespace FlowDesk.API.Endpoints;

/// <summary>
/// Endpoints REST agrupados por módulo de negocio. Cada endpoint declara el permiso requerido:
/// la UI puede ocultar botones, pero la autorización real ocurre aquí y en el dominio.
/// </summary>
public static class FlowDeskEndpoints
{
    public static void MapFlowDeskEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        MapAuth(api.MapGroup("/auth").WithTags("Identity"));
        MapUsers(api.MapGroup("").WithTags("Identity"));
        MapExpedients(api.MapGroup("/expedients").WithTags("Expedients"));
        MapDocuments(api.MapGroup("/documents").WithTags("DocumentManagement"));
        MapReporting(api.MapGroup("").WithTags("Reporting"));
        MapNotifications(api.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization());
        MapInternal(app.MapGroup("/internal").ExcludeFromDescription());

        app.MapGet("/health", () => Results.Ok(new { status = "ok", at = DateTimeOffset.UtcNow })).AllowAnonymous();
    }

    private static void MapAuth(RouteGroupBuilder g)
    {
        g.MapPost("/login", (LoginRequest request, AuthService auth, CancellationToken ct) => auth.LoginAsync(request, ct))
            .AllowAnonymous()
            .RequireRateLimiting("login");

        g.MapGet("/me", (AuthService auth, CancellationToken ct) => auth.GetCurrentAsync(ct))
            .RequireAuthorization();
    }

    private static void MapUsers(RouteGroupBuilder g)
    {
        g.MapGet("/users", (UserService users, CancellationToken ct) => users.ListAsync(ct))
            .RequireAuthorization(P.UsersManage);

        g.MapPost("/users", async (CreateUserRequest request, UserService users, CancellationToken ct) =>
            {
                var created = await users.CreateAsync(request, ct);
                return Results.Created($"/api/users/{created.Id}", created);
            })
            .RequireAuthorization(P.UsersManage);

        g.MapPut("/users/{id:guid}", (Guid id, UpdateUserRequest request, UserService users, CancellationToken ct) =>
                users.UpdateAsync(id, request, ct))
            .RequireAuthorization(P.UsersManage);

        g.MapGet("/users/lookup", (string? permission, UserService users, CancellationToken ct) =>
                users.LookupAsync(permission, ct))
            .RequireAuthorization(P.ExpedientsRead);

        g.MapGet("/roles", (UserService users, CancellationToken ct) => users.ListRolesAsync(ct))
            .RequireAuthorization(P.UsersManage);
    }

    private static void MapExpedients(RouteGroupBuilder g)
    {
        g.MapGet("/", ([AsParameters] ExpedientQueryParameters q, ExpedientService service, CancellationToken ct) =>
                service.SearchAsync(new ExpedientQuery(q.Status, q.Search, q.AssignedToMe ?? false, q.Priority, q.Page ?? 1, q.PageSize ?? 20), ct))
            .RequireAuthorization(P.ExpedientsRead);

        g.MapGet("/{id:guid}", (Guid id, ExpedientService service, CancellationToken ct) => service.GetAsync(id, ct))
            .RequireAuthorization(P.ExpedientsRead);

        g.MapPost("/", async (CreateExpedientRequest request, ExpedientService service, CancellationToken ct) =>
            {
                var created = await service.CreateAsync(request, ct);
                return Results.Created($"/api/expedients/{created.Id}", created);
            })
            .RequireAuthorization(P.ExpedientsCreate);

        g.MapPost("/{id:guid}/assign", (Guid id, AssignExpedientRequest request, ExpedientService service, CancellationToken ct) =>
                service.AssignAsync(id, request, ct))
            .RequireAuthorization(P.ExpedientsAssign);

        // El permiso específico de cada transición lo valida el motor de workflow en el dominio.
        g.MapPost("/{id:guid}/transitions", (Guid id, ExecuteTransitionRequest request, ExpedientService service, CancellationToken ct) =>
                service.ExecuteTransitionAsync(id, request, ct))
            .RequireAuthorization(P.ExpedientsRead);

        g.MapPost("/{id:guid}/documents", async (
                Guid id, IFormFile file, [FromForm] string? source, DocumentService documents, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                var created = await documents.UploadAsync(id, file.FileName, stream, source, ct);
                return Results.Created($"/api/documents/{created.Id}", created);
            })
            .RequireAuthorization(P.DocumentsUpload)
            .DisableAntiforgery() // API con JWT en cabecera: no usa cookies, no aplica CSRF.
            .WithMetadata(new RequestSizeLimitAttribute(FileSignatureValidator.MaxFileSizeBytes + 1024 * 1024));
    }

    private static void MapDocuments(RouteGroupBuilder g)
    {
        g.MapGet("/{id:guid}", (Guid id, DocumentService documents, CancellationToken ct) => documents.GetAsync(id, ct))
            .RequireAuthorization(P.ExpedientsRead);

        g.MapGet("/{id:guid}/content", async (Guid id, int? version, DocumentService documents, CancellationToken ct) =>
            {
                var content = await documents.OpenAsync(id, version, ct);
                return Results.File(content.Content, content.ContentType, content.FileName, enableRangeProcessing: true);
            })
            .RequireAuthorization(P.ExpedientsRead);
    }

    private static void MapReporting(RouteGroupBuilder g)
    {
        g.MapGet("/dashboard", (DashboardService dashboard, CancellationToken ct) => dashboard.GetAsync(ct))
            .RequireAuthorization(P.ExpedientsRead);

        g.MapGet("/audit", (Guid? entityId, string? action, int? page, int? pageSize, AuditQueryService audit, CancellationToken ct) =>
                audit.SearchAsync(entityId, action, page ?? 1, pageSize ?? 25, ct))
            .RequireAuthorization(P.AuditRead);
    }

    private static void MapNotifications(RouteGroupBuilder g)
    {
        g.MapGet("/", (bool? unreadOnly, NotificationService notifications, CancellationToken ct) =>
            notifications.ListMineAsync(unreadOnly ?? false, ct));

        g.MapPost("/read-all", async (NotificationService notifications, CancellationToken ct) =>
        {
            await notifications.MarkAllReadAsync(ct);
            return Results.NoContent();
        });
    }

    /// <summary>Relé interno: el Worker entrega eventos para difundirlos por SignalR.</summary>
    private static void MapInternal(RouteGroupBuilder g)
    {
        g.MapPost("/realtime", async (
                HttpRequest http, RealtimeEvent realtimeEvent, IRealtimeNotifier notifier,
                IOptions<RealtimeRelayOptions> options, CancellationToken ct) =>
            {
                var key = options.Value.InternalApiKey;
                if (string.IsNullOrEmpty(key) || http.Headers[HttpRealtimeNotifier.HeaderName] != key)
                    return Results.Unauthorized();

                await notifier.PublishAsync(realtimeEvent, ct);
                return Results.Accepted();
            })
            .AllowAnonymous();
    }

    public sealed record ExpedientQueryParameters(
        string? Status, string? Search, bool? AssignedToMe, string? Priority, int? Page, int? PageSize);
}
