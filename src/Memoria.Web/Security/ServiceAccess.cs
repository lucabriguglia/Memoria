using System.Security.Claims;
using Memoria.Web.Extensibility;

namespace Memoria.Web.Security;

/// <summary>
/// Whether an operator may do a thing to a service: read it, update it, or — for the tool as a
/// whole — administer it.
/// </summary>
/// <remarks>
/// Two places grant a role. The configuration maps claim values to the three global roles, and
/// a global role reaches every service: a Reader reads them all, an Updater updates them all, an
/// Administrator does everything. A service's manifest names claim values of its own under
/// <c>roles.read</c> and <c>roles.update</c>, read off the same claim the configuration's are,
/// and those reach that service alone. Update includes read, in both places. Administrator is
/// global only: there is no per-service Settings.
/// <para>
/// Silence is nobody. An operator holding no mapped value and named by no manifest sees no
/// service, which is the safe reading once services carry their own roles: a team's service is
/// for that team, and the tool is not told who else there is.
/// </para>
/// <para>
/// Open, there is nobody to refuse, and <see cref="Open"/> grants everything to anyone.
/// </para>
/// </remarks>
public sealed class ServiceAccess(AuthorizationSettings? settings)
{
    /// <summary>The access of a tool running open: everything, to anyone.</summary>
    public static ServiceAccess Open { get; } = new(null);

    /// <summary>
    /// Whether <paramref name="user"/> holds <paramref name="role"/> for <paramref name="service"/>,
    /// or holds it globally — the only way to hold it outside every service.
    /// </summary>
    /// <param name="user">The operator, as the request carries them after the claims transformation.</param>
    /// <param name="role">One of the three roles.</param>
    /// <param name="service">The service the address is under, or null outside every service.</param>
    public bool Grants(ClaimsPrincipal user, string role, Service? service)
    {
        if (settings is null)
        {
            return true;
        }

        if (user.FindAll(Roles.ClaimType).Any(claim => Roles.Includes(claim.Value, role)))
        {
            return true;
        }

        if (service is null || role == Roles.Administrator)
        {
            return false;
        }

        var sent = user.FindAll(settings.RoleClaimType).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);

        return role == Roles.Updater
            ? service.UpdateRoles.Any(sent.Contains)
            : service.ReadRoles.Concat(service.UpdateRoles).Any(sent.Contains);
    }

    /// <summary>
    /// The services among <paramref name="services"/> that <paramref name="user"/> may read, by
    /// name, whatever order their zips were installed in. One answer for every place that lists
    /// them — Home and the bar's Services menu — so a reader finds a service the same way in each,
    /// and no list shows a door the sign-in does not open.
    /// </summary>
    /// <remarks>
    /// Compared the way a reader compares words, case aside, rather than by code point, which
    /// would sort every capital before every lower-case letter.
    /// </remarks>
    public IReadOnlyList<Service> Readable(ClaimsPrincipal user, IEnumerable<Service> services) =>
        services
            .Where(service => Grants(user, Roles.Reader, service))
            .OrderBy(service => service.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
