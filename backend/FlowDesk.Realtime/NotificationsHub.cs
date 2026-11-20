using System.Security.Claims;
using FlowDesk.Application.Abstractions;
using FlowDesk.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.Realtime;

/// <summary>Contrato tipado de los métodos que el servidor invoca en los clientes.</summary>
public interface IFlowDeskClient
{
    Task ReceiveEvent(RealtimeEvent realtimeEvent);
}

/// <summary>
/// Hub de notificaciones. SignalR negocia el transporte y usa WebSockets cuando está disponible.
/// Grupos: "user:{id}" (notificaciones personales) y "expedient:{id}" (detalle abierto).
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub<IFlowDeskClient>
{
    public const string Route = "/hubs/notifications";

    public static string UserGroup(Guid userId) => $"user:{userId}";
    public static string ExpedientGroup(Guid expedientId) => $"expedient:{expedientId}";

    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    public Task JoinExpedient(Guid expedientId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ExpedientGroup(expedientId));

    public Task LeaveExpedient(Guid expedientId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ExpedientGroup(expedientId));
}

public sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub, IFlowDeskClient> hub) : IRealtimeNotifier
{
    public Task PublishAsync(RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        if (realtimeEvent.TargetUserId is { } userId)
            return hub.Clients.Group(NotificationsHub.UserGroup(userId)).ReceiveEvent(realtimeEvent);

        // Cambios de bandejas y tableros: todos los usuarios autenticados refrescan sus vistas.
        return hub.Clients.All.ReceiveEvent(realtimeEvent);
    }
}

public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddFlowDeskRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
        return services;
    }
}
