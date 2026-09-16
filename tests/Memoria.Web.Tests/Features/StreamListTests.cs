using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The list of streams beside the Streams page's panel is the same list the six Types pages
/// have: the same rows, narrowed by the same filter, so what a reader learned on any of them is
/// true here. What folds under a stream is its own — the three sections holding what was stored
/// under it, each fronted by that section's mark.
/// </summary>
public class StreamListTests
{
    private const string Streams = "/samples/streamed/streams";

    [Fact]
    public async Task Draws_the_streams_as_the_shared_index_with_its_filter()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync(Streams));

        Markup.IndexNames(page).Should().Contain(["SampleStreamId", "SamplePrefixedStreamId", "SampleOnlyStreamId"]);
        page.Should().Contain("<ul class=\"index-rows\">")
            .And.Contain("<form class=\"index-filter\" method=\"get\" action=\"samples/streamed/streams\">")
            .And.MatchRegex(@"\d+ streams</p>");
    }

    [Fact]
    public async Task Narrows_the_streams_by_what_was_typed_and_keeps_it_across_the_tabs()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync($"{Streams}?q=prefixed&tab=state"));

        Markup.IndexNames(page).Should().Equal("SamplePrefixedStreamId");
        page.Should().MatchRegex(@"1 of \d+ streams</p>")
            .And.Contain($"href=\"samples/streamed/streams?type={typeof(SamplePrefixedStreamId).FullName}&amp;tab=info&amp;q=prefixed\"")
            .And.Contain($"href=\"samples/streamed/streams?type={typeof(SamplePrefixedStreamId).FullName}&amp;tab=state\">Clear</a>");
    }

    /// <summary>
    /// Three rows fold under a stream, one per section holding what was stored under it, each
    /// entered by the mark that section wears on its own tile rather than by one lens down all
    /// three.
    /// </summary>
    [Fact]
    public async Task Folds_the_three_sections_under_a_stream_each_with_its_own_mark()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var stream = typeof(SamplePrefixedStreamId).FullName;
        var page = Markup.Plain(await web.Client.GetStringAsync($"{Streams}?type={stream}"));
        var overview = Markup.Plain(await web.Client.GetStringAsync("/samples/streamed"));
        var row = Regex.Match(page, "<li class=\"current\">.*?</li>\\s*</ul>", RegexOptions.Singleline).Value;

        row.Should().Contain($"href=\"samples/streamed/events/data?stream={stream}\"")
            .And.Contain($"href=\"samples/streamed/aggregates/data?stream={stream}\"")
            .And.Contain($"href=\"samples/streamed/projections/data?stream={stream}\"");
        MarkBefore(row, "Events").Should().Be(MarkBefore(overview, "Events"));
        MarkBefore(row, "Aggregates").Should().Be(MarkBefore(overview, "Aggregates"));
        MarkBefore(row, "Projections").Should().Be(MarkBefore(overview, "Projections"));
        MarkBefore(row, "Events").Should().NotBe(MarkBefore(row, "Aggregates"));
    }

    /// <summary>
    /// The paths of the glyph drawn in front of a word, whether the word is the link's own text or
    /// the name on a tile.
    /// </summary>
    private static string MarkBefore(string markup, string word)
    {
        var mark = Regex.Match(
            markup,
            // The paths of one glyph and no further: a lazy match would run on from the first
            // glyph on the page to the word, and take the header's marks with it.
            $"<svg class=\"glyph\"[^>]*>(?<paths>(?:(?!</svg>).)*)</svg>\\s*(?:<span class=\"body\">\\s*<span class=\"name\">)?{word}<",
            RegexOptions.Singleline);

        mark.Success.Should().BeTrue($"a mark is drawn in front of {word}");

        return Regex.Replace(mark.Groups["paths"].Value, "\\s+", string.Empty);
    }
}
