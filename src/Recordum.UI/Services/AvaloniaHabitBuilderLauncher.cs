using System.ComponentModel;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.UI.Views;
using Recordum.UI.Views.HabitBuilder;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>WpfHabitBuilderLauncher</c>: il builder in una finestra modale con le decorazioni di
/// sistema (decisione 1 di F2b: il chrome nostro e' solo della finestra principale). Si chiude con l'id quando
/// "Lancia" lo imposta, con null su "Annulla" o sulla X. Il ViewModel vive in uno scope per apertura, cosi' i
/// servizi con scope (composizione, dati di riferimento) non sopravvivono alla finestra. Il resolver
/// dell'owner e' iniettato come in <see cref="AvaloniaConfirmationDialog"/>.</summary>
public sealed class AvaloniaHabitBuilderLauncher(IServiceProvider services, Func<Window?> ownerResolver) : IHabitBuilderLauncher
{
    /// <summary>Test-friendly: la finestra e' creata al volo e non ha altro modo di essere raggiunta.</summary>
    internal event Action<Window>? WindowCreatedForTests;

    public async Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<HabitBuilderViewModel>();
        vm.SetParent(parentId);
        await vm.InitializeAsync(ct).ConfigureAwait(true);   // i selettori di unita' e scala prima di aprire

        var view = new HabitBuilderView { DataContext = vm };
        var window = new RecordumWindow
        {
            Title = Strings.HabitBuilder_Title,
            Width = 1080,     // 720 per la composizione + 360 per l'anteprima viva
            Height = 680,
            MinWidth = 720,
            MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = view,
        };

        void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HabitBuilderViewModel.CreatedHabitNodeId) && vm.CreatedHabitNodeId is not null)
                window.Close();
        }

        vm.PropertyChanged += OnVmPropertyChanged;
        view.CancelButton.Click += (_, _) => window.Close();
        WindowCreatedForTests?.Invoke(window);
        try
        {
            if (ownerResolver() is { } owner)
                await window.ShowDialog(owner);
            else
            {
                var closed = new TaskCompletionSource();
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                await closed.Task;
            }
        }
        finally { vm.PropertyChanged -= OnVmPropertyChanged; }

        return vm.CreatedHabitNodeId;
    }
}
