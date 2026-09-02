using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.Cosmos;

/// <summary>
/// Failures particular to the Cosmos DB store.
/// </summary>
public static class CosmosStoreFailures
{
    /// <summary>
    /// A point read by id returned a document of a different kind.
    /// </summary>
    public const string DocumentIdCollisionType = "memoria/document-id-collision";

    /// <summary>
    /// Reports that two documents of different kinds want the same id in the same partition.
    /// </summary>
    /// <param name="operation">The operation that found it.</param>
    /// <param name="streamId">The stream, which is also the partition key.</param>
    /// <param name="documentId">The id both documents want.</param>
    /// <param name="expected">The kind the caller asked for.</param>
    /// <param name="actual">The kind actually stored there.</param>
    /// <remarks>
    /// <para>
    /// Events, aggregates and projections share one container and one partition key, and their ids
    /// are built from different things: an event is <c>{streamId}:{sequence}</c>, an aggregate
    /// <c>{aggregateId}:{typeVersion}</c>, a projection <c>{projectionId}:{typeVersion}</c>. Give a
    /// stream and an aggregate the same string and a version 1 aggregate lands on the id of the event
    /// at sequence 1.
    /// </para>
    /// <para>
    /// Reported rather than swallowed. Treating the wrong document as absent would rebuild the
    /// aggregate from events and then upsert the snapshot over the colliding one, which is the
    /// version of this that loses data instead of naming the problem.
    /// </para>
    /// </remarks>
    public static Failure DocumentIdCollision(string operation, IStreamId streamId, string documentId,
        string expected, string actual) =>
        // Not Conflict: that is documented as retryable against the state as it now stands, and an
        // identifier collision is a modelling mistake that will fail identically every time.
        new(ErrorCode.UnprocessableEntity,
            Title: operation,
            Description:
            $"Document '{documentId}' in partition '{streamId.Id}' is a {actual} document, but a {expected} " +
            "document was expected. Events, aggregates and projections share one container, so their " +
            "identifiers must not collide: an event is '{streamId}:{sequence}', an aggregate " +
            "'{aggregateId}:{typeVersion}' and a projection '{projectionId}:{typeVersion}'. Give the " +
            "aggregate or projection an identifier that differs from the stream identifier.",
            Type: DocumentIdCollisionType,
            Tags: new Dictionary<string, string>
            {
                { "streamId", streamId.Id },
                { "documentId", documentId },
                { "expectedDocumentType", expected },
                { "actualDocumentType", actual }
            });
}
