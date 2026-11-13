using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.ViewModels.Home;

namespace Recordum.Presentation.Services;

/// <summary>La versione delle righe della Home scelta su questo dispositivo (AppSetting <c>home.rows</c>, senza seed: si
/// scrive al primo cambio). <paramref name="fallback"/> è la predefinita della shell: vale prima di
/// <see cref="InitializeAsync"/> e quando il valore salvato manca o non è una delle tre.</summary>
public sealed class HomeRowsPreference(IAppSettingService settings, HomeRows fallback)
{
    public const string Key = "home.rows";

    private readonly HomeRows _fallback = fallback;

    public HomeRows Current { get; private set; } = fallback;

    public event EventHandler? Changed;

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        // Enum.TryParse accetta anche "7": un numero fuori dai tre nomi vale come un valore sconosciuto.
        var saved = await settings.GetEnumAsync<HomeRows>(Key, ct);
        Current = saved is { } value && Enum.IsDefined(value) ? value : _fallback;
    }

    public async ValueTask SetAsync(HomeRows value, CancellationToken ct = default)
    {
        if (Current == value) return;
        await settings.SetEnumAsync(Key, value, ct);
        Current = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
