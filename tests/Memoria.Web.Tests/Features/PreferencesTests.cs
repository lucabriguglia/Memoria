using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Preferences are this browser's own — theme, rows per page, the ordering note — and every
/// operator has a browser, so they live on a page of their own that every signed-in operator can
/// reach, not on the settings page that only an Administrator can. The way there is under the
/// operator's name in the header, beside sign-out; open, with nobody to name, it is a plain link.
/// </summary>
public class PreferencesTests
{
    private const string Admins = "memoria-admins";

    [Fact]
    public async Task Shows_a_reader_their_preferences()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace")
            .With("Authorization:Roles:Administrator", Admins);

        var response = await web.Client.GetAsync("/preferences");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should()
            .Contain("data-preference=\"theme\"").And
            .Contain("data-preference=\"rows-per-page\"").And
            .Contain("data-preference=\"hide-ordering-notice\"");
    }

    [Fact]
    public async Task Keeps_the_preferences_off_the_settings_page()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

        var page = await web.Client.GetStringAsync("/settings");

        page.Should().NotContain("tab=preferences").And.NotContain("data-preference=\"theme\"");
    }

    /// <summary>
    /// The name opens a menu, the way the section headings in the bar do, holding the two things
    /// that are the operator's own: their preferences and their way out.
    /// </summary>
    [Fact]
    public async Task Opens_preferences_and_sign_out_under_the_signed_in_operators_name()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace");

        var page = await web.Client.GetStringAsync("/");

        Markup.Unmarked(Markup.Header(page)).Should().MatchRegex("<summary[^>]*>[^<]*Ada Lovelace");
        Markup.OperatorMenu(page).Should().Equal("Ada Lovelace", "Preferences", "Sign out");
        Markup.Header(page).Should().Contain("href=\"preferences\"").And.Contain("action=\"logout\"");
    }

    /// <summary>
    /// Settings goes with the preferences rather than on the bar: an Administrator's own page,
    /// first under their name, ahead of what every operator has. Whoever is not one still sees
    /// nothing of it, and the bar holds the places to go alone.
    /// </summary>
    [Fact]
    public async Task Opens_settings_first_under_an_administrators_name()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

        var home = await web.Client.GetStringAsync("/");
        var settings = await web.Client.GetStringAsync("/settings");

        using (new AssertionScope())
        {
            Markup.OperatorMenu(home).Should().Equal("Ada Lovelace", "Settings", "Preferences", "Sign out");
            Markup.MenuBar(home).Should().Equal("Home");
            Markup.Unmarked(Markup.Header(settings)).Should()
                .Contain("<summary class=\"active\">Ada Lovelace</summary>", "the name is marked while on a page under it");
        }
    }

    [Fact]
    public async Task Links_to_preferences_in_place_of_the_operator_when_running_open()
    {
        using var web = MemoriaWeb.Open();

        var header = Markup.Header(await web.Client.GetStringAsync("/"));

        header.Should().Contain("href=\"preferences\"").And.NotContain("action=\"logout\"");
    }

    /// <summary>
    /// Open, there is no name to fold the two under, so they stand on the bar where the name
    /// would be, Settings first, as they would be listed under it.
    /// </summary>
    [Fact]
    public async Task Puts_settings_before_preferences_in_place_of_the_operator_when_running_open()
    {
        using var web = MemoriaWeb.Open();

        var page = await web.Client.GetStringAsync("/");

        using (new AssertionScope())
        {
            Markup.OperatorMenu(page).Should().Equal("Settings", "Preferences");
            Markup.MenuBar(page).Should().Equal("Home");
        }
    }
}
