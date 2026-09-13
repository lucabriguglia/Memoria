using Microsoft.Extensions.Configuration;

namespace Memoria.Web.Security;

/// <summary>
/// Which of the provider's claim values make an operator an Updater or an Administrator, and
/// which claim to look for them in.
/// </summary>
/// <param name="RoleClaimType">The claim the provider puts its groups or roles in.</param>
/// <param name="Administrators">The values of that claim that make an operator an Administrator.</param>
/// <param name="Updaters">The values of that claim that make an operator an Updater.</param>
/// <remarks>
/// Read off configuration because every provider does this differently: Entra ID puts group ids
/// under <c>groups</c> and app roles under <c>roles</c>, Cognito uses <c>cognito:groups</c>,
/// Keycloak nests realm roles unless told to flatten them. The tool asks for one claim by name
/// and for the values under it that mean something here. Nothing is mapped until it is said:
/// silence makes every signed-in operator a Reader, which is the safe reading of silence.
/// </remarks>
public sealed record AuthorizationSettings(
    string RoleClaimType,
    IReadOnlyList<string> Administrators,
    IReadOnlyList<string> Updaters)
{
    /// <summary>The setting that names the claim the roles are read from.</summary>
    public const string RoleClaimTypeSetting = "Authorization:RoleClaimType";

    /// <summary>The setting listing the claim values that make an Administrator.</summary>
    public const string AdministratorSetting = "Authorization:Roles:Administrator";

    /// <summary>The setting listing the claim values that make an Updater.</summary>
    public const string UpdaterSetting = "Authorization:Roles:Updater";

    /// <summary>The claim read when the setting is silent: what most providers call it.</summary>
    public const string DefaultRoleClaimType = "roles";

    /// <summary>Whether any value maps to any role, or every operator is a Reader.</summary>
    public bool MapsAnyone => Administrators.Count > 0 || Updaters.Count > 0;

    /// <summary>
    /// Reads the mapping off configuration.
    /// </summary>
    /// <param name="configuration">The application's configuration.</param>
    /// <returns>The mapping, with nobody mapped where nothing was said.</returns>
    public static AuthorizationSettings Of(IConfiguration configuration) => new(
        RoleClaimType: configuration[RoleClaimTypeSetting] is { Length: > 0 } claim ? claim : DefaultRoleClaimType,
        Administrators: Listed(configuration[AdministratorSetting]),
        Updaters: Listed(configuration[UpdaterSetting]));

    /// <summary>
    /// One setting read as a comma-separated list. One string rather than a configuration array
    /// so it fits in one environment variable; the values are group and role names, which carry
    /// no commas. Empty entries are dropped, since no claim ever carries the empty string.
    /// </summary>
    private static IReadOnlyList<string> Listed(string? setting) =>
        setting is null
            ? []
            : setting.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Record equality over the lists' contents, since arrays compare by reference.</summary>
    public bool Equals(AuthorizationSettings? other) =>
        other is not null &&
        RoleClaimType == other.RoleClaimType &&
        Administrators.SequenceEqual(other.Administrators) &&
        Updaters.SequenceEqual(other.Updaters);

    public override int GetHashCode() =>
        HashCode.Combine(RoleClaimType, Administrators.Count, Updaters.Count);
}
