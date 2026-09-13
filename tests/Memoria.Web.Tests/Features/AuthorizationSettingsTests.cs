using System.Collections.Generic;
using FluentAssertions;
using Memoria.Web.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How the tool is told which of the provider's claim values make an operator an Updater or an
/// Administrator. Read off configuration, because every provider puts its groups somewhere
/// different and calls them something different, and the tool never learns which provider it is.
/// </summary>
public class AuthorizationSettingsTests
{
    [Fact]
    public void Maps_nobody_and_reads_the_roles_claim_when_nothing_is_said()
    {
        AuthorizationSettings.Of(Configured()).Should().Be(new AuthorizationSettings(
            RoleClaimType: "roles",
            Administrators: [],
            Updaters: []));
    }

    [Fact]
    public void Reads_each_role_as_a_comma_separated_list_of_claim_values()
    {
        var settings = AuthorizationSettings.Of(Configured(
            ("Authorization:Roles:Administrator", "memoria-admins, ops"),
            ("Authorization:Roles:Updater", "memoria-updaters")));

        settings.Administrators.Should().Equal("memoria-admins", "ops");
        settings.Updaters.Should().Equal("memoria-updaters");
    }

    [Fact]
    public void Takes_the_claim_type_it_is_given_over_the_default()
    {
        AuthorizationSettings.Of(Configured(("Authorization:RoleClaimType", "cognito:groups")))
            .RoleClaimType.Should().Be("cognito:groups");
    }

    /// <summary>
    /// A trailing comma, a doubled one, or a list of nothing but spaces map nobody rather than
    /// mapping the empty string, which no claim ever carries.
    /// </summary>
    [Theory]
    [InlineData("memoria-admins,", new[] { "memoria-admins" })]
    [InlineData("memoria-admins,,ops", new[] { "memoria-admins", "ops" })]
    [InlineData("   ", new string[0])]
    [InlineData("", new string[0])]
    public void Ignores_empty_entries(string configured, string[] expected)
    {
        AuthorizationSettings.Of(Configured(("Authorization:Roles:Administrator", configured)))
            .Administrators.Should().Equal(expected);
    }

    [Fact]
    public void Knows_whether_anyone_is_mapped_at_all()
    {
        AuthorizationSettings.Of(Configured()).MapsAnyone.Should().BeFalse();
        AuthorizationSettings.Of(Configured(("Authorization:Roles:Updater", "u"))).MapsAnyone.Should().BeTrue();
        AuthorizationSettings.Of(Configured(("Authorization:Roles:Administrator", "a"))).MapsAnyone.Should().BeTrue();
    }

    private static IConfiguration Configured(params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?>();

        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
