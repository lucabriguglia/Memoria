using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Web.Tests.Elsewhere;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The list of types beside a Types page's panel, once a domain has more of them than fit at a
/// glance: narrowed by a word typed into it, and folded by namespace so a long list opens one
/// group at a time.
/// </summary>
public class TypeIndexTests
{
    private const string StreamedEvents = "/samples/streamed/events/types";

    [Fact]
    public async Task Lists_every_type_and_says_how_many_when_nothing_is_typed()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync(StreamedEvents);

        Markup.IndexNames(page).Should().Contain(["SampleHappened", "SampleCarried", "ElsewhereHappened"]);
        page.Should().MatchRegex(@"\d+ event types</p>").And.NotContain(" of ");
    }

    [Fact]
    public async Task Narrows_the_list_to_the_types_whose_name_holds_what_was_typed()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync($"{StreamedEvents}?q=CARRIED");

        Markup.IndexNames(page).Should().Equal("SampleCarried");
        page.Should().MatchRegex(@"1 of \d+ event types</p>");
    }

    /// <summary>
    /// The class is matched as well as the name, so a namespace narrows the list to what came from
    /// it — which is what a reader who knows where a type lives types.
    /// </summary>
    [Fact]
    public async Task Narrows_the_list_by_namespace_as_well_as_by_name()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync($"{StreamedEvents}?q=elsewhere");

        Markup.IndexNames(page).Should().Equal("ElsewhereHappened");
    }

    [Fact]
    public async Task Says_so_when_nothing_matches()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync($"{StreamedEvents}?q=nothinglikethis");

        Markup.IndexNames(page).Should().BeEmpty();
        page.Should().Contain("No event type matches.").And.MatchRegex(@"0 of \d+ event types</p>");
    }

    /// <summary>
    /// The word typed travels with every link out of the list and with the tabs, so choosing a
    /// type or a view does not throw the narrowing away; the form itself carries the choice and the
    /// tab, so typing does not throw those away either.
    /// </summary>
    [Fact]
    public async Task Keeps_the_narrowing_across_the_rows_and_the_tabs_and_the_choice_across_the_form()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync(
            $"{StreamedEvents}?type={typeof(SampleCarriedEvent).FullName}&tab=state&q=sample"));

        page.Should()
            .Contain($"href=\"samples/streamed/events/types?type={typeof(SampleHappenedEvent).FullName}&amp;tab=state&amp;q=sample\"")
            .And.Contain($"href=\"samples/streamed/events/types?type={typeof(SampleCarriedEvent).FullName}&amp;tab=info&amp;q=sample\"")
            .And.Contain("<form class=\"index-filter\" method=\"get\" action=\"samples/streamed/events/types\">")
            .And.Contain("name=\"q\" value=\"sample\"")
            .And.Contain($"<input type=\"hidden\" name=\"type\" value=\"{typeof(SampleCarriedEvent).FullName}\" />")
            .And.Contain("<input type=\"hidden\" name=\"tab\" value=\"state\" />")
            .And.Contain($"href=\"samples/streamed/events/types?type={typeof(SampleCarriedEvent).FullName}&amp;tab=state\">Clear</a>");
    }

    [Fact]
    public async Task Offers_no_way_to_clear_when_nothing_is_typed()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync(StreamedEvents);

        page.Should().NotContain(">Clear</a>");
    }

    /// <summary>
    /// Folded by namespace, with the group holding the type being read open and the others shut:
    /// a long list is then as long as the group the reader is in.
    /// </summary>
    [Fact]
    public async Task Folds_the_list_by_namespace_with_only_the_current_group_open()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync(
            $"{StreamedEvents}?type={typeof(SampleCarriedEvent).FullName}");

        Markup.IndexGroups(page).Should().Equal(
            new Markup.IndexGroup("Memoria.Web.Tests.Elsewhere", Open: false),
            new Markup.IndexGroup("Memoria.Web.Tests", Open: true));
    }

    /// <summary>
    /// What was typed is what the reader is looking for, so every group holding a match is open.
    /// </summary>
    [Fact]
    public async Task Opens_every_group_that_the_narrowing_left_something_in()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync($"{StreamedEvents}?q=happened");

        Markup.IndexGroups(page).Should().Equal(
            new Markup.IndexGroup("Memoria.Web.Tests.Elsewhere", Open: true),
            new Markup.IndexGroup("Memoria.Web.Tests", Open: true));
        Markup.IndexNames(page).Should().Equal("ElsewhereHappened", "SampleHappened");
    }

    /// <summary>
    /// One namespace is nothing to fold by: a heading over the whole list would say what every row
    /// under it already agrees on.
    /// </summary>
    [Fact]
    public async Task Draws_no_groups_when_every_type_shares_one_namespace()
    {
        using var web = MemoriaWeb.Open().WithOneNamespace();

        var page = await web.Client.GetStringAsync("/samples/streamed/aggregates/types");

        Markup.IndexGroups(page).Should().BeEmpty();
        Markup.IndexNames(page).Should().Equal("FirstAggregate", "SecondAggregate");
    }

    /// <summary>
    /// The same list on every Types page, so what is learned on one is true of the other five.
    /// </summary>
    [Theory]
    [InlineData("/samples/streamed/aggregates/types", "SampleAggregate", "aggregate")]
    [InlineData("/samples/streamed/projections/types", "SampleProjection", "projection")]
    [InlineData("/samples/dcb/aggregates/types", "SampleDcbAggregate", "aggregate")]
    [InlineData("/samples/dcb/projections/types", "SampleDcbProjection", "projection")]
    [InlineData("/samples/dcb/events/types", "SampleHappened", "event type")]
    [InlineData("/samples/streamed/streams", "SamplePrefixedStreamId", "stream")]
    public async Task Narrows_every_types_page_the_same_way(string address, string name, string noun)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync($"{address}?q={name}");

        Markup.IndexNames(page).Should().Contain(name);
        page.Should().MatchRegex($@"\d+ of \d+ {noun}s</p>");
    }
}
