namespace Recordum.Core.Errors;

public sealed class SegmentDefinitionNotFoundException(Guid id)
    : RecordumDomainException($"SegmentDefinition {id} not found.")
{
    public Guid SegmentId { get; } = id;
}
