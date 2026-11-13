using Avalonia.Controls;
using Avalonia.Interactivity;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen.Head;

public partial class AbstinenceCommandView : UserControl
{
    private AbstinenceQuickAddViewModel? _subscribed;

    public AbstinenceCommandView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribed is not null) _subscribed.LapsePromptRequested -= OnLapsePromptRequested;
        _subscribed = DataContext as AbstinenceQuickAddViewModel;
        if (_subscribed is not null) _subscribed.LapsePromptRequested += OnLapsePromptRequested;
        ShowPendingLapsePrompt();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ShowPendingLapsePrompt();
    }

    private void OnLapsePromptRequested(object? sender, EventArgs e) => ShowPendingLapsePrompt();

    // La domanda può arrivare prima della vista (schermata appena aperta dalla Home): aspetta nel view model e si apre
    // quando il pulsante è a schermo.
    private void ShowPendingLapsePrompt()
    {
        if (!IsLoaded || _subscribed is null || !_subscribed.TakeLapsePrompt()) return;
        LapseButton.Flyout?.ShowAt(LapseButton);
    }

    // Il contenuto di un Flyout non eredita il DataContext in modo affidabile: il giorno scelto si scrive qui.
    private void OnLapseFlyoutOpening(object? sender, EventArgs e)
    {
        if (sender is not Flyout { Content: Panel panel }) return;
        var note = panel.Children.OfType<TextBlock>().FirstOrDefault(t => t.Tag as string == "LapseOn");
        if (note is null) return;
        var text = (DataContext as AbstinenceQuickAddViewModel)?.LapseOnDisplay ?? string.Empty;
        note.Text = text;
        note.IsVisible = text.Length > 0;
    }

    private async void OnConfirmLapse(object? sender, RoutedEventArgs e)
    {
        try
        {
            LapseButton.Flyout?.Hide();
            if (DataContext is AbstinenceQuickAddViewModel vm) await vm.LapseCommand.ExecuteAsync(null);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OnConfirmLapse failed: {ex}"); }
    }
}
