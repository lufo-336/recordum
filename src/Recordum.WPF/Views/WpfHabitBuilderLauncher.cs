using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.WPF.Views.HabitBuilder;

namespace Recordum.WPF.Views;

/// <summary>
/// Hosts <see cref="HabitBuilderView"/> in a modal window. Closes with success when the
/// builder's Launch command sets <see cref="HabitBuilderViewModel.CreatedHabitNodeId"/>;
/// the view's Cancel button closes with no result.
/// </summary>
public sealed class WpfHabitBuilderLauncher(IServiceProvider services) : IHabitBuilderLauncher
{
    public async Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var vm = services.GetRequiredService<HabitBuilderViewModel>();
        vm.SetParent(parentId);
        await vm.InitializeAsync(ct).ConfigureAwait(true); // populate unit/scale pickers before showing

        var view = new HabitBuilderView { DataContext = vm };
        var window = new Window
        {
            Title = Recordum.Presentation.Resources.Strings.HabitBuilder_Title,
            // 720 for the composition column + 360 for the live preview pane (Onda 2.5 T9).
            Width = 1080,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
            Content = view,
        };
        // Built in code, so the shared dark chrome (applied via XAML on the other dialogs) has to be
        // wired here too — otherwise this window keeps the native white title bar (U1 uniformity).
        if (Application.Current?.TryFindResource("Recordum.ChromedWindow") is Style chrome)
            window.Style = chrome;
        window.SetResourceReference(Control.BackgroundProperty, "Brush.Background");
        window.SetResourceReference(Control.FontFamilyProperty, "Font.Default");

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
        return ok ? vm.CreatedHabitNodeId : null;
    }
}
