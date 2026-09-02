using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;
using Memoria.Queries;
using Memoria.Results;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Queries;

public record GetStudentTranscriptQuery(string StudentId) : IQuery<StudentTranscript?>;

/// <summary>
/// Reads a student's transcript, building the snapshot the first time and bringing it up to date on
/// every read after that.
/// </summary>
/// <remarks>
/// <para>
/// A query, not a command, because nothing here decides anything: it appends no events and takes no
/// <see cref="AppendCondition"/>. The snapshot it writes is a cache of a fold, not a fact about the
/// world, so there is nothing for a condition to protect — a concurrent append simply means the next
/// read folds one more event.
/// </para>
/// <para>
/// <see cref="ReadMode.SnapshotWithNewEventsOrCreate"/> is the mode that does both halves. With no
/// snapshot it folds the boundary from the beginning and saves the result; with one it applies only
/// what arrived after <c>LatestPosition</c> and saves it again. Either way the caller gets a
/// transcript that is current as of the read, and the log is walked from the beginning exactly once.
/// </para>
/// <para>
/// The three narrower modes are all wrong here, which is what makes this one worth naming:
/// <c>SnapshotOnly</c> returns a stale transcript, <c>SnapshotOrCreate</c> returns a stale one
/// whenever a snapshot already exists, and <c>SnapshotWithNewEvents</c> returns null until something
/// else has built the snapshot first.
/// </para>
/// </remarks>
public class GetStudentTranscriptQueryHandler(IDcbDomainService dcb)
    : IQueryHandler<GetStudentTranscriptQuery, StudentTranscript?>
{
    public async Task<Result<StudentTranscript?>> Handle(GetStudentTranscriptQuery query,
        CancellationToken cancellationToken = default) =>
        await dcb.GetProjection(new StudentTranscriptId(query.StudentId),
            ReadMode.SnapshotWithNewEventsOrCreate, cancellationToken);
}
