using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Expedients;
using FlowDesk.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Reporting;

public sealed class DashboardService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var since30 = now.AddDays(-30);
        var since90 = now.AddDays(-90);
        var me = currentUser.UserId;

        var byStatus = await db.Expedients.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(ExpedientStatus s) => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        var open = byStatus.Where(x => !x.Status.IsFinal()).Sum(x => x.Count);

        var assignedToMe = me is null ? 0 : await db.Expedients.AsNoTracking()
            .CountAsync(e => e.AssignedToId == me
                && e.Status != ExpedientStatus.Approved && e.Status != ExpedientStatus.Rejected, ct);

        var closed = await db.Expedients.AsNoTracking()
            .Where(e => e.ClosedAt != null && e.ClosedAt >= since90)
            .Select(e => new { e.Status, e.CreatedAt, ClosedAt = e.ClosedAt!.Value })
            .ToListAsync(ct);

        // Actividad de negocio: se excluyen accesos (logins y consultas de documentos).
        string[] noise = [AuditActions.Login, AuditActions.LoginFailed, AuditActions.DocumentDownloaded];
        var recent = await db.AuditLogs.AsNoTracking()
            .Where(a => !noise.Contains(a.Action))
            .OrderByDescending(a => a.OccurredAt).Take(10)
            .Select(a => new AuditEntryDto(a.Id, a.OccurredAt, a.UserName, a.Action, a.EntityType, a.EntityId,
                a.PreviousState, a.NewState, a.Details, a.CorrelationId))
            .ToListAsync(ct);

        return new DashboardDto(
            OpenExpedients: open,
            AssignedToMe: assignedToMe,
            PendingApproval: Count(ExpedientStatus.PendingApproval),
            ApprovedLast30Days: closed.Count(c => c.Status == ExpedientStatus.Approved && c.ClosedAt >= since30),
            RejectedLast30Days: closed.Count(c => c.Status == ExpedientStatus.Rejected && c.ClosedAt >= since30),
            AverageResolutionHours: closed.Count == 0 ? null : Math.Round(closed.Average(c => (c.ClosedAt - c.CreatedAt).TotalHours), 1),
            ByStatus: Enum.GetValues<ExpedientStatus>().ToDictionary(s => s.ToString(), Count),
            RecentActivity: recent);
    }
}

public sealed class AuditQueryService(IAppDbContext db)
{
    public async Task<PagedResult<AuditEntryDto>> SearchAsync(
        Guid? entityId, string? action, int page, int pageSize, CancellationToken ct)
    {
        Guard.Paging(ref page, ref pageSize);
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (entityId is { } id) q = q.Where(a => a.EntityId == id);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AuditEntryDto(a.Id, a.OccurredAt, a.UserName, a.Action, a.EntityType, a.EntityId,
                a.PreviousState, a.NewState, a.Details, a.CorrelationId))
            .ToListAsync(ct);

        return new PagedResult<AuditEntryDto>(items, page, pageSize, total);
    }
}
