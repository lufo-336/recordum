namespace Recordum.Core.Errors;

public sealed class HabitTemplateNotFoundException(Guid id)
    : RecordumDomainException($"HabitTemplate {id} not found.")
{
    public Guid TemplateId { get; } = id;
}
