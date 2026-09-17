using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Beside Home on the bar, a Services heading folds the services this operator may read, each a
/// link to its own page: the way from any page to any other service, without going back through
/// Home. It lists what Home lists — by name, and only what the sign-in reaches — and is left off
/// the bar when there is nothing to list.
/// </summary>
public class ServicesMenuTests
{
    /// <summary>
    /// The heading sits between Home and everything else, and the services under it are in name
    /// order, case aside, whatever order their zips were installed in — the order Home lists them
    /// in, so a reader finds one the same way in both places.
    /// </summary>
    [Fact]
    public async Task Folds_every_installed_service_by_name_under_a_heading_beside_home()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Zebra").WithService("apple").WithService("Mango");

        var page = await web.Client.GetStringAsync("/");
        var items = Markup.MenuItems(page);

        using (new AssertionScope())
        {
            Markup.MenuBar(page).Should().Equal("Home", "Services");
            items.Select(item => item.Label).Should().ContainInOrder("Home", "Services", "apple", "Mango", "samples", "Zebra");
            items.Should().OnlyContain(item => item.Marked, "each item is told apart by its mark before its words");
            Markup.Header(page).Should().Contain("href=\"apple\"").And.Contain("href=\"zebra\"");
        }
    }

    /// <summary>
    /// The menu stays on the bar inside a service, ahead of the service's own part of it: it is
    /// the tool's, and the way across to another service from wherever a reader is.
    /// </summary>
    [Fact]
    public async Task Keeps_the_heading_on_the_bar_inside_a_service_ahead_of_the_service_s_own_menu()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithService("Orders");

        var page = await web.Client.GetStringAsync("/samples/streamed/events");

        Markup.MenuBar(page).Should().Equal("Home", "Services", "samples", "Streamed", "DCB");
    }

    /// <summary>
    /// An operator sees under the heading the services whose manifest names a claim they hold and
    /// no other, exactly as on Home: a menu is not a place to learn what one may not read.
    /// </summary>
    [Fact]
    public async Task Lists_only_the_services_the_operator_may_read()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", "orders-team")).WithSampleTypes()
            .WithService("Orders", read: ["orders-team"])
            .WithService("Billing", read: ["billing-team"]);

        var header = Markup.Header(await web.Client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            header.Should().Contain("href=\"orders\"");
            header.Should().NotContain("href=\"billing\"").And.NotContain("href=\"samples\"");
        }
    }

    /// <summary>
    /// Nothing to list, no heading: a menu with nothing under it is a door painted on a wall.
    /// Whether because no service is installed, or because none names this operator.
    /// </summary>
    [Fact]
    public async Task Leaves_the_heading_off_when_there_is_no_service_to_list()
    {
        using var empty = MemoriaWeb.Open();
        using var unnamed = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", "somewhere-else")).WithSampleTypes();

        var nothingInstalled = await empty.Client.GetStringAsync("/");
        var nothingNamed = await unnamed.Client.GetStringAsync("/");

        using (new AssertionScope())
        {
            Markup.MenuBar(nothingInstalled).Should().Equal("Home");
            Markup.MenuBar(nothingNamed).Should().Equal("Home");
        }
    }
}
