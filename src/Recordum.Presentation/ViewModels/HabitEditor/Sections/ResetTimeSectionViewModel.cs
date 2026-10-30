#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI strings
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

public sealed partial class ResetTimeSectionViewModel(IHabitNodeService nodes, IResetTimeResolver? resolver = null) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_ResetTime;

    private readonly IHabitNodeService _nodes = nodes;
    private readonly IResetTimeResolver? _resolver = resolver;
    private string? _inheritedText;

    /// <summary>"5:00" se l'orario e' proprio; altrimenti quello che l'abitudine eredita (gruppo o app).</summary>
    public override string Summary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ResetTimeText))
                return TimeOnly.TryParseExact(ResetTimeText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var own)
                    ? Hm(own) : ResetTimeText;
            return _inheritedText is null
                ? Strings.Editor_Summary_InheritedBare
                : string.Format(CultureInfo.CurrentCulture, Strings.Editor_Summary_Inherited, _inheritedText);
        }
    }

    private static string Hm(TimeOnly t) => t.ToString("H:mm", CultureInfo.InvariantCulture);

    private async Task RefreshInheritedAsync()
    {
        _inheritedText = null;
        if (_resolver is not null && NodeId != Guid.Empty)
        {
            // Un riassunto non deve far fallire il caricamento dell'editor: se il resolver cade, resta "Ereditato".
            try { _inheritedText = Hm(await _resolver.GetEffectiveResetTimeAsync(NodeId).ConfigureAwait(true)); }
            catch (Exception) { _inheritedText = null; }
        }
        NotifySummary();
    }
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private string _resetTimeText = string.Empty;

    public async Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            ResetTimeText = node.ResetTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        }
        finally { _loading = false; }
        await RefreshInheritedAsync().ConfigureAwait(true);
    }

    partial void OnResetTimeTextChanged(string value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = ApplyAsync(async () =>
            {
                await _nodes.UpdateAsync(NodeId, n => n.ResetTime = null).ConfigureAwait(true);
                await RefreshInheritedAsync().ConfigureAwait(true);
            });
            return;
        }
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            ErrorMessage = "Reset time must be HH:mm (24h)";
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _nodes.UpdateAsync(NodeId, n => n.ResetTime = parsed).AsTask());
    }
}
