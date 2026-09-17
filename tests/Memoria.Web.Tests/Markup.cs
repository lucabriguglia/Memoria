using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Memoria.Web.Tests;

/// <summary>The parts of a rendered page a test asks about.</summary>
internal static class Markup
{
    /// <summary>
    /// The page as written, without the attribute scoped CSS stamps on every element of a
    /// component that has a stylesheet of its own: a test says what the page says, not which
    /// component said it.
    /// </summary>
    public static string Plain(string page) => Regex.Replace(page, " b-[a-z0-9]{10}(?=[ >/])", string.Empty);

    /// <summary>
    /// The markup without the marks drawn in front of words: a test asking what a menu says reads
    /// the words, and the glyph before them is not one.
    /// </summary>
    public static string Unmarked(string markup) =>
        Regex.Replace(markup, "<svg.*?</svg>\\s*", string.Empty, RegexOptions.Singleline);

    /// <summary>The page's header alone, so a word in the body does not stand in for one up there.</summary>
    public static string Header(string page)
    {
        page = Plain(page);
        var start = page.IndexOf("<header", StringComparison.Ordinal);
        var end = page.IndexOf("</header>", StringComparison.Ordinal);

        return start >= 0 && end > start ? page[start..end] : string.Empty;
    }

    /// <summary>The page's breadcrumb alone, so a link in the body does not stand in for a crumb.</summary>
    public static string Breadcrumb(string page)
    {
        page = Plain(page);
        var start = page.IndexOf("<nav class=\"breadcrumb\"", StringComparison.Ordinal);
        var end = start >= 0 ? page.IndexOf("</nav>", start, StringComparison.Ordinal) : -1;

        return start >= 0 && end > start ? page[start..end] : string.Empty;
    }

    /// <summary>
    /// The labels along the top of the header's main menu, in order: each section heading and each
    /// plain link, without what is folded under a heading.
    /// </summary>
    public static string[] MenuBar(string page)
    {
        // The first nav is the bar; the operator's own menu is a second one after it.
        var header = Header(page);
        var start = Regex.Match(header, "<nav(\\s[^>]*)?>").Index;
        var end = header.IndexOf("</nav>", StringComparison.Ordinal);
        var nav = Unmarked(end > start ? header[start..end] : string.Empty);

        // A heading is a <summary>; a plain link is an <a> that is not folded under one.
        var labels = new List<string>();
        var folded = false;

        foreach (Match match in Regex.Matches(
                     nav, "<summary[^>]*>(?<summary>[^<]*)</summary>|<div class=\"submenu\"|</details>|<a [^>]*>(?<link>[^<]*)</a>"))
        {
            if (match.Groups["summary"].Success)
            {
                labels.Add(match.Groups["summary"].Value.Trim());
            }
            else if (match.Value.StartsWith("<div", StringComparison.Ordinal))
            {
                folded = true;
            }
            else if (match.Value == "</details>")
            {
                folded = false;
            }
            else if (!folded)
            {
                labels.Add(match.Groups["link"].Value.Trim());
            }
        }

        return [.. labels];
    }

    /// <summary>One thing in a menu, and whether a mark is drawn in front of its words.</summary>
    public sealed record MenuItem(string Label, bool Marked);

    /// <summary>
    /// The links the Installed table's Services column carries, in order: where each leads and
    /// the name it shows, without the mark drawn in front of it.
    /// </summary>
    public static (string Href, string Name)[] ServiceLinks(string page) =>
        Regex.Matches(page, "<a class=\"service-link\" href=\"(?<href>[^\"]+)\">(?<inner>.*?)</a>", RegexOptions.Singleline)
            .Select(match => (match.Groups["href"].Value, Unmarked(match.Groups["inner"].Value).Trim()))
            .ToArray();

    /// <summary>
    /// Everything in the header's menus that can be chosen — each heading, each link and each
    /// button, on the bar and folded under a heading alike, in the order written. The brand is
    /// not one of them: it is the mark of the tool, not a place in it.
    /// </summary>
    public static MenuItem[] MenuItems(string page)
    {
        var items = new List<MenuItem>();

        foreach (Match item in Regex.Matches(
                     Header(page),
                     "<(?<tag>summary|a|button)\\b(?![^>]*class=\"brand\")[^>]*>(?<inner>.*?)</\\k<tag>>",
                     RegexOptions.Singleline))
        {
            var inner = item.Groups["inner"].Value;

            items.Add(new MenuItem(Unmarked(inner).Trim(), Regex.IsMatch(inner, "^\\s*<svg class=\"glyph\"")));
        }

        return [.. items];
    }

    /// <summary>
    /// The names down the index beside a Types page's panel, in the order written: what a reader
    /// can pick from, after any narrowing and whichever group each sits in.
    /// </summary>
    public static string[] IndexNames(string page)
    {
        var names = new List<string>();

        foreach (Match match in Regex.Matches(page, "<span class=\"index-name\"[^>]*>(?<name>[^<]*)"))
        {
            names.Add(match.Groups["name"].Value.Trim());
        }

        return [.. names];
    }

    /// <summary>One fold of the index, and whether it is open.</summary>
    public sealed record IndexGroup(string Name, bool Open);

    /// <summary>
    /// The groups the index is folded into, in the order written, or none when it is one flat list.
    /// </summary>
    public static IndexGroup[] IndexGroups(string page)
    {
        var groups = new List<IndexGroup>();

        foreach (Match match in Regex.Matches(
                     Plain(page),
                     "<details class=\"index-group\"(?<open> open)?>\\s*<summary class=\"index-group-name\">(?<name>[^<]*)</summary>"))
        {
            groups.Add(new IndexGroup(match.Groups["name"].Value.Trim(), match.Groups["open"].Success));
        }

        return [.. groups];
    }
}
