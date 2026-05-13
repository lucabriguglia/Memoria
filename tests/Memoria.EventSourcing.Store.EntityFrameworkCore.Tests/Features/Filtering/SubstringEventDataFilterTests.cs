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

    [Fact]
    public void Matches_numeric_property_values_without_quoting()
    {
        var filter = new SubstringEventDataFilter();
        var events = new[]
        {
            new EventEntity { Id = "1", StreamId = "s", EventType = "t", Data = "{\"Amount\":25.45}" },
            new EventEntity { Id = "2", StreamId = "s", EventType = "t", Data = "{\"Amount\":99.99}" }
        }.AsQueryable();

        var result = filter.ApplyPropertyFilter(events, "Amount", "25.45").ToList();

        result.Should().ContainSingle().Which.Id.Should().Be("1");
    }

    [Fact]
    public void Matches_boolean_property_values_without_quoting()
    {
        var filter = new SubstringEventDataFilter();
        var events = new[]
        {
            new EventEntity { Id = "1", StreamId = "s", EventType = "t", Data = "{\"IsPaid\":true}" },
            new EventEntity { Id = "2", StreamId = "s", EventType = "t", Data = "{\"IsPaid\":false}" }
        }.AsQueryable();

        var result = filter.ApplyPropertyFilter(events, "IsPaid", "true").ToList();

        result.Should().ContainSingle().Which.Id.Should().Be("1");
    }

    [Fact]
    public void Matches_null_property_values_without_quoting()
    {
        var filter = new SubstringEventDataFilter();
        var events = new[]
        {
            new EventEntity { Id = "1", StreamId = "s", EventType = "t", Data = "{\"ShippedAt\":null}" },
            new EventEntity { Id = "2", StreamId = "s", EventType = "t", Data = "{\"ShippedAt\":\"2025-01-01\"}" }
        }.AsQueryable();

        var result = filter.ApplyPropertyFilter(events, "ShippedAt", "null").ToList();

        result.Should().ContainSingle().Which.Id.Should().Be("1");
    }
}
