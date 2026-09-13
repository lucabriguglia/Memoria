using System;

namespace Memoria.Web.Tests;

/// <summary>The parts of a rendered page a test asks about.</summary>
internal static class Markup
{
    /// <summary>The page's header alone, so a word in the body does not stand in for one up there.</summary>
    public static string Header(string page)
    {
        var start = page.IndexOf("<header", StringComparison.Ordinal);
        var end = page.IndexOf("</header>", StringComparison.Ordinal);

        return start >= 0 && end > start ? page[start..end] : string.Empty;
    }
}
