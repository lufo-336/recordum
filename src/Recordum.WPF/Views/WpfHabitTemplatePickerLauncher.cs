using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.ViewModels.HabitEditor;

namespace Recordum.WPF.Views;

public sealed class WpfHabitTemplatePickerLauncher(IServiceProvider services) : IHabitTemplatePickerLauncher
{
    public async Task<Guid?> PickAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var vm = services.GetRequiredService<HabitTemplatePickerViewModel>();
        await vm.LoadAsync(parentId, ct);
        var window = new HabitTemplatePicker(vm) { Owner = Application.Current?.MainWindow };
        return window.ShowDialog() == true ? window.CreatedHabitNodeId : null;
    }
}
