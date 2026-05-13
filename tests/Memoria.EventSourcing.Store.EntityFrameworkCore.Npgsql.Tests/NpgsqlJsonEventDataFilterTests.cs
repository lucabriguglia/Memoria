using FluentAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql.Tests;

public class NpgsqlJsonEventDataFilterTests
{
    [Fact]
    public void Applies_a_JsonContains_predicate_to_the_query()
    {
        var filter = new NpgsqlJsonEventDataFilter();
        var query = new List<EventEntity>().AsQueryable();

        var filtered = filter.ApplyPropertyFilter(query, "Name", "Alice");

        filtered.Expression.ToString().Should().Contain(nameof(NpgsqlJsonDbFunctionsExtensions.JsonContains));
    }
}
