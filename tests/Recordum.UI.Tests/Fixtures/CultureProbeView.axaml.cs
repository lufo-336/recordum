using System.ComponentModel;
using Avalonia.Controls;

namespace Recordum.UI.Tests.Fixtures;

public sealed class CultureProbeModel : INotifyPropertyChanged
{
    private decimal _amount = 7m;
    public decimal Amount { get => _amount; set { _amount = value; PropertyChanged?.Invoke(this, new(nameof(Amount))); } }
    public DateTime Day { get; } = new(2026, 8, 14);
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class CultureProbeView : UserControl
{
    public CultureProbeView() => InitializeComponent();
}
