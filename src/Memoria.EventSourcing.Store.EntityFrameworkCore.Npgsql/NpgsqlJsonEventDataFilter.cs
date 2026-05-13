using System.Text.Json;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql;

/// <summary>
/// <see cref="IEventDataFilter"/> for Postgres that uses the <c>@&gt;</c> JSON-containment operator via
/// <c>EF.Functions.JsonContains</c>. Requires <see cref="EventEntity.Data"/> to be mapped as
/// <c>jsonb</c> (or <c>json</c>) — that's the whole point of using this filter rather than the
/// substring default. Containment is indexable with a GIN index on the column.
/// </summary>
public sealed class NpgsqlJsonEventDataFilter : IEventDataFilter
{
    /// <inheritdoc />
    public IQueryable<EventEntity> ApplyPropertyFilter(IQueryable<EventEntity> query, string propertyName, string propertyValue)
    {
        var contained = JsonSerializer.Serialize(new Dictionary<string, string> { [propertyName] = propertyValue });
        return query.Where(eventEntity => EF.Functions.JsonContains(eventEntity.Data, contained));
    }
}
