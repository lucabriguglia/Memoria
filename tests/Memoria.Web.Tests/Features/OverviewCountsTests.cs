using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Every overview page counts, on each tile, the types behind it: the service's page counts each
/// model's sections and each model as a whole, a model's overview counts its sections, and a
/// section's overview counts on its Types tile what the model declares. The same fact wherever a
/// reader meets the tile, so how much is behind a door is known before it is opened.
/// </summary>
public class OverviewCountsTests
{
    /// <summary>The page with its whitespace folded, so a sentence wrapped in the markup reads as one line.</summary>
    private static string Prose(string page) => Regex.Replace(Markup.Unmarked(Markup.Plain(page)), "\\s+", " ");

    /// <summary>
    /// With one model laid out alone, its four tiles each carry their count — including the
    /// zeros, which say a section is empty before the reader opens it.
    /// </summary>
    [Fact]
    public async Task Counts_each_section_s_types_on_the_service_s_tiles()
    {
        using var web = MemoriaWeb.Open().WithOneNamespace();

        var page = Prose(await web.Client.GetStringAsync("/samples"));

        using (new AssertionScope())
        {
            page.Should().Contain("0 events the model can apply.");
            page.Should().Contain("2 aggregates the events are held in.");
            page.Should().Contain("0 projections built from those events.");
            page.Should().Contain("0 streams the events are held in.");
        }
    }

    /// <summary>
    /// With both models laid out, each column counts its own sections, and the Overview tile
    /// heading each column counts the tiles under it: a reader adding a column up arrives at the
    /// number over it. The identifiers that address the models are not in the total — no tile
    /// is theirs — so the total cannot outrun what it heads.
    /// </summary>
    [Fact]
    public async Task Counts_each_model_s_types_on_the_service_s_tiles_when_both_are_laid_out()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Prose(await web.Client.GetStringAsync("/samples"));
        var columns = Regex.Split(page, "(?=<section class=\"model\">)");
        var streamed = columns.Single(column => column.Contains("registered under the streamed model."));
        var dcb = columns.Single(column => column.Contains("registered under the DCB model."));

        using (new AssertionScope())
        {
            Number(streamed, "types registered under the streamed model\\.").Should().Be(
                Number(streamed, "events the model can apply\\.")
                + Number(streamed, "aggregates? the events are held in\\.")
                + Number(streamed, "projections? built from those events\\.")
                + Number(streamed, "streams the events are held in\\."));
            Number(dcb, "types registered under the DCB model\\.").Should().Be(
                Number(dcb, "events the model can apply\\.")
                + Number(dcb, "aggregates? the events are addressed by\\.")
                + Number(dcb, "projections? the events are addressed by\\."));
            Number(streamed, "types registered under the streamed model\\.").Should().BePositive();
            Number(dcb, "types registered under the DCB model\\.").Should().BePositive();
            page.Should().NotContain("What is registered under");
        }
    }

    /// <summary>The number written in front of the sentence given, which the page says exactly once.</summary>
    private static int Number(string page, string sentence) =>
        int.Parse(Regex.Matches(page, $"(\\d+) {sentence}").Single().Groups[1].Value);

    /// <summary>
    /// A model's overview and a section's overview open on their tiles: the heading, then the
    /// tiles, with no sentence between them saying what the page is. The tiles say it, and a
    /// line that repeated them was read past. The note a page draws when nothing is registered
    /// is not this sentence, and is kept — it is not drawn here, where something is.
    /// </summary>
    [Theory]
    [InlineData("/samples/streamed")]
    [InlineData("/samples/streamed/events")]
    [InlineData("/samples/streamed/aggregates")]
    [InlineData("/samples/streamed/projections")]
    [InlineData("/samples/dcb")]
    [InlineData("/samples/dcb/events")]
    [InlineData("/samples/dcb/aggregates")]
    [InlineData("/samples/dcb/projections")]
    public async Task Opens_on_the_tiles_with_no_sentence_under_the_heading(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Prose(await web.Client.GetStringAsync(address));

        page.Should().NotContain("class=\"lede\"").And.MatchRegex("</h1> <ul class=\"tiles\">");
    }

    /// <summary>
    /// A section's overview counts on its Types tile what the model declares there, under both
    /// models alike: the streamed sections used to say only what the tile led to.
    /// </summary>
    [Theory]
    [InlineData("/samples/streamed/events", "[1-9]\\d* events the model declares, and the state each one carries\\.")]
    [InlineData("/samples/streamed/aggregates", "[1-9]\\d* aggregates? the model declares, addressed by [1-9]\\d* identifiers?\\.")]
    [InlineData("/samples/streamed/projections", "[1-9]\\d* projections? the model declares, addressed by [1-9]\\d* identifiers?\\.")]
    [InlineData("/samples/dcb/events", "[1-9]\\d* events the model declares, and the state each one carries\\.")]
    [InlineData("/samples/dcb/aggregates", "[1-9]\\d* aggregates? the model declares, addressed by [1-9]\\d* identifiers?\\.")]
    [InlineData("/samples/dcb/projections", "[1-9]\\d* projections? the model declares, addressed by [1-9]\\d* identifiers?\\.")]
    public async Task Counts_the_types_a_section_declares_on_its_types_tile(string address, string pattern)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Prose(await web.Client.GetStringAsync(address));

        page.Should().MatchRegex(pattern);
    }
}
