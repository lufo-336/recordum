using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.UI.Views;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>WpfHabitTemplatePickerLauncher</c>: il selettore in una finestra modale con le decorazioni
/// di sistema. Se chiede il creatore (vuoto con "Da zero", riempito con "Personalizza…"), lo apre col genitore del selettore
/// e torna il suo risultato; altrimenti torna l'id creato, o null se la finestra e' stata chiusa senza scegliere.</summary>
public sealed class AvaloniaHabitTemplatePickerLauncher(IServiceProvider services, Func<Window?> ownerResolver)
    : IHabitTemplatePickerLauncher
{
    /// <summary>Per i test: la finestra e' creata al volo e non ha altro modo di essere raggiunta.</summary>
    internal event Action<Window>? WindowCreatedForTests;

    public async Task<Guid?> PickAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        HabitTemplatePickerViewModel vm;
        // Uno scope per apertura (come il builder): IHabitTemplateService e' scoped, e senza scope ogni apertura
        // riuserebbe un DbContext di radice mai smaltito. Lo scope finisce con la finestra, prima del builder.
        await using (var scope = services.CreateAsyncScope())
        {
            vm = scope.ServiceProvider.GetRequiredService<HabitTemplatePickerViewModel>();
            await vm.LoadAsync(parentId, ct).ConfigureAwait(true);
            var window = new HabitTemplatePickerWindow(vm);
            WindowCreatedForTests?.Invoke(window);
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

        if (vm.OpenBuilderRequested)
            return await services.GetRequiredService<IHabitBuilderLauncher>().LaunchAsync(vm.ParentForBuilder, ct);
        // "Personalizza…" (o "Usa" su un modello che il creatore non crea cosi' com'e'): il creatore riempito dal modello.
        if (vm.CustomizeTemplateRequested is { } templateId)
            return await services.GetRequiredService<IHabitBuilderLauncher>().LaunchFromTemplateAsync(templateId, vm.ParentForBuilder, ct);
        return vm.CreatedHabitNodeId;
    }
}
