using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;

/// <summary>
/// Default <see cref="IEventDataFilter"/> that matches a compact JSON substring against the serialized
/// event data column. Works for any column stored as plain text. Does not work for Postgres
/// <c>jsonb</c> columns — use the Npgsql package's JSON-operator filter instead.
/// </summary>
public sealed class SubstringEventDataFilter : IEventDataFilter
{
    /// <inheritdoc />
    public IQueryable<EventEntity> ApplyPropertyFilter(IQueryable<EventEntity> query, string propertyName, string propertyValue)
    {
        var needle = $"\"{propertyName}\":\"{propertyValue}\"";
        return query.Where(eventEntity => eventEntity.Data.Contains(needle));
    }
}
