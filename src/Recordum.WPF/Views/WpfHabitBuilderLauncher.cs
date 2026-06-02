using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.ViewModels.HabitBuilder;
using Recordum.WPF.Views.HabitBuilder;

namespace Recordum.WPF.Views;

/// <summary>
/// Hosts <see cref="HabitBuilderView"/> in a modal window. Closes with success when the
/// builder's Launch command sets <see cref="HabitBuilderViewModel.CreatedHabitNodeId"/>;
/// the view's Cancel button closes with no result.
/// </summary>
public sealed class WpfHabitBuilderLauncher(IServiceProvider services) : IHabitBuilderLauncher
{
    public Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var vm = services.GetRequiredService<HabitBuilderViewModel>();
        vm.SetParent(parentId);

        var view = new HabitBuilderView { DataContext = vm };
        var window = new Window
        {
            Title = Recordum.WPF.Resources.Strings.HabitBuilder_Title,
            Width = 720,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
            Content = view,
        };

        void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HabitBuilderViewModel.CreatedHabitNodeId)) return;
            if (vm.CreatedHabitNodeId is null) return;
            window.DialogResult = true;
            window.Close();
        }

        vm.PropertyChanged += OnVmPropertyChanged;
        view.CancelButton.Click += (_, _) => window.Close();
        window.Closed += (_, _) => vm.PropertyChanged -= OnVmPropertyChanged;

        var ok = window.ShowDialog() == true;
        return Task.FromResult(ok ? vm.CreatedHabitNodeId : null);
    }
}
