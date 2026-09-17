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
    [Fact]
    public async Task Lists_each_installed_service_with_the_models_it_registered()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            page.Should().Contain("href=\"samples\"");
            page.Should().Contain(">samples<");
            page.Should().Contain("Streamed").And.Contain("DCB");
        }
    }

    [Fact]
    public async Task Says_which_model_a_service_registered_when_it_registered_one()
    {
        using var web = MemoriaWeb.Open().WithStreamedTypesOnly();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().Contain("href=\"samples\"").And.Contain("Streamed").And.NotContain(">DCB<");
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
            Markup.MenuBar(page).Should().Equal("Home", "Orders  Team (EU)", "Streamed", "DCB");
        }
    }

    [Fact]
    public async Task Says_when_no_service_is_installed()
    {
        using var web = MemoriaWeb.Open();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().Contain("No services").And.NotContain("class=\"models\"");
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
            Markup.MenuBar(inside).Should().Equal("Home", "samples", "Streamed", "DCB");
            Markup.MenuBar(outside).Should().Equal("Home");
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
