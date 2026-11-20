namespace FlowDesk.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

/// <summary>
/// Raíz de agregado: único punto de entrada para modificar su consistencia interna.
/// Acumula eventos de dominio que la infraestructura convierte en mensajes Outbox
/// dentro de la misma transacción.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Violación de una regla de negocio.</summary>
public class DomainException(string message, string code = "domain_rule") : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>El usuario no tiene permiso para ejecutar la operación de negocio.</summary>
public sealed class DomainForbiddenException(string message) : DomainException(message, "forbidden");
