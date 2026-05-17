using System.IO.Hashing;
using System.Text;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql;

/// <summary>
/// Computes deterministic <c>pg_advisory_xact_lock</c> keys for DCB conditions.
/// </summary>
/// <remarks>
/// Each (key, value) tag becomes one 64-bit lock key. Keys are returned in ascending order so
/// any two writers that share a tag acquire the corresponding lock in the same order — that
/// rules out deadlocks across the writer pool.
/// </remarks>
internal static class AdvisoryLockKey
{
    private const string Prefix = "memoria.dcb:";

    public static IReadOnlyList<long> ForCondition(AppendCondition condition)
    {
        var keys = new SortedSet<long>();
        foreach (var item in condition.Query.Items)
        {
            foreach (var tag in item.Tags.Tags)
            {
                keys.Add(Hash(tag.Key, tag.Value));
            }
        }
        return keys.ToList();
    }

    private static long Hash(string key, string value)
    {
        var bytes = Encoding.UTF8.GetBytes($"{Prefix}{key}={value}");
        var hash = XxHash64.HashToUInt64(bytes);
        return unchecked((long)hash);
    }
}
