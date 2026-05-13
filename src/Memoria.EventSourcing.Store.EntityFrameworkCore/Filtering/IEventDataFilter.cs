using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;

/// <summary>
/// Strategy for translating event-data property filters into provider-specific queries.
/// </summary>
/// <remarks>
/// The default implementation (<see cref="SubstringEventDataFilter"/>) performs a substring match on
/// the serialized JSON stored in <see cref="EventEntity.Data"/>. That works for plain text columns
/// (SQL Server <c>nvarchar</c>, SQLite <c>TEXT</c>, Postgres <c>text</c>) but fails when the column is
/// stored as Postgres <c>jsonb</c>, because <c>jsonb</c> reformats the JSON (e.g. inserts a space after
/// every colon) so the compact substring no longer matches. Provider-specific implementations swap this
/// abstraction for one that uses JSON operators (e.g. <c>-&gt;&gt;</c>).
/// </remarks>
public interface IEventDataFilter
{
    /// <summary>
    /// Applies a single property filter to the supplied query.
    /// </summary>
    /// <param name="query">The current event-entity query.</param>
    /// <param name="propertyName">The JSON property name to filter by.</param>
    /// <param name="propertyValue">The expected value of the property.</param>
    /// <returns>The query with the filter applied.</returns>
    IQueryable<EventEntity> ApplyPropertyFilter(IQueryable<EventEntity> query, string propertyName, string propertyValue);
}
