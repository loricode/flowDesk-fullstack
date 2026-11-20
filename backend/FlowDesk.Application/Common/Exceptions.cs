namespace FlowDesk.Application.Common;

public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' no existe.");

public sealed class ForbiddenException(string message) : Exception(message);

public sealed class UnauthorizedException(string message) : Exception(message);

public sealed class ConcurrencyException()
    : Exception("El registro fue modificado por otro usuario. Recargue e intente de nuevo.");

public sealed class ValidationException(IDictionary<string, string[]> errors)
    : Exception("Uno o más datos no son válidos.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}

internal static class Guard
{
    public static void Paging(ref int page, ref int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
    }
}
