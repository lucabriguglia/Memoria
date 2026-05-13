using FluentAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.Filtering;

public class SubstringEventDataFilterTests
{
    [Fact]
    public void Keeps_events_whose_data_contains_the_compact_property_substring()
    {
        var filter = new SubstringEventDataFilter();
        var events = new[]
        {
            new EventEntity { Id = "1", StreamId = "s", EventType = "t", Data = "{\"Name\":\"Alice\",\"Age\":30}" },
            new EventEntity { Id = "2", StreamId = "s", EventType = "t", Data = "{\"Name\":\"Bob\",\"Age\":40}" }
        }.AsQueryable();

        var result = filter.ApplyPropertyFilter(events, "Name", "Alice").ToList();

        result.Should().ContainSingle().Which.Id.Should().Be("1");
    }

    [Fact]
    public void Excludes_events_whose_data_has_spaces_around_the_colon()
    {
        var filter = new SubstringEventDataFilter();
        var events = new[]
        {
            new EventEntity { Id = "1", StreamId = "s", EventType = "t", Data = "{\"Name\": \"Alice\"}" }
        }.AsQueryable();

        var result = filter.ApplyPropertyFilter(events, "Name", "Alice").ToList();

        result.Should().BeEmpty(because: "this is the known limitation that motivates the Npgsql filter");
    }
}
