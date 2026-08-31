// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

/// <summary>
/// One tag on one event. The primary key is <c>(Tag, Position)</c> because every query runs
/// tag-first, so the key itself is the serving index and no secondary index on <see cref="Tag"/> is
/// needed.
/// </summary>
public class DcbEventTagEntity
{
    /// <summary>
    /// Gets or sets the tagged event's position.
    /// </summary>
    public long Position { get; set; }

    /// <summary>
    /// Gets or sets the tag, in its canonical <c>{key}:{value}</c> form.
    /// </summary>
    public string Tag { get; set; } = null!;

    /// <summary>
    /// Gets or sets the tagged event.
    /// </summary>
    public DcbEventEntity Event { get; set; } = null!;
}
