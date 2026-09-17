using Memoria.EventSourcing;

namespace Memoria.Web.Extensibility;

/// <summary>
/// What the compare tab asks for: which model, folded from which stream through which identifier,
/// at which two versions.
/// </summary>
/// <param name="Model">The aggregate or projection type to fold.</param>
/// <param name="Identity">The stream and identifier worked back out of the row's ids.</param>
/// <param name="StreamId">The stream's id as the store keys it, for the reads that count and place the model's events.</param>
/// <param name="EventTypes">The binding keys of the types the model applies, or null when it applies everything.</param>
/// <param name="From">What the address says for the earlier version, or null when it says nothing.</param>
/// <param name="To">What the address says for the later version, or null when it says nothing.</param>
public sealed record ComparisonRequest(
    Type Model,
    StreamedIdentity Identity,
    string StreamId,
    IReadOnlyList<string>? EventTypes,
    string? From,
    string? To);

/// <summary>
/// One version of a model as a point in its history: the version, the place in the log it was
/// folded up to, and what is known of the event that produced it.
/// </summary>
/// <param name="Version">The version, which is the model's own count of its events.</param>
/// <param name="Sequence">
/// The place the fold stopped at — a sequence in a stream, or a position in the one DCB log — or
/// zero for version zero.
/// </param>
/// <param name="Type">The binding key the event that produced this version was stored under, or null for version zero.</param>
/// <param name="Written">When that event was appended, or null for version zero.</param>
/// <param name="WrittenBy">Who appended it, or null for version zero or where the store attributes nothing.</param>
/// <remarks>
/// What the cards say of a version and nothing more: the type, the date and the author, not the payload,
/// so a history can be read as headers without the payloads that make it heavy. Version zero is
/// the model before anything happened to it and has no event of its own.
/// </remarks>
public sealed record FoldPoint(int Version, long Sequence, string? Type, DateTimeOffset? Written, string? WrittenBy);

/// <summary>
/// Two versions of one model laid over each other: which they are, what each was folded up to, and
/// the rows where they differ.
/// </summary>
/// <param name="Range">The two versions, or null when the address names no pair that can be folded.</param>
/// <param name="From">The earlier version as a point in the history, or null when nothing was folded.</param>
/// <param name="To">The later version as a point in the history, or null when nothing was folded.</param>
/// <param name="LastVersion">
/// The model's last version, which is how many of its events there are, or null when the history
/// could not be counted. What a pair is checked against, and the most a form offers.
/// </param>
/// <param name="Rows">The later version's state against the earlier's, or empty when there is nothing to show.</param>
/// <param name="Error">Why there is nothing to show: a pair the address could not be read as, or a fold the store refused.</param>
public sealed record ModelComparison(
    CompareRange? Range,
    FoldPoint? From,
    FoldPoint? To,
    long? LastVersion,
    IReadOnlyList<DiffRow> Rows,
    string? Error)
{
    /// <summary>Nothing asked and nothing answered, for a row whose identity could not be rebuilt.</summary>
    public static readonly ModelComparison None = new(null, null, null, null, [], null);

    /// <summary>
    /// Folds the model at each of the two versions and compares the results.
    /// </summary>
    /// <param name="reads">The tool's own reads, for counting and placing the model's events.</param>
    /// <param name="service">The store's domain service, which does the folding.</param>
    /// <param name="request">Which model, from where, at which versions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Only once the identifier is known to have been rebuilt: the panel says why when it was not,
    /// and there is nothing to ask the store for. The model's own history is counted first —
    /// narrowed the way the events tab is — because the count is the last version, which a pair
    /// is checked against and which an address naming no pair compares by default. A count that
    /// failed says nothing rather than nothing found: a history the store could not read is not an
    /// empty one, and the range reader is told the difference.
    /// <para>
    /// A version is then placed in the history by reading the model's event at that index, oldest
    /// first: the store folds up to a sequence, and a version's sequence is the one thing it does
    /// not know. Each is the read made for it — a count with no rows, a row with no count — rather
    /// than a page of one, which would be both at twice the cost.
    /// </para>
    /// </remarks>
    public static async Task<ModelComparison> Of(
        IStreamedReads reads,
        IDomainService service,
        ComparisonRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = request.Identity;

        if (!identity.IsRecovered || identity.Claim is null)
        {
            return None;
        }

        var history = new StreamedEventFilter(
            StreamPattern: request.StreamId,
            EventType: null,
            Text: null,
            Descending: false,
            Page: 1,
            Size: 1)
        {
            EventTypes = request.EventTypes,
            Properties = identity.Claim
        };

        var counted = await reads.Count(history, cancellationToken);

        long? lastVersion = counted.Error is null ? counted.Total : null;

        var reading = CompareRange.Of(request.From, request.To, lastVersion);

        if (reading.Range is not { } range)
        {
            return new ModelComparison(null, null, null, lastVersion, [], reading.Error);
        }

        if (lastVersion is null)
        {
            return new ModelComparison(range, null, null, null, [],
                "The history could not be read, so there is no saying which event a version was folded up to.");
        }

        // Where a version sits in the history: version zero is the fold up to zero and has no
        // event; every other version is the model's event at that place.
        async Task<FoldPoint?> Place(int version)
        {
            if (version == 0)
            {
                return new FoldPoint(0, 0, null, null, null);
            }

            var placed = await reads.At(history, version - 1, cancellationToken);

            return placed.Error is null && placed.Event is { } found
                ? new FoldPoint(version, found.Event.Position, found.Event.Type, found.Event.Written, found.Event.WrittenBy)
                : null;
        }

        var from = await Place(range.From);
        var to = await Place(range.To);

        if (from is null || to is null)
        {
            return new ModelComparison(range, null, null, lastVersion, [],
                "The event one of the two versions was folded up to could not be found in the history.");
        }

        // Both versions out of one read, up to the later one: the earlier version is the first of
        // those events, since a version is the model's own count of them.
        var folded = await ModelFolder.Fold(
            service, request.Model, identity.Stream!, identity.Identifier!, from.Version,
            checked((int)to.Sequence), cancellationToken);

        var error = folded.Error ?? (folded.Before is null || folded.After is null
            ? "The store folded nothing for one of the two versions."
            : null);

        return error is not null
            ? new ModelComparison(range, from, to, lastVersion, [], error)
            : new ModelComparison(range, from, to, lastVersion, StateDiff.Of(
                DomainTypeDescriber.ReadState(folded.Before!),
                DomainTypeDescriber.ReadState(folded.After!)), null);
    }
}
