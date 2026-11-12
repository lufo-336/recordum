using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Records;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Le registrazioni del giorno nella Cronologia (F2a task 7). DB SQLCipher PRIVATO, orologio finto,
/// conferma di cancellazione finta che risponde si' o no.</summary>
public class RecordsViewTests
{
    private static readonly Confirm Answer = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-records",
        s => s.AddSingleton<IConfirmationDialog>(Answer));
    private static readonly DateTimeOffset Noon = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class Confirm : IConfirmationDialog
    {
        public bool Yes { get; set; }
        public int Asked { get; set; }
        public Task<bool> ConfirmAsync(string title, string message) { Asked++; return Task.FromResult(Yes); }
    }

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id);

    private static async Task<Opened> OpenAsync(HabitType type, Func<IServiceScope, Guid, Task>? seed = null)
    {
        Host.Clock.Now = Noon;
        Answer.Yes = false; Answer.Asked = 0;
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"RC-{Guid.NewGuid():N}", Color = "#E11D48",
        });
        if (seed is not null) await seed(scope, id);
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 800, Height = 900, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static Task<Guid> RecordAtAsync(IServiceScope scope, Guid id, DateTimeOffset at) =>
        scope.ServiceProvider.GetRequiredService<ICompletionService>().RecordAsync(new Completion
        {
            HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
        }).AsTask();

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static Button Day(Visual root, DateOnly d) =>
        root.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("day") && ((Recordum.Presentation.ViewModels.HabitScreen.History.HistoryDayCell)b.DataContext!).Date == d);

    private static Border RecordsHost(Opened o) => o.View.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "RecordsHost");

    private static List<string> Lines(Opened o) =>
        [.. RecordsHost(o).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    private static List<Button> Buttons(Opened o) => [.. RecordsHost(o).GetVisualDescendants().OfType<Button>()];

    private static async Task SeedTwoDays(IServiceScope s, Guid id)
    {
        await RecordAtAsync(s, id, Noon);                       // 15 marzo 12:00
        await RecordAtAsync(s, id, Noon.AddDays(-1));           // 14 marzo 12:00
    }

    [AvaloniaFact]
    public async Task The_rows_are_those_of_the_selected_day_and_change_with_it()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            // La riga dice il giorno (nascosto nel giorno scelto, ma c'è) e l'ora: "dom 15 mar" · "12:00".
            static string D(int day) => new DateOnly(2026, 3, day).ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture);
            await UntilAsync(() => Lines(o).Contains(D(15)));
            Lines(o).Should().NotContain(D(14));

            Click(o.Window, Day(o.View, new DateOnly(2026, 3, 14)));
            await UntilAsync(() => Lines(o).Contains(D(14)));
            Lines(o).Should().NotContain(D(15));
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("en-US", "[photo]")]
    [InlineData("it-IT", "[foto]")]
    public async Task A_diary_row_shows_its_tag_chips_and_the_photo_label(string culture, string label)
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo(culture);
        var o = await OpenAsync(HabitType.Journal, async (scope, id) =>
        {
            var draft = new DiaryEntryDraft($"corsa #fiato\n![mare.jpg](photo:{Guid.NewGuid():N})", ["sera"], []);
            await scope.ServiceProvider.GetRequiredService<IDiaryService>()
                .SaveEntryAsync(id, new DateOnly(2026, 3, 15), draft);
        });
        try
        {
            o.Vm.IsCronologiaOpen = true;   // un diario apre il suo editor, non la Cronologia
            Pump();
            await UntilAsync(() => RecordsHost(o).GetVisualDescendants().OfType<Border>().Any(b => b.Classes.Contains("tag-chip")));
            var chips = RecordsHost(o).GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("tag-chip"))
                .OrderBy(b => ((TextBlock)b.Child!).Text).ToList();
            chips.Select(b => ((TextBlock)b.Child!).Text).Should().Equal("fiato", "sera");
            chips[0].Classes.Should().Contain("inline").And.NotContain("property");
            chips[1].Classes.Should().Contain("property").And.NotContain("inline");
            // La voce è resa (UX-10): il testo sta nell'editor in sola lettura, con l'etichetta della foto al posto del link.
            var entry = RecordsHost(o).GetVisualDescendants().OfType<Recordum.UI.Controls.DiaryEditor>().Single();
            entry.IsReadOnly.Should().BeTrue();
            entry.Markdown.Should().Contain(label).And.NotContain("photo:");
        }
        finally { o.Window.Close(); Recordum.Presentation.Resources.Strings.Culture = previous; }
    }

    [AvaloniaFact]
    public async Task Rows_have_only_a_delete_button_no_edit()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            await UntilAsync(() => Buttons(o).Count == 1);
            var b = Buttons(o).Single();
            b.Name.Should().Be("DeleteButton");
            Avalonia.Automation.AutomationProperties.GetName(b).Should().NotBeNullOrEmpty();
            ToolTip.GetTip(b).Should().NotBeNull();
            Lines(o).Should().NotContain("✎");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Cancelling_the_confirmation_keeps_the_record()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            await UntilAsync(() => Buttons(o).Count == 1);
            Answer.Yes = false;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Answer.Asked == 1);
            Pump();
            Buttons(o).Should().HaveCount(1);
            var cs = o.Scope.ServiceProvider.GetRequiredService<ICompletionService>();
            (await cs.ListByHabitAsync(o.Id)).Should().HaveCount(2);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Confirming_deletes_it()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            await UntilAsync(() => Buttons(o).Count == 1);
            o.View.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "RecordsEmpty").IsVisible.Should().BeFalse();
            Answer.Yes = true;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Buttons(o).Count == 0);
            var cs = o.Scope.ServiceProvider.GetRequiredService<ICompletionService>();
            (await cs.ListByHabitAsync(o.Id)).Should().HaveCount(1, "resta solo quella di ieri");
            var empty = o.View.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "RecordsEmpty");
            empty.IsVisible.Should().BeTrue("il giorno non ha piu' righe: compare lo stato vuoto");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task An_abstinence_habit_lists_its_periods_and_deletes_behind_the_confirmation()
    {
        var o = await OpenAsync(HabitType.Resistance, async (s, id) =>
            await s.ServiceProvider.GetRequiredService<IAbstinenceService>().StartAsync(id));
        try
        {
            await UntilAsync(() => RecordsHost(o).Child is AbstinencePeriodListView && Buttons(o).Count == 1);
            Answer.Yes = false;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Answer.Asked == 1);
            Buttons(o).Should().HaveCount(1, "un annullo lascia la riga");

            Answer.Yes = true;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Buttons(o).Count == 0);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_closed_period_is_not_drawn_as_an_error()
    {
        // B5: una ricaduta non e' un errore — barra del colore del bordo, testo attenuato, data "15 mar".
        using var _ = TestCulture.Pin();
        var o = await OpenAsync(HabitType.Resistance, async (s, id) =>
        {
            var ab = s.ServiceProvider.GetRequiredService<IAbstinenceService>();
            Host.Clock.Now = Noon.AddDays(-3);
            await ab.StartAsync(id);
            Host.Clock.Now = Noon;
            await ab.LapseAsync(id);   // oggi: il periodo chiuso tocca il giorno scelto
        });
        try
        {
            await UntilAsync(() => RecordsHost(o).Child is AbstinencePeriodListView && Buttons(o).Count == 1);
            Avalonia.Media.Color Res(string key)
            {
                o.Window.TryFindResource(key, o.Window.ActualThemeVariant, out var b).Should().BeTrue();
                return ((Avalonia.Media.ISolidColorBrush)b!).Color;
            }

            var bar = RecordsHost(o).GetVisualDescendants().OfType<Border>()
                .Single(b => b.CornerRadius == new CornerRadius(2) && b.IsEffectivelyVisible);
            ((Avalonia.Media.ISolidColorBrush)bar.Background!).Color.Should().Be(Res("Brush.Border")).And.NotBe(Res("Brush.Danger"));

            var relapsed = RecordsHost(o).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Ricaduta il 15 mar");
            ((Avalonia.Media.ISolidColorBrush)relapsed.Foreground!).Color.Should().Be(Res("Brush.TextMuted")).And.NotBe(Res("Brush.Danger"));
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public void The_template_builds_the_list_of_its_type_only()
    {
        var t = RecordsTemplate.Instance;
        t.Match(new object()).Should().BeFalse();
        t.Build(null).Should().BeNull();
        t.Build(new object()).Should().BeNull();
    }
}
