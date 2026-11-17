using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels.Scales;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen.Today;
using Recordum.UI;
using Xunit;
using static Recordum.UI.Tests.Scales.ScaleFixtures;

namespace Recordum.UI.Tests.Scales;

/// <summary>GIO-23 Task 7: l'editor della scala e il cursore di registrazione a valore vero.</summary>
public class ScaleEditorViewTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-scale-editor");

    private static (ScaleEditor Editor, ScaleEditorViewModel Vm, Window Window) OpenEditor(Core.Models.ScaleTemplate scale, int width = 900, string theme = "light")
    {
        var vm = new ScaleEditorViewModel();
        vm.Load(scale, null);
        var editor = new ScaleEditor { DataContext = vm };
        return (editor, vm, Show(editor, theme, width, 900));
    }

    [AvaloniaFact]
    public void The_mood_editor_shows_five_rows_and_five_names_on_the_bar()
    {
        using var _ = TestCulture.Pin();
        var (editor, _, w) = OpenEditor(Umore());
        try
        {
            AllNamed<TextBox>(editor, "LabelBox").Select(b => b.Text).Should().Equal("Malissimo", "Male", "Così così", "Bene", "Benissimo");
            var bar = Named<ScaleMarksBar>(editor, "PreviewBar");
            VisibleNames(bar).Should().Equal("Malissimo", "Male", "Così così", "Bene", "Benissimo");
            Ticks(bar).Should().HaveCount(5, "su 1-100 solo gli scaglioni hanno una tacca");
            Named<Button>(editor, "FillButton").IsEffectivelyVisible.Should().BeFalse("1-100 ha piu' di 12 valori");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Naming_every_value_on_a_1_to_12_scale_gives_twelve_ticks()
    {
        using var _ = TestCulture.Pin();
        var (editor, vm, w) = OpenEditor(Medica12());
        try
        {
            var bar = Named<ScaleMarksBar>(editor, "PreviewBar");
            Ticks(bar).Should().HaveCount(12, "fino a 12 valori c'e' una tacca per intero, anche senza scaglioni");
            var fill = Named<Button>(editor, "FillButton");
            fill.IsEffectivelyVisible.Should().BeTrue();
            Click(w, fill);
            vm.Rows.Should().HaveCount(12);
            AllNamed<TextBox>(editor, "LabelBox").Should().HaveCount(12);
            Ticks(bar).Should().HaveCount(12);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Editing_the_range_through_the_fields_commits_on_enter_not_per_keystroke()
    {
        using var _ = TestCulture.Pin();
        var (editor, vm, w) = OpenEditor(Umore());
        try
        {
            var changed = 0;
            vm.Changed += (_, _) => changed++;
            var max = Named<TextBox>(editor, "MaxBox");
            max.Focus();
            Pump();
            max.Text = "1";
            max.Text = "12";
            changed.Should().Be(0, "digitare non e' una modifica: in Configura ogni modifica valida e' una scrittura");
            TypeAndEnter(w, max, "120");
            vm.MaxValue.Should().Be(120);
            changed.Should().Be(1);

            var name = Named<TextBox>(editor, "NameBox");
            TypeAndEnter(w, name, "Umore del giorno");
            vm.Name.Should().Be("Umore del giorno");
            changed.Should().Be(2);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Live_update_pushes_every_keystroke_to_the_view_model()
    {
        using var _ = TestCulture.Pin();
        var vm = new ScaleEditorViewModel();
        vm.Load(Umore(), null);
        var editor = new ScaleEditor { DataContext = vm, LiveUpdate = true };
        var w = Show(editor);
        try
        {
            Named<TextBox>(editor, "NameBox").Text = "Nuovo";
            Pump();
            vm.Name.Should().Be("Nuovo");
            Named<TextBox>(editor, "MaxBox").Text = "50";
            Pump();
            vm.MaxValue.Should().Be(50);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void An_official_scale_is_read_only_and_has_no_add_or_remove()
    {
        using var _ = TestCulture.Pin();
        var scale = Umore();
        scale.IsOfficial = true;
        var (editor, _, w) = OpenEditor(scale);
        try
        {
            Named<TextBox>(editor, "NameBox").IsReadOnly.Should().BeTrue();
            AllNamed<TextBox>(editor, "LabelBox").Should().OnlyContain(b => b.IsReadOnly);
            Named<Button>(editor, "AddButton").IsEffectivelyVisible.Should().BeFalse();
            AllNamed<Button>(editor, "RemoveButton").Should().OnlyContain(b => !b.IsEffectivelyVisible);
            Named<RadioButton>(editor, "DecimalPrecision").IsEnabled.Should().BeFalse();
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void The_precision_is_a_two_segment_choice_bound_to_the_view_model()
    {
        using var _ = TestCulture.Pin();
        var (editor, vm, w) = OpenEditor(Umore());
        try
        {
            Named<RadioButton>(editor, "IntegerPrecision").IsChecked.Should().BeTrue();
            Click(w, Named<RadioButton>(editor, "DecimalPrecision"));
            vm.Precision.Should().Be(ScalePrecision.Decimal);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void A_phone_width_wraps_the_tag_under_the_name()
    {
        using var _ = TestCulture.Pin();
        var (editor, _, w) = OpenEditor(Umore(), width: 360);
        try
        {
            editor.Classes.Should().Contain("narrow");
            var label = AllNamed<TextBox>(editor, "LabelBox")[1];
            var tag = AllNamed<TextBlock>(editor, "TagText")[1];
            tag.IsEffectivelyVisible.Should().BeTrue();
            tag.TranslatePoint(new Avalonia.Point(0, 0), w)!.Value.Y.Should().BeGreaterThan(label.TranslatePoint(new Avalonia.Point(0, 0), w)!.Value.Y + label.Bounds.Height - 1,
                "sul telefono il tag sta sotto il nome");
            editor.Bounds.Width.Should().BeLessThanOrEqualTo(360);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task The_state_field_accepts_the_decimal_comma_and_the_reading_follows()
    {
        using var pin = TestCulture.Pin();
        const string typed = "1,37", reading = "1,37, fra Poca e Tanta";
        var (scope, content, w, today) = await OpenStateAsync(Host, Decimale(), "Fatica");
        try
        {
            var box = Named<TextBox>(today, "ValueBox");
            TypeAndEnter(w, box, typed);
            content.SliderValue.Should().BeApproximately(1.37, 1e-9);
            Named<TextBlock>(today, "CurrentLabel").Text.Should().Be(reading);
            box.Text.Should().Be("1,37");
        }
        finally { w.Close(); scope.Dispose(); }
    }

    private static (Views.HabitScreen.QuickAdd.MetricCellView View, Presentation.ViewModels.HabitScreen.QuickAdd.SeriesCellViewModel Cell, Window Window) OpenCell(Core.Models.ScaleTemplate scale)
    {
        var metric = new Core.Models.MetricDefinition { Id = Guid.NewGuid(), Name = "Umore", Type = MetricType.Decimal, ScaleTemplateId = scale.Id };
        var cell = new Presentation.ViewModels.HabitScreen.QuickAdd.SeriesCellViewModel(metric, null, new Presentation.ViewModels.HabitScreen.QuickAdd.ScaleInfo(scale));
        var view = new Views.HabitScreen.QuickAdd.MetricCellView { DataContext = cell };
        return (view, cell, Show(view, width: 360, height: 200));
    }

    [AvaloniaFact]
    public void The_cell_slider_draws_a_tick_per_integer_up_to_twelve_values_else_only_the_marks()
    {
        using var _ = TestCulture.Pin();
        var (view, _, w) = OpenCell(Nrs());
        try
        {
            Ticks(Named<ScaleMarksBar>(view, "ScaleMarks")).Should().HaveCount(11, "0-10 ha undici valori: una tacca per intero");
            Named<ScaleMarksBar>(view, "ScaleMarks").IsEffectivelyVisible.Should().BeTrue();
        }
        finally { w.Close(); }
        var (view2, _, w2) = OpenCell(Umore());
        try { Ticks(Named<ScaleMarksBar>(view2, "ScaleMarks")).Should().HaveCount(5, "1-100: solo gli scaglioni"); }
        finally { w2.Close(); }
    }

    [AvaloniaFact]
    public void The_cell_numeric_field_commits_on_enter_and_moves_the_slider()
    {
        using var _ = TestCulture.Pin();
        var (view, cell, w) = OpenCell(Umore());
        try
        {
            TypeAndEnter(w, Named<TextBox>(view, "ScaleValueBox"), "62");
            cell.SliderValue.Should().Be(62);
            Named<Slider>(view, "ScaleSlider").Value.Should().Be(62);
        }
        finally { w.Close(); }
    }
}
