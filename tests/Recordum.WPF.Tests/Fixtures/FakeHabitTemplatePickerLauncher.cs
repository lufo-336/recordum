using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeHabitTemplatePickerLauncher : IHabitTemplatePickerLauncher
{
    public Guid? NextResult { get; set; }
    public int PickCallCount { get; private set; }
    public Guid? LastParentId { get; private set; }

    public Task<Guid?> PickAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        PickCallCount++;
        LastParentId = parentId;
        return Task.FromResult(NextResult);
    }
}
