using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Every line the tool logs about a write names the operator who asked for it. An assembly on the
/// host that nobody remembers uploading is answered by the log the host already keeps, not by
/// reconstruction from access logs.
/// </summary>
public class WriteLogTests
{
    private const string Admins = "memoria-admins";

    private const string Ada = "Ada Lovelace (ada lovelace)";

    [Fact]
    public async Task Names_the_operator_who_uploaded()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/upload", await Upload(client));

        web.Logged.Should().Contain(entry =>
            entry.Category == "Memoria.Web.Settings" &&
            entry.Message.Contains("Installed orders.zip") && entry.Message.Contains(Ada));
    }

    /// <summary>
    /// Removing what is not there is not a failure to the store, so the failed-removal line is
    /// written the same way and left to the store's own tests to reach.
    /// </summary>
    [Fact]
    public async Task Names_the_operator_who_removed()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client));

        await client.PostAsync("/settings/delete", await Form(client, ("name", "orders.zip")));

        web.Logged.Should().Contain(entry =>
            entry.Message.Contains("Removed orders.zip") && entry.Message.Contains(Ada));
    }

    /// <summary>
    /// Rereading changes what every operator resolves, so it is a write in every sense but the
    /// store's — and today it logs the catalogue but never that somebody asked.
    /// </summary>
    [Fact]
    public async Task Names_the_operator_who_reread_the_extensions()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/refresh", await Form(client));

        web.Logged.Should().Contain(entry =>
            entry.Category == "Memoria.Web.Settings" &&
            entry.Message.Contains("Reread") && entry.Message.Contains(Ada));
    }

    /// <summary>
    /// Whatever the store then says — refreshed, or could not — the line about the model carries
    /// the operator. Proved on the streamed store; the DCB handler answers through the same
    /// method, and its sample identifiers all take a parameter called <c>id</c>, which is also the
    /// form field that names the identifier type, so a post of them cannot reach the store here.
    /// </summary>
    [Fact]
    public async Task Names_the_operator_who_refreshed_a_snapshot()
    {
        using var web = Administrator().WithSampleTypes();
        var client = web.Client;

        await client.PostAsync("/streamed/aggregates/update", await Form(client,
            ("type", typeof(SampleAggregate).FullName!),
            ("stream", "sample:1"),
            ("id", "sample-1:1"),
            ("returnUrl", "/streamed/aggregates")));

        var about = web.Logged
            .Where(entry => entry.Category == "Memoria.Web.Streamed" && entry.Message.Contains(nameof(SampleAggregate)))
            .ToList();
        about.Should().NotBeEmpty();
        about.Should().AllSatisfy(entry => entry.Message.Should().Contain(Ada));
    }

    [Fact]
    public async Task Says_nobody_when_running_open()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;

        await client.PostAsync("/settings/upload", await Upload(client, tokenPage: "/settings"));

        web.Logged.Should().Contain(entry =>
            entry.Message.Contains("Installed orders.zip") && entry.Message.Contains("nobody (running open)"));
    }

    private static MemoriaWeb Administrator() =>
        MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

    private static async Task<MultipartFormDataContent> Upload(HttpClient client, string tokenPage = "/")
    {
        var page = await client.GetStringAsync(tokenPage);

        return new MultipartFormDataContent
        {
            { new StringContent(Forms.AntiforgeryToken(page)), Forms.AntiforgeryField },
            { new ByteArrayContent(Forms.Zip("Contoso.Orders.dll")), "files", "orders.zip" }
        };
    }

    private static async Task<FormUrlEncodedContent> Form(HttpClient client, params (string Name, string Value)[] fields)
    {
        var page = await client.GetStringAsync("/");

        return new FormUrlEncodedContent(
            fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))
                .Prepend(new KeyValuePair<string, string>(Forms.AntiforgeryField, Forms.AntiforgeryToken(page))));
    }
}
