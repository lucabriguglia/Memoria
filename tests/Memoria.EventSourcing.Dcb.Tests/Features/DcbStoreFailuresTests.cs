using FluentAssertions;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

/// <summary>
/// DCB failures reuse the classification introduced for the streamed stores in 1.5.0, so a caller
/// matching on <c>memoria/concurrency-conflict</c> works against either consistency model.
/// </summary>
public class DcbStoreFailuresTests
{
    private static readonly TagQuery Boundary =
        TagQuery.AnyOf(new Tag("seat", "a1"), new Tag("student", "s7"));

    [Fact]
    public void A_concurrency_conflict_carries_the_shared_classification()
    {
        var failure = DcbStoreFailures.ConcurrencyConflict(Boundary, expectedPosition: 7, latestPosition: 9);

        failure.ErrorCode.Should().Be(ErrorCode.Conflict);
        failure.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
    }

    [Fact]
    public void A_concurrency_conflict_carries_enough_to_retry_without_another_read()
    {
        var failure = DcbStoreFailures.ConcurrencyConflict(Boundary, expectedPosition: 7, latestPosition: 9);

        failure.Tags.Should().Contain("expectedPosition", "7");
        failure.Tags.Should().Contain("latestPosition", "9");
        failure.Tags.Should().ContainKey("tagQuery");
        failure.Tags!["tagQuery"].Should().Contain("seat:a1").And.Contain("student:s7");
    }

    [Fact]
    public void A_storage_failure_names_the_operation_and_nothing_from_the_provider()
    {
        // Provider detail names tables, columns and constraints; a Failure mapped onto an HTTP
        // response would disclose it. It goes on the Activity instead, as it does for streams.
        var failure = DcbStoreFailures.StorageFailure("Append Events", Boundary);

        failure.ErrorCode.Should().Be(ErrorCode.Error);
        failure.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
        failure.Tags.Should().Contain("operation", "Append Events");
    }

    [Fact]
    public void A_storage_failure_does_not_require_a_boundary()
    {
        var failure = DcbStoreFailures.StorageFailure("Append Events");

        failure.Tags.Should().Contain("operation", "Append Events");
        failure.Tags.Should().NotContainKey("tagQuery");
    }

    [Fact]
    public void An_oversized_append_is_reported_as_such()
    {
        var failure = DcbStoreFailures.BatchLimitExceeded("Append Events", requested: 1500, maximum: 1000);

        failure.ErrorCode.Should().Be(ErrorCode.BadRequest);
        failure.Type.Should().Be(EventSourcing.StoreFailures.BatchLimitExceededType);
        failure.Tags.Should().Contain("requestedEventCount", "1500");
        failure.Tags.Should().Contain("maximumEventCount", "1000");
    }
}
