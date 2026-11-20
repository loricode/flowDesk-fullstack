using FlowDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Common;

/// <summary>Resuelve nombres de usuario en lote para evitar consultas N+1 al construir DTOs.</summary>
internal static class UserDirectory
{
    public static async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(
        this IAppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();

        return await db.Users.AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    public static string NameOf(this IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } value && names.TryGetValue(value, out var name) ? name : "Sistema";
}
