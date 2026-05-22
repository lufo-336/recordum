using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public abstract partial class QuickAddViewModelBase : ObservableObject
{
    protected ICompletionService Completions { get; }
    protected IClock Clock { get; }

    [ObservableProperty] private HabitNode? _node;
    [ObservableProperty] private DateTime _completionDate;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isSaving;

    public event EventHandler<Guid>? CompletionSaved;

    protected QuickAddViewModelBase(ICompletionService completions, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(clock);
        Completions = completions;
        Clock = clock;
        CompletionDate = clock.UtcNow.UtcDateTime.Date;
    }

    public abstract Task SaveAsync(CancellationToken ct = default);

    protected void OnSaved(Guid completionId) => CompletionSaved?.Invoke(this, completionId);

    /// <summary>Builds (timestamp, isRetroactive) for the current CompletionDate vs clock.Today.</summary>
    protected (DateTime Timestamp, long Ms, bool IsRetroactive) ResolveCompletionTimestamp()
    {
        var date = CompletionDate.Date;
        var today = Clock.UtcNow.UtcDateTime.Date;
        var ts = date == today
            ? Clock.UtcNow.UtcDateTime
            : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        var ms = new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeMilliseconds();
        return (ts, ms, date != today);
    }
}
