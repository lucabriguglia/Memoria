using FluentAssertions;
using Memoria.EventSourcing.Filtering;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.Filtering;

public class EventPropertyFilterValueTests
{
    [Theory]
    [InlineData("null", "null")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("0", "0")]
    [InlineData("25", "25")]
    [InlineData("-25", "-25")]
    [InlineData("25.45", "25.45")]
    [InlineData("-25.45", "-25.45")]
    public void Coerces_scalars_into_their_unquoted_json_form(string input, string expected)
    {
        EventPropertyFilterValue.ToJsonLiteral(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Alice", "\"Alice\"")]
    [InlineData("", "\"\"")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000", "\"550e8400-e29b-41d4-a716-446655440000\"")]
    [InlineData("25abc", "\"25abc\"")]
    [InlineData("0025", "\"0025\"")]
    [InlineData(".5", "\".5\"")]
    public void Treats_non_round_tripping_values_as_strings(string input, string expected)
    {
        EventPropertyFilterValue.ToJsonLiteral(input).Should().Be(expected);
    }

    [Fact]
    public void Escapes_special_characters_in_string_values()
    {
        EventPropertyFilterValue.ToJsonLiteral("she said \"hi\"")
            .Should().Be("\"she said \\\"hi\\\"\"");
    }
}
