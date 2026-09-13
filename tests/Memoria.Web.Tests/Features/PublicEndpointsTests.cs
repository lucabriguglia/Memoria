using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What answers without a session, pinned. Everything else is protected by not saying otherwise —
/// that is the fallback policy's whole point — so the only way to open an endpoint is to declare
/// it, and the declaration lands here. An addition to this list is a security review, not a
/// merge.
/// </summary>
public class PublicEndpointsTests
{
    /// <summary>Every route an anonymous caller may reach, other than the static assets.</summary>
    private static readonly string[] Public = [];

    [Fact]
    public void Opens_nothing_but_the_static_assets_and_the_routes_pinned_here()
    {
        using var web = MemoriaWeb.SigningIn();
        _ = web.Client;

        var endpoints = web.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();

        var open = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<StaticAssetDescriptor>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        open.Should().BeEquivalentTo(Public);
    }

    /// <summary>
    /// The stylesheet and the script are fetched by a page that has already been answered, so
    /// there is nothing to protect in them and something to lose: a sign-in page that cannot draw
    /// itself.
    /// </summary>
    [Fact]
    public void Serves_the_static_assets_to_anyone()
    {
        using var web = MemoriaWeb.SigningIn();
        _ = web.Client;

        var assets = web.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<StaticAssetDescriptor>() is not null)
            .ToList();

        assets.Should().NotBeEmpty();
        assets.Should().AllSatisfy(endpoint =>
            endpoint.Metadata.GetMetadata<IAllowAnonymous>().Should().NotBeNull());
    }
}
