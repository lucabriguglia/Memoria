using Memoria.EventSourcing.Domain;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Which of the two streamed models a page is reading.
/// </summary>
/// <remarks>
/// The streamed store keeps the two in separate tables, unlike the DCB one — but the tables hold the
/// same columns and mean the same things by them, because a snapshot of either is a model folded
/// from a stream and written down. So the pages that read them are the same shape, and what differs
/// is named here once rather than in each page that has to know it.
/// </remarks>
public enum StreamedModelKind
{
    /// <summary>A write model: folded from a stream, and able to append to it.</summary>
    Aggregate,

    /// <summary>A read model: folded from a stream, and never appending to it.</summary>
    Projection
}

/// <summary>
/// What each kind of model is called by the store, the catalogue and the framework.
/// </summary>
public static class StreamedModels
{
    /// <summary>The models of one kind that the uploaded assemblies declare.</summary>
    public static IReadOnlyList<Type> Streamed(this DomainTypeCatalogue catalogue, StreamedModelKind kind) =>
        kind switch
        {
            StreamedModelKind.Projection => catalogue.StreamedProjections,
            _ => catalogue.StreamedAggregates
        };

    /// <summary>The identifiers that could address a model of one kind.</summary>
    public static IReadOnlyList<Type> Identifiers(
        this DomainTypeCatalogue catalogue, StreamedModelKind kind) =>
        kind switch
        {
            StreamedModelKind.Projection => catalogue.StreamedProjectionIds,
            _ => catalogue.StreamedAggregateIds
        };

    /// <summary>
    /// The framework's map from a stored key to the type that was written under it, for the kind of
    /// model being read.
    /// </summary>
    /// <remarks>
    /// One map per kind because one key can name both: a projection and an aggregate may be bound
    /// under the same name, and the store tells them apart by which table it wrote the row into.
    /// </remarks>
    public static IReadOnlyDictionary<string, Type> Bindings(this StreamedModelKind kind, TypeBindingSet bindings) =>
        kind switch
        {
            StreamedModelKind.Projection => bindings.ProjectionTypeBindings,
            _ => bindings.AggregateTypeBindings
        };

    /// <summary>
    /// What the attribute a model of this kind has to carry is called, for the page that says a type
    /// without one has never been written.
    /// </summary>
    public static string Attribute(this StreamedModelKind kind) =>
        kind switch
        {
            StreamedModelKind.Projection => "[ProjectionType]",
            _ => "[AggregateType]"
        };
}
