using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Recordum.Presentation.Resources;

namespace Recordum.UI.Views.Startup;

/// <summary>La piccola finestra sì/no dietro <see cref="Services.AvaloniaConfirmationDialog"/>. Codice,
/// non XAML: due bottoni e due TextBlock non giustificano un file .axaml separato, e tenerla qui
/// evita un ennesimo <c>InitializeComponent()</c> per una vista che non cambia mai forma.</summary>
public sealed class ConfirmationWindow : Window
{
    public Button YesButton { get; }
    public Button NoButton { get; }

    /// <param name="okOnly">Un avviso, non una domanda: un solo pulsante "OK" e niente "No".</param>
    public ConfirmationWindow(string title, string message, bool okOnly = false)
    {
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        YesButton = new Button { Content = okOnly ? Strings.Dialog_Ok : Strings.Dialog_Yes, Classes = { "primary" }, MinWidth = 72 };
        NoButton = new Button { Content = Strings.Dialog_No, MinWidth = 72, IsVisible = !okOnly };

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { NoButton, YesButton },
                },
            },
        };
    }
}
