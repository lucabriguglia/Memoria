using Microsoft.Extensions.Configuration;

namespace Memoria.Web.Security;

/// <summary>
/// How operators reach this tool: signed in through an OpenID Connect provider, or not at all.
/// </summary>
/// <remarks>
/// Decided once before the application listens, the way the store is. There is no third answer
/// and no default: a tool told neither refuses to start. The settings page takes an assembly and
/// runs it, so "nobody said" cannot mean "anybody may" — whoever deploys it has to write down
/// which of the two they want, and the refusal names the settings that would say so.
/// </remarks>
public abstract record AuthenticationSettings
{
    /// <summary>The setting that runs the tool open, in so many words.</summary>
    public const string DisabledSetting = "Authentication:Disabled";

    /// <summary>The section the provider is described in.</summary>
    public const string OidcSection = "Authentication:Oidc";

    /// <summary>The setting that names the provider.</summary>
    public const string AuthoritySetting = $"{OidcSection}:Authority";

    /// <summary>The setting that names this tool to the provider.</summary>
    public const string ClientIdSetting = $"{OidcSection}:ClientId";

    /// <summary>The setting that proves this tool to the provider.</summary>
    public const string ClientSecretSetting = $"{OidcSection}:ClientSecret";

    /// <summary>The setting that says what is asked of the provider, space-separated.</summary>
    public const string ScopesSetting = $"{OidcSection}:Scopes";

    /// <summary>
    /// What is asked for when the setting is silent: the identity, the name to show, and the email
    /// address a provider may put its groups against.
    /// </summary>
    public const string DefaultScopes = "openid profile email";

    /// <summary>Nobody is signed in and every page answers anyone. Asked for, never assumed.</summary>
    public sealed record Disabled : AuthenticationSettings;

    /// <summary>
    /// Operators are signed in through the provider at <paramref name="Authority"/>.
    /// </summary>
    /// <param name="Authority">The issuer, where the provider's discovery document is read from.</param>
    /// <param name="ClientId">What this tool is registered as at the provider.</param>
    /// <param name="ClientSecret">What this tool proves that registration with.</param>
    /// <param name="Scopes">What is asked of the provider, space-separated.</param>
    public sealed record OpenIdConnect(string Authority, string ClientId, string ClientSecret, string Scopes)
        : AuthenticationSettings;

    /// <summary>
    /// Reads which of the two the configuration asks for.
    /// </summary>
    /// <param name="configuration">The application's configuration.</param>
    /// <returns>The provider to sign in through, or the decision to run open.</returns>
    /// <exception cref="InvalidOperationException">
    /// Neither is asked for, both are, or the provider is described with one of its three settings
    /// missing.
    /// </exception>
    public static AuthenticationSettings Of(IConfiguration configuration)
    {
        var disabled = RunsOpen(configuration);

        // Described at all, rather than described fully: a section with one of its three settings
        // is a provider with a setting missing, not silence, and is answered by naming that one.
        var described = new[] { AuthoritySetting, ClientIdSetting, ClientSecretSetting, ScopesSetting }
            .Any(setting => Value(configuration, setting) is not null);

        if (disabled && described)
        {
            throw new InvalidOperationException(
                $"{DisabledSetting} is true and {OidcSection} is set, and these ask for opposite " +
                "things. Remove one of them: the setting, to sign operators in through the " +
                "provider, or the section, to run this tool open.");
        }

        if (disabled)
        {
            return new Disabled();
        }

        if (!described)
        {
            throw new InvalidOperationException(
                $"Authentication is not configured. Set {AuthoritySetting}, {ClientIdSetting} and " +
                $"{ClientSecretSetting} to sign operators in through an OpenID Connect provider, " +
                $"or set {DisabledSetting} to true to run this tool open, which leaves its upload " +
                "form to anyone who can reach it.");
        }

        return new OpenIdConnect(
            Authority: Required(configuration, AuthoritySetting),
            ClientId: Required(configuration, ClientIdSetting),
            ClientSecret: Required(configuration, ClientSecretSetting),
            Scopes: Value(configuration, ScopesSetting) ?? DefaultScopes);
    }

    /// <summary>
    /// Whether the tool was told to run open. Told nothing, or told with nothing after the equals
    /// sign, it was not; told something that is neither a yes nor a no, it refuses to guess.
    /// </summary>
    private static bool RunsOpen(IConfiguration configuration)
    {
        var value = Value(configuration, DisabledSetting);

        if (value is null)
        {
            return false;
        }

        return bool.TryParse(value, out var disabled)
            ? disabled
            : throw new InvalidOperationException(
                $"{DisabledSetting} is '{value}', which is neither true nor false.");
    }

    private static string Required(IConfiguration configuration, string setting) =>
        Value(configuration, setting)
        ?? throw new InvalidOperationException(
            $"{setting} is not configured. The provider needs {AuthoritySetting}, " +
            $"{ClientIdSetting} and {ClientSecretSetting} all set.");

    /// <summary>A setting's value, with an empty one read as no value at all.</summary>
    private static string? Value(IConfiguration configuration, string setting) =>
        configuration[setting] is { Length: > 0 } value ? value : null;
}
