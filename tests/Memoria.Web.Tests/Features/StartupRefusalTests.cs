using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What a tool that could not read its settings answers: one page, on every address, saying which
/// settings it needs, with the status a host reads as "not up". Before, it exited and the host in
/// front of it said only that; whoever had just deployed it found the reason in a log they had to
/// go and open. Nothing else is mapped while it refuses — not the pages, not the upload form —
/// because a tool told neither how operators sign in nor to run open is exactly the one that must
/// not answer them.
/// </summary>
public class StartupRefusalTests
{
    [Fact]
    public async Task Answers_every_address_with_the_settings_it_needs_when_told_neither_to_sign_in_nor_to_run_open()
    {
        using var web = MemoriaWeb.Unconfigured();

        var home = await web.Client.GetAsync("/");
        var settings = await web.Client.GetAsync("/settings");

        home.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        settings.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var page = await home.Content.ReadAsStringAsync();
        page.Should()
            .Contain("Authentication:Oidc:Authority").And
            .Contain("Authentication:Oidc:ClientId").And
            .Contain("Authentication:Oidc:ClientSecret").And
            .NotContain("Authentication:Disabled").And
            .Contain("memoria-web-configuration");
    }

    /// <summary>
    /// The upload form is the reason the tool refuses in the first place, so its post is the one
    /// address that most needs to be absent — and it is absent by there being nothing mapped at
    /// all, not by a policy remembering to refuse it.
    /// </summary>
    [Fact]
    public async Task Maps_nothing_while_it_refuses()
    {
        using var web = MemoriaWeb.Unconfigured();

        var upload = await web.Client.PostAsync("/settings/upload", new MultipartFormDataContent());

        upload.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        web.Services.GetRequiredService<EndpointDataSource>().Endpoints.Should().BeEmpty();
    }

    /// <summary>
    /// A service names the connection string it reads over, so there is no single string to insist
    /// on at start-up: the tool starts with whatever strings it has, and a service naming one that
    /// is not there is listed as unreachable rather than stopping everything.
    /// </summary>
    [Fact]
    public async Task Starts_without_the_memoria_connection_string()
    {
        using var web = MemoriaWeb.Open().With("ConnectionStrings:Memoria", "");

        var home = await web.Client.GetAsync("/");

        home.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A string that is there but cannot be read is still a mistake nobody wants to find minutes
    /// later against a store that is perfectly reachable, whichever name it is under.
    /// </summary>
    [Fact]
    public async Task Refuses_a_connection_string_it_cannot_read_under_any_name()
    {
        using var web = MemoriaWeb.Open().With("ConnectionStrings:Orders", "nonsense");

        var home = await web.Client.GetAsync("/");

        home.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await home.Content.ReadAsStringAsync()).Should()
            .Contain("Connection string").And.Contain("Orders").And.Contain("could not be read");
    }

    /// <summary>
    /// The log still says it, and as an error: the page is for whoever is looking at the browser,
    /// and the log for whoever is looking at the host.
    /// </summary>
    [Fact]
    public async Task Says_why_in_the_log_as_well()
    {
        using var web = MemoriaWeb.Unconfigured();

        await web.Client.GetAsync("/");

        web.Logged.Should().Contain(entry =>
            entry.Level == LogLevel.Error && entry.Message.Contains("Authentication:Oidc:Authority"));
    }
}
