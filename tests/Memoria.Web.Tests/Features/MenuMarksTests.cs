using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Every item in the header's menus is fronted by a mark, the same one the tile for that place
/// wears on the home page, so a menu is read at a glance the way the tiles are — and what is the
/// operator's own, under their name, is marked the same way as the places to go.
/// </summary>
public class MenuMarksTests
{
    [Fact]
    public async Task Marks_every_item_on_the_bar_and_under_its_headings()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var items = Markup.MenuItems(await web.Client.GetStringAsync("/samples"));

        items.Select(item => item.Label).Should().ContainInOrder(
            "Home", "samples", "Streamed", "Overview", "Types", "Data", "Streams", "DCB", "Settings", "Preferences");
        items.Should().OnlyContain(item => item.Marked, "each item is told apart by its mark before its words");
    }

    [Fact]
    public async Task Marks_the_sections_laid_out_alone_for_one_model()
    {
        using var web = MemoriaWeb.Open().WithDcbTypesOnly();

        var items = Markup.MenuItems(await web.Client.GetStringAsync("/samples"));

        items.Select(item => item.Label).Should().ContainInOrder("Home", "samples", "Events", "Aggregates", "Projections", "Settings");
        items.Should().OnlyContain(item => item.Marked);
    }

    [Fact]
    public async Task Marks_the_operator_their_preferences_and_their_way_out()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace");

        var items = Markup.MenuItems(await web.Client.GetStringAsync("/"));

        var own = items.Where(item => item.Label is "Ada Lovelace" or "Preferences" or "Sign out").ToArray();
        own.Select(item => item.Label).Should().Equal("Ada Lovelace", "Preferences", "Sign out");
        own.Should().OnlyContain(item => item.Marked);
    }
}
