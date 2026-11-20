using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Domain.Workflow;
using FlowDesk.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Expedients;

public sealed class ExpedientService(IAppDbContext db, ICurrentUser currentUser, IAuditWriter audit, IClock clock)
{
    public async Task<PagedResult<ExpedientSummaryDto>> SearchAsync(ExpedientQuery query, CancellationToken ct)
    {
        int page = query.Page, pageSize = query.PageSize;
        Guard.Paging(ref page, ref pageSize);

        var q = db.Expedients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var statuses = query.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<ExpedientStatus>(s, true, out var st) ? st : throw new ValidationException("status", $"Estado '{s}' inválido."))
                .ToList();
            q = q.Where(e => statuses.Contains(e.Status));
        }

        if (!string.IsNullOrWhiteSpace(query.Priority) && Enum.TryParse<ExpedientPriority>(query.Priority, true, out var priority))
            q = q.Where(e => e.Priority == priority);

        if (query.AssignedToMe && currentUser.UserId is { } me)
            q = q.Where(e => e.AssignedToId == me);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(e => e.Code.Contains(term) || e.Title.Contains(term) || e.ApplicantName.Contains(term));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(e => e.Priority).ThenByDescending(e => e.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new
            {
                e.Id, e.Code, e.Title, e.ApplicantName, e.Status, e.Priority, e.AssignedToId,
                AssignedToName = db.Users.Where(u => u.Id == e.AssignedToId).Select(u => u.FullName).FirstOrDefault(),
                DocumentCount = e.Documents.Count,
                e.CreatedAt, e.UpdatedAt,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ExpedientSummaryDto(
            r.Id, r.Code, r.Title, r.ApplicantName, r.Status.ToString(), r.Priority.ToString(),
            r.AssignedToId, r.AssignedToName, r.DocumentCount, r.CreatedAt, r.UpdatedAt)).ToList();

        return new PagedResult<ExpedientSummaryDto>(items, page, pageSize, total);
    }

    public async Task<ExpedientDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var expedient = await LoadAggregateAsync(id, tracking: false, ct);
        var workflow = await LoadWorkflowAsync(ct);
        return await ToDetailAsync(expedient, workflow, ct);
    }

    public async Task<ExpedientDetailDto> CreateAsync(CreateExpedientRequest request, CancellationToken ct)
    {
        var userId = RequireUser();

        if (!Enum.TryParse<ExpedientPriority>(request.Priority, true, out var priority))
            throw new ValidationException("priority", "Prioridad inválida.");

        if (request.AssignedToId is { } assignee && !await db.Users.AnyAsync(u => u.Id == assignee && u.IsActive, ct))
            throw new ValidationException("assignedToId", "El responsable no existe o está inactivo.");

        var now = clock.UtcNow;
        var expedient = Expedient.Create(
            GenerateCode(now), request.Title, request.ApplicantName, request.Description,
            priority, userId, request.AssignedToId, now);

        db.Expedients.Add(expedient);
        audit.Write(AuditActions.ExpedientCreated, nameof(Expedient), expedient.Id,
            newState: expedient.Status.ToString(), details: expedient.Code);
        await db.SaveChangesAsync(ct);

        return await GetAsync(expedient.Id, ct);
    }

    public async Task<ExpedientDetailDto> AssignAsync(Guid id, AssignExpedientRequest request, CancellationToken ct)
    {
        var userId = RequireUser();
        var expedient = await LoadAggregateAsync(id, tracking: true, ct);
        EnsureStamp(expedient, request.ConcurrencyStamp);

        var assignee = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct)
            ?? throw new ValidationException("userId", "El responsable no existe o está inactivo.");

        var previous = expedient.AssignedToId;
        expedient.Assign(assignee.Id, userId, clock.UtcNow);
        audit.Write(AuditActions.ExpedientAssigned, nameof(Expedient), expedient.Id,
            previous?.ToString(), assignee.Id.ToString(), $"Asignado a {assignee.FullName}");

        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Ejecuta una transición del workflow. El dominio valida estado, permisos y condiciones.</summary>
    public async Task<ExpedientDetailDto> ExecuteTransitionAsync(Guid id, ExecuteTransitionRequest request, CancellationToken ct)
    {
        var userId = RequireUser();
        var expedient = await LoadAggregateAsync(id, tracking: true, ct);
        EnsureStamp(expedient, request.ConcurrencyStamp);

        var workflow = await LoadWorkflowAsync(ct);
        var transition = workflow.GetTransition(request.TransitionCode);

        var from = expedient.Status;
        expedient.ApplyTransition(transition, userId, currentUser.Permissions, request.Comment, clock.UtcNow);

        audit.Write(AuditActions.ExpedientTransition, nameof(Expedient), expedient.Id,
            from.ToString(), expedient.Status.ToString(),
            string.IsNullOrWhiteSpace(request.Comment) ? transition.Name : $"{transition.Name}: {request.Comment}");

        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    internal async Task<Expedient> LoadAggregateAsync(Guid id, bool tracking, CancellationToken ct)
    {
        var query = db.Expedients
            .Include(e => e.Documents).ThenInclude(d => d.Versions)
            .Include(e => e.History)
            .Include(e => e.Approvals)
            .AsQueryable(); // Split query configurado globalmente en Infrastructure.

        if (!tracking) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Expediente", id);
    }

    private async Task<WorkflowDefinition> LoadWorkflowAsync(CancellationToken ct) =>
        await db.WorkflowDefinitions.AsNoTracking()
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Code == WorkflowDefinition.DefaultCode && w.IsActive, ct)
        ?? throw new InvalidOperationException("No hay un workflow activo configurado.");

    private async Task<ExpedientDetailDto> ToDetailAsync(Expedient e, WorkflowDefinition workflow, CancellationToken ct)
    {
        var userIds = new List<Guid> { e.CreatedById };
        if (e.AssignedToId is { } a) userIds.Add(a);
        userIds.AddRange(e.History.Select(h => h.ChangedById));
        userIds.AddRange(e.Approvals.Select(x => x.DecidedById));
        userIds.AddRange(e.Documents.SelectMany(d => d.Versions).Select(v => v.UploadedById));
        var names = await db.NamesAsync(userIds, ct);

        var transitions = workflow.AvailableFrom(e.Status, currentUser.Permissions)
            .Select(t => new TransitionDto(t.Code, t.Name, t.FromStatus.ToString(), t.ToStatus.ToString(), t.RequiresComment))
            .ToList();

        return new ExpedientDetailDto(
            e.Id, e.Code, e.Title, e.ApplicantName, e.Description, e.Status.ToString(), e.Priority.ToString(),
            e.CreatedById, names.NameOf(e.CreatedById), e.AssignedToId,
            e.AssignedToId is null ? null : names.NameOf(e.AssignedToId),
            e.CreatedAt, e.UpdatedAt, e.ConcurrencyStamp.ToString(),
            e.Documents.OrderBy(d => d.CreatedAt).Select(d => d.ToDto(names)).ToList(),
            e.History.OrderBy(h => h.OccurredAt)
                .Select(h => new HistoryEntryDto(h.FromStatus?.ToString(), h.ToStatus.ToString(), names.NameOf(h.ChangedById), h.Comment, h.OccurredAt))
                .ToList(),
            e.Approvals.OrderBy(x => x.DecidedAt)
                .Select(x => new ApprovalDto(x.Decision.ToString(), names.NameOf(x.DecidedById), x.Comment, x.DecidedAt))
                .ToList(),
            transitions);
    }

    private static void EnsureStamp(Expedient expedient, string stamp)
    {
        if (!Guid.TryParse(stamp, out var parsed) || parsed != expedient.ConcurrencyStamp)
            throw new ConcurrencyException();
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException();
        }
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new UnauthorizedException("No autenticado.");

    private static string GenerateCode(DateTimeOffset now) =>
        $"EXP-{now:yyyyMMdd}-{Convert.ToHexString(Guid.NewGuid().ToByteArray(), 0, 3)}";
}

internal static class DocumentMappings
{
    public static DocumentDto ToDto(this Document d, IReadOnlyDictionary<Guid, string> names)
    {
        var current = d.Versions.First(v => v.VersionNumber == d.CurrentVersion);
        return new DocumentDto(
            d.Id, d.ExpedientId, d.FileName, d.ContentType, d.Status.ToString(), d.ValidationMessage,
            d.CurrentVersion, current.SizeBytes, current.Source, current.UploadedAt,
            d.Versions.OrderByDescending(v => v.VersionNumber)
                .Select(v => new DocumentVersionDto(v.VersionNumber, v.SizeBytes, v.Sha256, names.NameOf(v.UploadedById), v.UploadedAt, v.Source))
                .ToList());
    }
}
