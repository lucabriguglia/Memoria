using Memoria.EventSourcing.Dcb;

namespace Memoria.Web.Extensibility;

/// <summary>
/// What the compare tab asks for on a DCB page: which model, named by which identifier, at which
/// two versions.
/// </summary>
/// <param name="Model">The aggregate or projection type to fold.</param>
/// <param name="Identifier">
/// The rebuilt identifier instance, whose boundary selects the model's events — or null when it
/// could not be rebuilt, in which case the page says why and nothing is asked.
/// </param>
/// <param name="From">What the address says for the earlier version, or null when it says nothing.</param>
/// <param name="To">What the address says for the later version, or null when it says nothing.</param>
public sealed record BoundaryComparisonRequest(Type Model, object? Identifier, string? From, string? To);

/// <summary>
/// Two versions of one DCB model laid over each other, answered in the same shape the streamed
/// pages answer theirs.
/// </summary>
/// <remarks>
/// The difference from the streamed comparison is where the counting happens. A streamed page
/// reads its history a page at a time, so it counts and places versions through store reads; a
/// DCB page has the shape of its whole boundary in hand as headers, so a version is arithmetic
/// over that list: the last version is the count, and version N is the Nth header's position.
/// Only the two folds go to the store.
/// </remarks>
public static class BoundaryComparison
{
    /// <summary>
    /// Folds the model at each of the two versions and compares the results.
    /// </summary>
    /// <param name="history">The headers of the model's whole history in position order, or why they could not be read.</param>
    /// <param name="service">The DCB domain service, which does the folding.</param>
    /// <param name="request">Which model, named how, at which versions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<ModelComparison> Of(
        BoundaryHistory history,
        IDcbDomainService service,
        BoundaryComparisonRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Identifier is null)
        {
            return ModelComparison.None;
        }

        // A read that failed says nothing rather than nothing found: the range reader is told the
        // difference, and with no history there is no placing a version either.
        if (history.Error is not null)
        {
            return new ModelComparison(null, null, null, null, [], history.Error);
        }

        // In position order as the store hands them over, which is the order the fold applies them
        // in: a version is a place in that order, and no re-sorting is needed to find it.
        var rows = history.Rows;
        var reading = CompareRange.Of(request.From, request.To, rows.Count);

        if (reading.Range is not { } range)
        {
            return new ModelComparison(null, null, null, rows.Count, [], reading.Error);
        }

        // Where a version sits in the history: version zero is the fold up to zero and has no
        // event; every other version is the model's event at that place, which the range reader
        // has already kept within the count.
        FoldPoint Place(int version) =>
            version == 0
                ? new FoldPoint(0, 0, null, null, null)
                : new FoldPoint(version, rows[version - 1].Position, rows[version - 1].EventType, rows[version - 1].CreatedDate, rows[version - 1].CreatedBy);

        var from = Place(range.From);
        var to = Place(range.To);

        // Both versions out of one read, up to the later one: the earlier version is the first of
        // those events, since a version is the model's own count of them.
        var folded = await ModelFolder.Fold(
            service, request.Model, request.Identifier, from.Version, to.Sequence, cancellationToken);

        var error = folded.Error ?? (folded.Before is null || folded.After is null
            ? "The store folded nothing for one of the two versions."
            : null);

        return error is not null
            ? new ModelComparison(range, from, to, rows.Count, [], error)
            : new ModelComparison(range, from, to, rows.Count, StateDiff.Of(
                DomainTypeDescriber.ReadState(folded.Before!),
                DomainTypeDescriber.ReadState(folded.After!)), null);
    }
}
