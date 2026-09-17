namespace Memoria.Web.Components.Shared;

/// <summary>
/// What a tile or a menu item leads to, as the mark drawn in front of its name.
/// </summary>
/// <remarks>
/// Named rather than left to each page to draw: the same few things are reached from the home
/// page, both overviews, every section page and the bar over all of them, and a mark that meant
/// one thing on one of them and another elsewhere would be worse than no mark at all.
/// <see cref="SectionMark"/> is where each one is drawn.
/// </remarks>
public enum SectionKind
{
    /// <summary>The home page, which the bar leads back to from everywhere.</summary>
    Home,

    /// <summary>The streamed model as a whole: its heading on the bar, over its sections.</summary>
    Streamed,

    /// <summary>The DCB model as a whole: its heading on the bar, over its sections.</summary>
    Dcb,

    /// <summary>What is registered under a model, as the page that says so.</summary>
    Overview,

    /// <summary>The types a model declares.</summary>
    Types,

    /// <summary>What has been stored of them.</summary>
    Data,

    /// <summary>The events a model can apply.</summary>
    Events,

    /// <summary>The write models.</summary>
    Aggregates,

    /// <summary>The read models.</summary>
    Projections,

    /// <summary>The streams events are held in, which only the streamed model has.</summary>
    Streams,

    /// <summary>A service: a named set of domain assemblies read over one store, as the Installed table lists one.</summary>
    Service,

    /// <summary>A page of the documentation, which is outside the tool.</summary>
    Documentation,

    /// <summary>The tool's own settings, which run on the host and are an Administrator's to change.</summary>
    Settings,

    /// <summary>The signed-in operator, named on the bar over what is theirs alone.</summary>
    Operator,

    /// <summary>This browser's preferences, which are nobody else's business.</summary>
    Preferences,

    /// <summary>The way out of a session.</summary>
    SignOut
}
