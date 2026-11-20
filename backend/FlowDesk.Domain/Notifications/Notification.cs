using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Notifications;

public sealed class Notification : Entity
{
    private Notification() { }

    public Notification(Guid userId, string title, string message, Guid? expedientId, DateTimeOffset now)
    {
        UserId = userId;
        Title = title;
        Message = message;
        ExpedientId = expedientId;
        CreatedAt = now;
    }

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public Guid? ExpedientId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkAsRead(DateTimeOffset now)
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = now;
    }
}
