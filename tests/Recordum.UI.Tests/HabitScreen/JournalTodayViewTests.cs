using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen.Today;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

public class JournalTodayViewTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-journal-today");

    [AvaloniaTheory]
    [InlineData(JournalPrimaryGesture.Diary)]
    [InlineData(JournalPrimaryGesture.QuickNote)]
    public void The_template_builds_the_journal_view_and_the_button_follows_the_gesture(JournalPrimaryGesture gesture)
    {
        using var scope = Host.Provider.CreateScope();
        var content = new JournalTodayContent(scope.ServiceProvider.GetRequiredService<IHabitUiPrefsService>()) { Gesture = gesture };
        var view = TodayContentTemplate.Instance.Build(content).Should().BeOfType<JournalTodayView>().Subject;
        view.DataContext = content;
        var w = new Window { Content = view };
        w.Show();
        PrivateDbHost.Pump();

        var button = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PrimaryButton");
        button.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).Should()
            .Equal(gesture == JournalPrimaryGesture.QuickNote ? Strings.Today_Journal_QuickNote : Strings.Today_Journal_WriteEntry);

        var requested = new List<TodayContentAction>();
        content.ActionRequested += (_, a) => requested.Add(a);
        button.Command!.Execute(null);
        requested.Should().Equal(gesture == JournalPrimaryGesture.QuickNote ? TodayContentAction.OpenQuickNote : TodayContentAction.OpenDiario);
        w.Close();
    }
}
