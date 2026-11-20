using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Documents;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Domain.Notifications;
using FlowDesk.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Notifications;

public sealed class NotificationService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<IReadOnlyList<NotificationDto>> ListMineAsync(bool unreadOnly, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException("No autenticado.");
        var q = db.Notifications.AsNoTracking().Where(n => n.UserId == me);
        if (unreadOnly) q = q.Where(n => !n.IsRead);

        return await q.OrderByDescending(n => n.CreatedAt).Take(50)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.ExpedientId, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException("No autenticado.");
        var unread = await db.Notifications.Where(n => n.UserId == me && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) n.MarkAsRead(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Reacciones a eventos de negocio ejecutadas por el Worker: persiste notificaciones para los
/// usuarios involucrados y difunde el cambio en tiempo real.
/// </summary>
public sealed class BusinessEventReactions(
    IAppDbContext db,
    IRealtimeNotifier realtime,
    DocumentProcessingService processing,
    IClock clock)
{
    public async Task OnExpedientCreatedAsync(ExpedientCreated e, CancellationToken ct)
    {
        if (e.AssignedToId is { } assignee && assignee != e.CreatedById)
            await NotifyAsync(assignee, "Nuevo expediente asignado", $"Se le asignó el expediente {e.Code}.", e.ExpedientId, ct);

        await realtime.PublishAsync(Event(RealtimeEventTypes.ExpedientCreated, e.ExpedientId, null,
            ExpedientStatus.Received.ToString(), $"Expediente {e.Code} creado"), ct);
    }

    public async Task OnExpedientAssignedAsync(ExpedientAssigned e, CancellationToken ct)
    {
        var code = await CodeOfAsync(e.ExpedientId, ct);
        if (e.AssignedToId != e.AssignedById)
            await NotifyAsync(e.AssignedToId, "Expediente asignado", $"Se le asignó el expediente {code}.", e.ExpedientId, ct);

        await realtime.PublishAsync(Event(RealtimeEventTypes.ExpedientAssigned, e.ExpedientId, null, null,
            $"Expediente {code} reasignado"), ct);
    }

    public async Task OnExpedientStatusChangedAsync(ExpedientStatusChanged e, CancellationToken ct)
    {
        var message = $"El expediente {e.Code} pasó de «{Label(e.FromStatus)}» a «{Label(e.ToStatus)}».";
        var targets = new HashSet<Guid> { e.CreatedById };
        if (e.AssignedToId is { } a) targets.Add(a);
        targets.Remove(e.ChangedById);

        foreach (var userId in targets)
            await NotifyAsync(userId, "Cambio de estado", message, e.ExpedientId, ct);

        await realtime.PublishAsync(Event(RealtimeEventTypes.ExpedientStatusChanged, e.ExpedientId, null,
            e.ToStatus.ToString(), message), ct);
    }

    public async Task OnDocumentUploadedAsync(DocumentUploaded e, CancellationToken ct)
    {
        await realtime.PublishAsync(Event(RealtimeEventTypes.DocumentUploaded, e.ExpedientId, e.DocumentId,
            DocumentStatus.Pending.ToString(), "Documento recibido, procesando…"), ct);

        var result = await processing.ProcessAsync(e.DocumentId, e.Version, ct);
        if (result is null) return;

        var text = result.Status == DocumentStatus.Validated
            ? $"'{result.FileName}' validado correctamente."
            : $"'{result.FileName}' inválido: {result.Message}";

        if (result.Status == DocumentStatus.Invalid)
            await NotifyAsync(e.UploadedById, "Documento inválido", text, e.ExpedientId, ct);

        await realtime.PublishAsync(Event(RealtimeEventTypes.DocumentProcessed, e.ExpedientId, e.DocumentId,
            result.Status.ToString(), text), ct);
    }

    private async Task NotifyAsync(Guid userId, string title, string message, Guid expedientId, CancellationToken ct)
    {
        db.Notifications.Add(new Notification(userId, title, message, expedientId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        await realtime.PublishAsync(Event(RealtimeEventTypes.Notification, expedientId, null, null, message) with { TargetUserId = userId }, ct);
    }

    private Task<string> CodeOfAsync(Guid expedientId, CancellationToken ct) =>
        db.Expedients.AsNoTracking().Where(x => x.Id == expedientId).Select(x => x.Code).FirstOrDefaultAsync(ct)!;

    private static string Label(ExpedientStatus status) => status switch
    {
        ExpedientStatus.Received => "Recibido",
        ExpedientStatus.InValidation => "En validación",
        ExpedientStatus.PendingApproval => "Pendiente de aprobación",
        ExpedientStatus.CorrectionRequested => "Corrección solicitada",
        ExpedientStatus.Approved => "Aprobado",
        ExpedientStatus.Rejected => "Rechazado",
        _ => status.ToString(),
    };

    private RealtimeEvent Event(string type, Guid expedientId, Guid? documentId, string? status, string message) =>
        new(type, expedientId, documentId, status, message, clock.UtcNow);
}
