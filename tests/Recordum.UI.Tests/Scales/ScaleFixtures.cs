using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen.Today;

namespace Recordum.UI.Tests.Scales;

/// <summary>Le scale di prova (Umore 1-100 con cinque scaglioni, Dolore 0-10) e i gesti comuni dei test di GIO-23 Task 7.</summary>
internal static class ScaleFixtures
{
    public static ScaleTemplate Umore(int min = 1, int max = 100, ScalePrecision p = ScalePrecision.Integer) => new()
    {
        Id = Guid.NewGuid(), Code = "umore", Name = "Umore", MinValue = min, MaxValue = max, Precision = p,
        Anchors =
        [
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 1, Label = "Malissimo" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 25, Label = "Male" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 50, Label = "Così così" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 75, Label = "Bene" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 100, Label = "Benissimo" },
        ],
    };

    /// <summary>Una scala a decimali 0-10 con due scaglioni: fra l'uno e l'altro la lettura e' "n, fra X e Y".</summary>
    public static ScaleTemplate Decimale() => new()
    {
        Id = Guid.NewGuid(), Code = "decimale", Name = "Fatica", MinValue = 0, MaxValue = 10, Precision = ScalePrecision.Decimal,
        Anchors =
        [
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 1, Label = "Poca" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 5, Label = "Tanta" },
        ],
    };

    /// <summary>La scala medica 1-12, senza scaglioni.</summary>
    public static ScaleTemplate Medica12() => new()
    {
        Id = Guid.NewGuid(), Code = "medica", Name = "Scala medica", MinValue = 1, MaxValue = 12, Precision = ScalePrecision.Integer, Anchors = [],
    };

    /// <summary>Dolore 0-10: estremi con nome, il resto senza.</summary>
    public static ScaleTemplate Nrs() => new()
    {
        Id = Guid.NewGuid(), Code = "nrs", Name = "Dolore", MinValue = 0, MaxValue = 10, Precision = ScalePrecision.Integer,
        Anchors =
        [
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 0, Label = "Nessun dolore" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 5, Label = "Moderato" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 10, Label = "Il peggiore" },
        ],
    };

    public static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    public static Window Show(Control content, string theme = "light", int width = 900, int height = 700)
    {
        var w = new Window
        {
            Width = width, Height = height, Content = content,
            RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        w.Show();
        Pump();
        return w;
    }

    public static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    public static List<T> AllNamed<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Where(c => c.Name == name).ToList();

    public static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    /// <summary>Digita in un campo e lo conferma con Invio (il fuoco esce, il binding LostFocus scrive).</summary>
    public static void TypeAndEnter(Window w, TextBox box, string text)
    {
        box.Focus();
        Pump();
        box.Text = text;
        w.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        w.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
    }

    /// <summary>Le tacche disegnate da una barra (i suoi rettangoli) e i nomi visibili.</summary>
    public static IReadOnlyList<Avalonia.Controls.Shapes.Rectangle> Ticks(ScaleMarksBar bar) =>
        Named<ItemsControl>(bar, "Ticks").GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().ToList();

    public static IReadOnlyList<string> VisibleNames(ScaleMarksBar bar) =>
        Named<ItemsControl>(bar, "Names").GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0
                // l'opacita' la decide il pannello, sul contenitore del testo
                && t.GetVisualAncestors().OfType<ContentPresenter>().First().Opacity > 0).Select(t => t.Text ?? "").ToList();

    /// <summary>La striscia "Oggi" di un'abitudine di Stato (la sua <see cref="StateTodayView"/>) sul DB privato, con la scala data.
    /// Il valore e' registrato come <paramref name="recorded"/> se dato.</summary>
    public static async Task<(IServiceScope Scope, StateTodayContent Content, Window Window, StateTodayView Today)>
        OpenStateAsync(PrivateDbHost host, ScaleTemplate scale, string name, string theme = "light", int width = 900, decimal? recorded = null)
    {
        host.Clock.Now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
        var scope = host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var scaleId = await sp.GetRequiredService<IScaleTemplateService>().CreateAsync(new ScaleDraft(
            scale.Name, scale.MinValue, scale.MaxValue, scale.Precision,
            scale.Anchors.Select(a => new ScaleAnchorDraft(a.Value, a.Label)).ToList()));
        var node = new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.State, Name = name, Color = "#D4527F" };
        node.Id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(node);
        await sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
        {
            HabitNodeId = node.Id, Name = name.ToLowerInvariant(), Type = MetricType.Decimal, SortOrder = 0, ScaleTemplateId = scaleId,
        });
        var content = new StateTodayContent(sp.GetRequiredService<ICompletionService>(), sp.GetRequiredService<IMetricDefinitionService>(),
            sp.GetRequiredService<IReferenceDataQueryService>(), sp.GetRequiredService<IClock>());
        await content.LoadAsync(node, DayBoundary.Utc);
        if (recorded is { } r) content.SliderValue = (double)r;
        var host2 = new ContentControl { Content = content, ContentTemplate = TodayContentTemplate.Instance, Margin = new Thickness(0) };
        var w = Show(host2, theme, width, 220);
        return (scope, content, w, host2.GetVisualDescendants().OfType<StateTodayView>().Single());
    }
}
