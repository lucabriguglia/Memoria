using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Home lists the services installed, each the way into its own page, which lays that service's
/// models out the way Home used to lay the one store's out. Every page that reads a store sits
/// under the service's name, so a page under one service shows that service's types and no
/// other's; a name nobody declares is not found, and neither are the old addresses under nothing.
/// </summary>
public class HomeTests
{
    /// <summary>
    /// A service is listed one to a row, by name, with what its manifest says it is under the
    /// name. Which models it registered under is not said here: that is what its own page lays
    /// out, and every service said one of three things, which told a reader nothing.
    /// </summary>
    [Fact]
    public async Task Lists_each_installed_service_in_its_own_row_with_its_description()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Orders", description: "Orders placed in the shop, one stream a customer.");

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            page.Should().Contain("class=\"tiles services\"");
            page.Should().Contain("href=\"samples\"").And.Contain(">samples<");
            page.Should().Contain("href=\"orders\"").And.Contain(">Orders<");
            page.Should().Contain("<span class=\"detail\">Orders placed in the shop, one stream a customer.</span>");
            page.Should().NotContain("Streamed").And.NotContain("DCB");
        }
    }

    /// <summary>
    /// Beside the name, how much domain is behind the door: the types the service registered,
    /// counted by kind and the empty kinds left out. Each count is drawn as its section's mark
    /// with the number after it, and the words the mark stands for are kept on the span for a
    /// reader who cannot see it. Nothing says which model the service uses, nor which engine its
    /// store runs on.
    /// </summary>
    [Fact]
    public async Task Counts_a_service_s_types_beside_its_name_under_each_kind_s_mark()
    {
        using var web = MemoriaWeb.Open().WithOneNamespace();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            page.Should().Contain("<span class=\"fact\" title=\"2 aggregates\" aria-label=\"2 aggregates\"><svg class=\"glyph\"");
            Markup.Unmarked(page).Should().Contain("aria-label=\"2 aggregates\">2</span>");
            page.Should().NotContain("class=\"engine\"").And.NotContain("SQLite");
        }
    }

    /// <summary>
    /// Every kind in turn, the streams last: the order the service's own page lays the sections
    /// out in, so the row of counts here reads as that page folded up.
    /// </summary>
    [Fact]
    public async Task Counts_every_kind_a_service_registered()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Unmarked(Markup.Plain(await web.Client.GetStringAsync("/")));

        page.Should().MatchRegex(
            "<span class=\"facts\">" +
            "<span class=\"fact\" title=\"(\\d+) events\" aria-label=\"\\1 events\">\\1</span>" +
            "<span class=\"fact\" title=\"(\\d+) aggregates\" aria-label=\"\\2 aggregates\">\\2</span>" +
            "<span class=\"fact\" title=\"(\\d+) projections\" aria-label=\"\\3 projections\">\\3</span>" +
            "<span class=\"fact\" title=\"(\\d+) streams\" aria-label=\"\\4 streams\">\\4</span>" +
            "</span>");
    }

    /// <summary>
    /// The events counted are the ones some model of the service applies — the same events its
    /// own page counts, column by column — not every event its assemblies declare. An event no
    /// model folds is bound and written, but it is not behind either door, and counting it here
    /// made Home disagree with the page it leads to. A service with no streams counts none, rather
    /// than counting zero.
    /// </summary>
    [Fact]
    public async Task Counts_the_events_some_model_applies_and_no_streams_where_there_are_none()
    {
        using var web = MemoriaWeb.Open().WithService("Partly", assembly: OneSidedAssembly.PartlyApplied);

        var page = Markup.Unmarked(Markup.Plain(await web.Client.GetStringAsync("/")));

        page.Should().Contain(
            "<span class=\"facts\">" +
            "<span class=\"fact\" title=\"1 event\" aria-label=\"1 event\">1</span>" +
            "<span class=\"fact\" title=\"1 aggregate\" aria-label=\"1 aggregate\">1</span>" +
            "</span>");
    }

    /// <summary>A service over an assembly that registers nothing says so, in words, where the counts would be.</summary>
    [Fact]
    public async Task Says_when_a_service_registered_no_types()
    {
        using var web = MemoriaWeb.Open().WithService("Empty", assembly: OneSidedAssembly.Empty);

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().Contain(">Empty<").And.Contain("<span class=\"facts\"><span class=\"fact\">no types registered</span></span>");
    }

    /// <summary>
    /// A store that cannot be reached is said under the name, and nothing is counted beside it:
    /// the counts would suggest a door that opens.
    /// </summary>
    [Fact]
    public async Task Says_a_service_is_unreachable_and_counts_nothing_beside_it()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithService("Ghost", connectionString: "Nowhere");

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));
        var ghost = page[page.IndexOf(">Ghost<", StringComparison.Ordinal)..];
        var tile = ghost[..ghost.IndexOf("</li>", StringComparison.Ordinal)];

        tile.Should().Contain("Unreachable").And.NotContain("class=\"facts\"");
    }

    /// <summary>
    /// The services are listed by name, whatever order their zips were installed in: a reader
    /// looking for one scans a list, and a list scanned wants an order the reader already knows.
    /// </summary>
    [Fact]
    public async Task Lists_the_services_by_name()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Zebra").WithService("apple").WithService("Mango");

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        new[] { ">apple<", ">Mango<", ">samples<", ">Zebra<" }
            .Select(name => page.IndexOf(name, StringComparison.Ordinal))
            .Should().BeInAscendingOrder().And.NotContain(-1);
    }

    /// <summary>A service whose manifest says nothing of it has a name and no line under it.</summary>
    [Fact]
    public async Task Draws_no_line_under_a_service_whose_manifest_gives_no_description()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().Contain(">samples<").And.NotContain("class=\"detail\"");
    }

    /// <summary>
    /// A service is shown by the name its manifest wrote and reached by the address that name
    /// makes: letters and digits kept, each run of spaces one dash, lower case.
    /// </summary>
    [Fact]
    public async Task Shows_a_service_by_its_name_and_reaches_it_by_the_address_the_name_makes()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithService("Orders  Team (EU)");

        var home = Markup.Plain(await web.Client.GetStringAsync("/"));
        var page = Markup.Plain(await web.Client.GetStringAsync("/orders-team-eu"));

        using (new AssertionScope())
        {
            home.Should().Contain("href=\"orders-team-eu\"").And.Contain("Orders  Team (EU)");
            page.Should().Contain("<h1>Orders  Team (EU)</h1>").And.Contain("href=\"orders-team-eu/streamed\"");
            Markup.MenuBar(page).Should().Equal("Home", "Services", "Orders  Team (EU)", "Streamed", "DCB");
        }
    }

    [Fact]
    public async Task Says_when_no_service_is_installed()
    {
        using var web = MemoriaWeb.Open();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().Contain("No services").And.NotContain("class=\"models\"");
    }

    /// <summary>
    /// Under the service's name on its own page, what its manifest says it is, and nothing else:
    /// the sentence about which models it uses told a reader nothing the tiles under it do not,
    /// and a service that says nothing of itself has a name and its tiles.
    /// </summary>
    [Fact]
    public async Task Says_under_the_service_s_name_what_its_manifest_says_it_is_and_nothing_else()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Orders", description: "Orders placed in the shop, one stream a customer.");

        var described = Markup.Plain(await web.Client.GetStringAsync("/orders"));
        var undescribed = Markup.Plain(await web.Client.GetStringAsync("/samples"));

        using (new AssertionScope())
        {
            described.Should().MatchRegex("<h1>Orders</h1>\\s*<p class=\"lede\">Orders placed in the shop, one stream a customer.</p>");
            described.Should().NotContain("Two models of the same domain");
            undescribed.Should().NotContain("class=\"lede\"");
        }
    }

    [Fact]
    public async Task Lays_a_service_out_on_its_own_page()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync("/samples"));

        page.Should().Contain("class=\"models\"")
            .And.Contain("<h2><a href=\"samples/streamed\">Streamed</a></h2>")
            .And.Contain("<h2><a href=\"samples/dcb\">DCB</a></h2>");
    }

    [Theory]
    [InlineData("/nobody")]
    [InlineData("/nobody/streamed/events")]
    [InlineData("/streamed/events")]
    [InlineData("/dcb")]
    public async Task Finds_no_page_for_a_service_nobody_declares_nor_at_the_old_addresses(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var response = await web.Client.GetAsync(address);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Shows_a_service_s_pages_under_its_name()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync("/samples/streamed/aggregates/types"));

        page.Should().Contain(nameof(SampleAggregate));
    }

    /// <summary>
    /// Every page that reads a store answers under the service — each of the two models' overview,
    /// sections, types and data pages, and the streams. Pinned by address because one page's route
    /// was missed by a scripted move once, and nothing else asked for it by address.
    /// </summary>
    [Theory]
    [InlineData("/samples/streamed")]
    [InlineData("/samples/streamed/events")]
    [InlineData("/samples/streamed/events/types")]
    [InlineData("/samples/streamed/events/data")]
    [InlineData("/samples/streamed/aggregates")]
    [InlineData("/samples/streamed/aggregates/types")]
    [InlineData("/samples/streamed/aggregates/data")]
    [InlineData("/samples/streamed/projections")]
    [InlineData("/samples/streamed/projections/types")]
    [InlineData("/samples/streamed/projections/data")]
    [InlineData("/samples/streamed/streams")]
    [InlineData("/samples/dcb")]
    [InlineData("/samples/dcb/events")]
    [InlineData("/samples/dcb/events/types")]
    [InlineData("/samples/dcb/events/data")]
    [InlineData("/samples/dcb/aggregates")]
    [InlineData("/samples/dcb/aggregates/types")]
    [InlineData("/samples/dcb/aggregates/data")]
    [InlineData("/samples/dcb/projections")]
    [InlineData("/samples/dcb/projections/types")]
    [InlineData("/samples/dcb/projections/data")]
    public async Task Answers_every_page_under_the_service(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var response = await web.Client.GetAsync(address);

        // Found, whatever the page then makes of a store with nothing in it: what is pinned here
        // is that the address reaches a page, and the pages' own tests say what each shows.
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Inside a service the bar is the service's: Home, the service's name leading to its page,
    /// and the model menus as they were. Outside one there are no models to menu.
    /// </summary>
    [Fact]
    public async Task Draws_the_service_s_menu_on_the_bar_inside_it_and_none_outside()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var inside = await web.Client.GetStringAsync("/samples");
        var outside = await web.Client.GetStringAsync("/");

        using (new AssertionScope())
        {
            Markup.MenuBar(inside).Should().Equal("Home", "Services", "samples", "Streamed", "DCB");
            Markup.MenuBar(outside).Should().Equal("Home", "Services");
        }
    }

    /// <summary>
    /// A rule stands between the tool's part of the bar — Home and the Services menu — and the
    /// service's, so where the one ends and the other begins can be seen. Outside a service there
    /// is nothing to set apart.
    /// </summary>
    [Fact]
    public async Task Sets_the_service_s_menu_apart_from_the_tool_s_with_a_rule()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var inside = Markup.Unmarked(Markup.Header(await web.Client.GetStringAsync("/samples")));
        var outside = Markup.Unmarked(Markup.Header(await web.Client.GetStringAsync("/")));

        using (new AssertionScope())
        {
            inside.Should().MatchRegex(
                "Home</a>\\s*<details class=\"menu\">\\s*<summary[^>]*>Services</summary>[\\s\\S]*?</details>\\s*<span class=\"separator\"></span>\\s*<a href=\"samples\"");
            outside.Should().NotContain("class=\"separator\"");
        }
    }

    [Fact]
    public async Task Passes_the_breadcrumb_through_the_service()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync("/samples/streamed/events/types");

        Markup.Breadcrumb(page).Should().Contain("<a href=\"samples\">samples</a>")
            .And.Contain("<a href=\"samples/streamed\">Streamed</a>");
    }
}
