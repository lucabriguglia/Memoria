using System.Security.Cryptography;
using System.Text;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

/// <summary>
/// A persisted fold of a boundary into an aggregate or a projection.
/// </summary>
/// <remarks>
/// One table for both, because they differ only in which identifier names them. The kind is part of
/// the identity, so an aggregate and a projection sharing an id do not collide.
/// </remarks>
public class DcbSnapshotEntity : IAuditableEntity, IEditableEntity
{
    /// <summary>
    /// The kind discriminator for a write model.
    /// </summary>
    public const string AggregateKind = "Aggregate";

    /// <summary>
    /// The kind discriminator for a read model.
    /// </summary>
    public const string ProjectionKind = "Projection";

    /// <summary>
    /// Gets or sets the identity: kind, store id and a digest of the boundary.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether this is an aggregate or a projection snapshot.
    /// </summary>
    public string SnapshotKind { get; set; } = null!;

    /// <summary>
    /// Gets or sets the model's store id, <c>{id}:{type version}</c>.
    /// </summary>
    public string StoreId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the canonical form of the boundary this was folded under.
    /// </summary>
    /// <remarks>
    /// Kept in full, not just as the digest in <see cref="Id"/>, so a read can confirm it found the
    /// snapshot it meant to and so an operator can see which boundary produced a given state.
    /// </remarks>
    public string TagQuery { get; set; } = null!;

    /// <summary>
    /// Gets or sets the model type binding key.
    /// </summary>
    public string ModelType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the model's version at the time of the fold.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the global position the fold reached.
    /// </summary>
    public long LatestPosition { get; set; }

    /// <summary>
    /// Gets or sets the serialised model.
    /// </summary>
    public string Data { get; set; } = null!;

    /// <inheritdoc />
    public DateTimeOffset CreatedDate { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedDate { get; set; }

    /// <inheritdoc />
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Builds the identity of a snapshot.
    /// </summary>
    /// <param name="kind"><see cref="AggregateKind"/> or <see cref="ProjectionKind"/>.</param>
    /// <param name="storeId">The model's store id.</param>
    /// <param name="query">The boundary the model was folded under.</param>
    /// <returns>The identity.</returns>
    /// <remarks>
    /// The boundary is part of the identity because a snapshot is only a valid answer for the query
    /// that produced it: the same aggregate id folded over a wider boundary is a different state,
    /// and returning one for the other would be silently wrong. It enters as a digest rather than in
    /// full because a boundary naming many tags would otherwise overflow what a database will index
    /// as a key. <see cref="TagQuery"/> carries the full form, and every read compares it, so a
    /// digest collision cannot return the wrong snapshot.
    /// </remarks>
    public static string BuildId(string kind, string storeId, TagQuery query)
    {
        var digest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(query.ToString())))[..32];

        return $"{kind}:{storeId}:{digest}";
    }
}
