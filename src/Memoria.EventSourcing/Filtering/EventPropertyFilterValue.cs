using System.Globalization;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Filtering;

/// <summary>
/// Coerces a stringly-typed event-property filter value into the JSON-scalar literal that
/// Newtonsoft.Json would produce when serializing the corresponding CLR type. Stores can use
/// this when constructing predicates against serialized event data so that filters work for
/// numbers, booleans, and null — not just strings.
/// </summary>
public static class EventPropertyFilterValue
{
    /// <summary>
    /// Returns the JSON-literal form of the supplied filter value.
    /// </summary>
    /// <remarks>
    /// Coercion rules (round-trip safe — values that do not round-trip exactly are treated as strings):
    /// <list type="bullet">
    ///   <item><description><c>"null"</c> → <c>null</c></description></item>
    ///   <item><description><c>"true"</c> / <c>"false"</c> → JSON boolean</description></item>
    ///   <item><description>integer literal → JSON number</description></item>
    ///   <item><description>decimal literal → JSON number</description></item>
    ///   <item><description>anything else → JSON-escaped string (quoted)</description></item>
    /// </list>
    /// </remarks>
    public static string ToJsonLiteral(string value)
    {
        if (value == "null") return "null";
        if (value == "true") return "true";
        if (value == "false") return "false";

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            && integer.ToString(CultureInfo.InvariantCulture) == value)
        {
            return value;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            && number.ToString(CultureInfo.InvariantCulture) == value)
        {
            return value;
        }

        return JsonConvert.ToString(value);
    }
}
