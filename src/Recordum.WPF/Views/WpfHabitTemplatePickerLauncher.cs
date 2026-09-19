using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.WPF.Views;

public sealed class WpfHabitTemplatePickerLauncher(IServiceProvider services) : IHabitTemplatePickerLauncher
{
    public async Task<Guid?> PickAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var vm = services.GetRequiredService<HabitTemplatePickerViewModel>();
        await vm.LoadAsync(parentId, ct);
        var window = new HabitTemplatePicker(vm) { Owner = Application.Current?.MainWindow };
        var ok = window.ShowDialog() == true;

        if (vm.OpenBuilderRequested)
        {
            var builder = services.GetRequiredService<IHabitBuilderLauncher>();
            return await builder.LaunchAsync(vm.ParentForBuilder, ct);
        }

        return ok ? window.CreatedHabitNodeId : null;
    }
}
