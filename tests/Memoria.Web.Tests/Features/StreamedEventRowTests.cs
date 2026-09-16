using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A row on a streamed model's events tab opens over the table, the way a row on the log's own
/// page opens on a page of its own: the same three views of the one stored event, drawn by the
/// one component both are read through.
/// </summary>
public class StreamedEventRowTests
{
    private static readonly DateTimeOffset Written = new(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);

    private const string FirstId = "sample:1:1";

    private const string SecondId = "sample:1:2";

    private static StoredStreamEvent Row(string id, long sequence, string value) =>
        new("sample:1", id, new StoredEvent(
            sequence,
            "SampleHappened:1",
            Written.AddMinutes(sequence),
            $$"""{"Id":"{{value}}"}""",
            [new DomainPropertyValue("Id", "string", value)],
            Error: null,
            Tags: [])
        {
            WrittenBy = "an operator"
        });

    private static IStreamedReads Holding(params StoredStreamEvent[] rows)
    {
        var reads = Substitute.For<IStreamedReads>();

        reads.Model(Arg.Any<StreamedModelAddress>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReadStreamModel(
                new StoredStreamModel(
                    "sample:1", "sample-1:1", "SampleAggregate:1", Version: rows.Length, Sequence: rows.Length,
                    Data: "{}", Written, CreatedBy: null, Written, UpdatedBy: null),
                Error: null)));
        reads.Events(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StoredStreamEvents(
                rows, Total: rows.Length, Page: 1, TotalPages: 1, Error: null)));
        reads.Event(Arg.Any<StreamedEventAddress>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new ReadStreamEvent(
                Array.Find(rows, row => row.Id == call.Arg<StreamedEventAddress>().Id),
                Error: null)));

        return reads;
    }

    private static string EventsTab(string? row = null, string? show = null) =>
        MemoriaWeb.SampleAggregateDetail("events") +
        (row is null ? string.Empty : $"&row={Uri.EscapeDataString(row)}") +
        (show is null ? string.Empty : $"&show={show}");

    [Fact]
    public async Task Every_row_on_the_events_tab_is_a_link_to_itself_opened_over_the_table()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding(Row(FirstId, 1, "one"), Row(SecondId, 2, "two")));

        var page = Markup.Plain(await web.Client.GetStringAsync(EventsTab()));

        page.Should().Contain("<tr class=\"clickable\">")
            .And.Contain($"&amp;row={Uri.EscapeDataString(FirstId)}#row\"")
            .And.Contain($"&amp;row={Uri.EscapeDataString(SecondId)}#row\"")
            .And.NotContain("role=\"dialog\"");
    }

    [Fact]
    public async Task An_opened_row_says_where_it_sits_over_the_table_it_was_opened_from()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding(Row(FirstId, 1, "one"), Row(SecondId, 2, "two")));

        var page = Markup.Plain(await web.Client.GetStringAsync(EventsTab(row: SecondId)));

        page.Should().Contain("role=\"dialog\"")
            .And.Contain("<dt>Stream Id</dt>")
            .And.Contain("<dd><code>sample:1</code></dd>")
            .And.Contain("<dt>Id</dt>")
            .And.Contain($"<dd><code>{SecondId}</code></dd>")
            .And.Contain("<dt>Sequence</dt>")
            .And.Contain("<dd>2</dd>")
            // The table is still under it, so a reader who closes it is where they were.
            .And.Contain("<tr class=\"clickable\">");
    }

    /// <summary>
    /// The same fact the log's own page ends its list with, because the table's read now asks for
    /// it too: a row opened over the table says who appended it.
    /// </summary>
    [Fact]
    public async Task An_opened_row_says_who_appended_it()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding(Row(FirstId, 1, "one")));

        var page = Markup.Plain(await web.Client.GetStringAsync(EventsTab(row: FirstId)));

        page.Should().Contain("<dt>Appended by</dt>").And.Contain("an operator");
    }

    [Theory]
    [InlineData("state", "<td>Id</td>", "<td>one</td>")]
    [InlineData("json", "<pre class=\"json\">", "one")]
    public async Task The_opened_rows_other_views_are_a_tab_away(string show, string first, string second)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding(Row(FirstId, 1, "one")));

        var page = Markup.Plain(await web.Client.GetStringAsync(EventsTab(row: FirstId, show: show)));

        page.Should().Contain(first).And.Contain(second)
            .And.Contain($"&amp;row={Uri.EscapeDataString(FirstId)}&amp;show=state#row\"")
            .And.Contain($"&amp;row={Uri.EscapeDataString(FirstId)}&amp;show=json#row\"");
    }

    [Fact]
    public async Task A_row_that_is_not_on_the_page_opens_nothing()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding(Row(FirstId, 1, "one")));

        var page = await web.Client.GetStringAsync(EventsTab(row: "sample:1:99"));

        page.Should().NotContain("role=\"dialog\"");
    }

    /// <summary>The same view on the page the log opens a row on.</summary>
    [Fact]
    public async Task The_events_own_page_reads_the_same_view_and_says_who_appended_it()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding(Row(FirstId, 1, "one")));

        var page = Markup.Plain(await web.Client.GetStringAsync(
            $"/samples/streamed/events/detail?stream=sample:1&id={Uri.EscapeDataString(FirstId)}&tab=info"));

        page.Should().Contain("<dt>Stream Id</dt>")
            .And.Contain($"<dd><code>{FirstId}</code></dd>")
            .And.Contain("<dt>Appended by</dt>")
            .And.Contain("an operator")
            .And.NotContain("role=\"dialog\"");
    }
}
