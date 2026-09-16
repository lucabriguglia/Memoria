namespace Memoria.Web.Security;

/// <summary>
/// The three things an operator may be, each including the one before it.
/// </summary>
/// <remarks>
/// A Reader browses. An Updater may also press Update, which writes a snapshot. An Administrator
/// may also use Settings, which installs an assembly the process loads and runs. Each is granted
/// by mapping a claim the provider sends: globally, in configuration, for every service; or by a
/// service's own manifest, for that service — see <see cref="ServiceAccess"/>. The names are the
/// glossary's, and the policy each maps to carries the same name.
/// </remarks>
public static class Roles
{
    /// <summary>May read every page.</summary>
    public const string Reader = "Reader";

    /// <summary>May also refresh a stored snapshot.</summary>
    public const string Updater = "Updater";

    /// <summary>May also install, remove and reread the uploaded assemblies.</summary>
    public const string Administrator = "Administrator";

    /// <summary>The three, lowest first; a role includes every one before it.</summary>
    private static readonly string[] Ordered = [Reader, Updater, Administrator];

    /// <summary>Whether holding <paramref name="held"/> is holding <paramref name="wanted"/> too.</summary>
    public static bool Includes(string held, string wanted) =>
        Array.IndexOf(Ordered, held) >= Array.IndexOf(Ordered, wanted);

    /// <summary>
    /// The claim the tool's own roles are carried in, once mapped. Its own rather than the
    /// provider's, so that a provider group happening to be called Administrator grants nothing
    /// until configuration says it does.
    /// </summary>
    public const string ClaimType = "memoria-web:role";
}
