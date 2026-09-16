using System.Security.Claims;
using AwesomeAssertions;
using Memoria.Web.Extensibility;
using Memoria.Web.Security;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Who may read and who may update a service: an operator holding a global role, or one whose
/// sign-in carries a claim value the service's manifest names. The manifest's values are read off
/// the same claim the configuration's are, so <c>orders-team</c> in a manifest means what
/// <c>memoria-admins</c> means in configuration. Update includes read; Administrator is global
/// only. Open, there is nobody to refuse and everything is granted.
/// </summary>
public class ServiceAccessTests
{
    private static readonly AuthorizationSettings Settings = new(
        RoleClaimType: "roles",
        Administrators: ["memoria-admins"],
        Updaters: ["memoria-updaters"],
        Readers: ["memoria-readers"]);

    private static readonly Service Orders = new(
        "Orders", ["Orders.dll"], "Orders", ReadRoles: ["orders-team"], UpdateRoles: ["orders-leads"]);

    private static readonly ServiceAccess Access = new(Settings);

    /// <summary>
    /// A principal as the request carries it after the claims transformation: the values the
    /// provider sent under the claim the settings name, and the tool's own role claims for
    /// whichever of them the configuration mapped.
    /// </summary>
    private static ClaimsPrincipal Holding(params string[] sent)
    {
        var claims = sent.Select(value => new Claim(Settings.RoleClaimType, value)).ToList();

        if (sent.Contains("memoria-admins")) claims.Add(new Claim(Roles.ClaimType, Roles.Administrator));
        if (sent.Contains("memoria-updaters")) claims.Add(new Claim(Roles.ClaimType, Roles.Updater));
        if (sent.Contains("memoria-readers")) claims.Add(new Claim(Roles.ClaimType, Roles.Reader));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", nameType: "name", roleType: "roles"));
    }

    [Theory]
    [InlineData("memoria-admins", true, true, true)]
    [InlineData("memoria-updaters", true, true, false)]
    [InlineData("memoria-readers", true, false, false)]
    [InlineData("orders-leads", true, true, false)]
    [InlineData("orders-team", true, false, false)]
    [InlineData("billing-team", false, false, false)]
    public void Decides_by_a_global_role_or_a_value_the_service_names(
        string sent, bool read, bool update, bool administer)
    {
        var user = Holding(sent);

        (Access.Grants(user, Roles.Reader, Orders), Access.Grants(user, Roles.Updater, Orders), Access.Grants(user, Roles.Administrator, Orders))
            .Should().Be((read, update, administer));
    }

    [Fact]
    public void Grants_nothing_to_an_operator_holding_no_claim_at_all()
    {
        Access.Grants(Holding(), Roles.Reader, Orders).Should().BeFalse();
    }

    /// <summary>
    /// Outside every service there is no manifest to consult: only the global roles answer.
    /// </summary>
    [Fact]
    public void Consults_only_the_global_roles_outside_a_service()
    {
        Access.Grants(Holding("orders-leads"), Roles.Reader, service: null).Should().BeFalse();
        Access.Grants(Holding("memoria-readers"), Roles.Reader, service: null).Should().BeTrue();
    }

    /// <summary>
    /// The manifest's values are read off the claim the settings name, and no other.
    /// </summary>
    [Fact]
    public void Reads_the_service_s_values_off_the_claim_the_settings_name()
    {
        var elsewhere = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("groups", "orders-team")], "test", nameType: "name", roleType: "roles"));

        Access.Grants(elsewhere, Roles.Reader, Orders).Should().BeFalse();
    }

    [Fact]
    public void Grants_everything_to_anyone_when_running_open()
    {
        var open = ServiceAccess.Open;
        var nobody = new ClaimsPrincipal(new ClaimsIdentity());

        (open.Grants(nobody, Roles.Reader, Orders), open.Grants(nobody, Roles.Updater, Orders), open.Grants(nobody, Roles.Administrator, null))
            .Should().Be((true, true, true));
    }
}
