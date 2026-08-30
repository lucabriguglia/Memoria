using FluentAssertions;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.Tests;

/// <summary>
/// Asserts that a store operation succeeded before a test goes on to assert on its side effects.
/// </summary>
/// <remarks>
/// Without this, a store that is unreachable fails the *later* assertion — "no telemetry was
/// recorded", "the aggregate was not saved" — which reads like the behaviour under test is broken
/// when the operation never ran at all. These helpers put the store's own failure in the message.
/// </remarks>
public static class ResultAssertions
{
    public static void ShouldHaveSucceeded(this Result result) =>
        result.IsSuccess.Should().BeTrue(Because(result.Failure));

    public static void ShouldHaveSucceeded<T>(this Result<T> result) =>
        result.IsSuccess.Should().BeTrue(Because(result.Failure));

    private static string Because(Failure? failure) => failure is null
        ? "the operation should have succeeded"
        : $"the operation should have succeeded, but failed with {failure.Type ?? "no type"}: " +
          $"{failure.Description ?? failure.Title ?? failure.ErrorCode.ToString()}";
}
