using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Scales;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Scales;

[Collection(ProcessGlobalCultureCollection.Name)]
public class ScaleEditorViewModelTests
{
    private static ScaleTemplate Mood(bool official = false, int min = 1, int max = 5, params string[] names) => new()
    {
        Id = Guid.NewGuid(), Code = "mood-5", Name = "Umore", MinValue = min, MaxValue = max, IsOfficial = official,
        Anchors = (names.Length > 0 ? names : ["Malissimo", "Male", "Così così", "Bene", "Benissimo"])
            .Select((n, i) => new ScaleAnchor { Id = Guid.NewGuid(), Value = min + i, Label = n }).ToList(),
    };

    private static T InIt<T>(Func<T> act)
    {
        var (c, u) = (CultureInfo.CurrentCulture, Strings.Culture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
            return act();
        }
        finally { CultureInfo.CurrentCulture = c; Strings.Culture = u; }
    }

    [Fact]
    public void Loading_the_mood_scale_gives_five_rows_and_a_derived_tag()
    {
        var sut = new ScaleEditorViewModel();
        sut.Load(Mood(), null);

        sut.Rows.Should().HaveCount(5);
        sut.Rows[2].TagPreview.Should().Be("#così-così");
        sut.Name.Should().Be("Umore");
        sut.CanConfirm.Should().BeTrue();
        sut.Preview.Select(p => p.Value).Should().Equal(1m, 2m, 3m, 4m, 5m);
    }

    [Fact]
    public void Name_every_value_is_enabled_only_up_to_twelve_values()
    {
        var sut = new ScaleEditorViewModel { MinValue = 1, MaxValue = 100 };
        sut.FillEveryValueCommand.CanExecute(null).Should().BeFalse();

        sut.MaxValue = 12;
        sut.FillEveryValueCommand.CanExecute(null).Should().BeTrue();
        sut.FillEveryValueCommand.Execute(null);

        sut.Rows.Should().HaveCount(12);
        sut.Rows.Select(r => r.Value).Should().Equal(Enumerable.Range(1, 12));
    }

    [Fact]
    public void Name_every_value_keeps_the_rows_already_there()
    {
        var sut = new ScaleEditorViewModel { Name = "x", MinValue = 1, MaxValue = 3 };
        sut.AddRowCommand.Execute(null);
        sut.Rows[0].Label = "uno";
        sut.FillEveryValueCommand.Execute(null);

        sut.Rows.Select(r => (r.Value, r.Label)).Should().Equal((1, "uno"), (2, ""), (3, ""));
    }

    [Fact]
    public void Add_row_takes_the_first_free_integer_from_the_minimum()
    {
        var sut = new ScaleEditorViewModel { MinValue = -2, MaxValue = 10 };
        sut.AddRowCommand.Execute(null);
        sut.AddRowCommand.Execute(null);
        sut.Rows.Select(r => r.Value).Should().Equal(-2, -1);
        sut.Rows[0].Value = 7;
        sut.AddRowCommand.Execute(null);

        sut.Rows.Select(r => r.Value).Should().Equal(7, -1, -2);
    }

    [Fact]
    public void Add_row_stops_when_every_value_in_range_has_one()
    {
        var sut = new ScaleEditorViewModel { MinValue = 1, MaxValue = 2 };
        for (var i = 0; i < 4; i++) sut.AddRowCommand.Execute(null);

        sut.Rows.Select(r => r.Value).Should().Equal(1, 2);
    }

    [Fact]
    public void Narrowing_below_a_recorded_value_blocks_with_the_recorded_number()
    {
        InIt(() =>
        {
            var sut = new ScaleEditorViewModel();
            sut.Load(Mood(min: 1, max: 100, names: ["a", "b"]), new ScaleUsage(1, 1m, 80m, false));
            sut.MaxValue = 50;

            sut.CanConfirm.Should().BeFalse();
            sut.Errors.Should().Contain("Ci sono già valori fino a 80");
            return 0;
        });
    }

    [Fact]
    public void Each_error_lands_in_the_area_that_causes_it()
    {
        InIt(() =>
        {
            var sut = new ScaleEditorViewModel();
            sut.Load(Mood(min: 1, max: 100, names: ["a", "b"]), new ScaleUsage(1, 1m, 80m, true));
            sut.MaxValue = 50;                       // narrow-max -> range area
            sut.Precision = Recordum.Core.Enums.ScalePrecision.Integer;   // precision (recorded decimals) -> precision area
            sut.Rows[0].Value = sut.Rows[1].Value;   // duplicate -> anchors area
            sut.Name = "";                           // name error -> name area

            sut.RangeErrors.Should().ContainSingle().Which.Should().Contain("80");
            sut.PrecisionErrors.Should().ContainSingle();
            sut.AnchorErrors.Should().ContainSingle().Which.Should().Contain("stesso valore");
            sut.NameErrors.Should().ContainSingle();
            sut.Errors.Should().HaveCount(4, "Errors resta l'elenco completo");
            return 0;
        });
    }

    [Fact]
    public void Two_rows_with_the_same_value_are_an_error()
    {
        InIt(() =>
        {
            var sut = new ScaleEditorViewModel();
            sut.Load(Mood(), null);
            sut.Rows[0].Value = 3;

            sut.CanConfirm.Should().BeFalse();
            sut.Errors.Should().Contain("Due scaglioni hanno lo stesso valore");
            return 0;
        });
    }

    [Fact]
    public void An_unnamed_row_stays_in_the_draft_as_a_tick()
    {
        var sut = new ScaleEditorViewModel { Name = " Sparsa ", MinValue = -5, MaxValue = 5, Precision = ScalePrecision.Decimal };
        sut.AddRowCommand.Execute(null);
        sut.Rows[0].Label = "Giù";
        sut.AddRowCommand.Execute(null);
        sut.Rows[1].Value = 5;

        var d = sut.ToDraft();

        d.Name.Should().Be("Sparsa");
        d.Precision.Should().Be(ScalePrecision.Decimal);
        d.Anchors.Select(a => (a.Value, a.Label)).Should().Equal((-5, "Giù"), (5, null));
    }

    [Fact]
    public void An_official_scale_is_read_only_with_translated_names_and_no_tags()
    {
        InIt(() =>
        {
            var sut = new ScaleEditorViewModel();
            sut.Load(Mood(official: true, names: ["Very low", "Low", "Neutral", "Good", "Very good"]), null);

            sut.IsReadOnly.Should().BeTrue();
            sut.CanConfirm.Should().BeFalse();
            sut.Rows.Select(r => r.Label).Should().Equal("Molto basso", "Basso", "Neutro", "Buono", "Molto buono");
            sut.Rows.Should().OnlyContain(r => r.TagPreview == null);
            return 0;
        });
    }

    [Fact]
    public void Usage_text_appears_only_for_more_than_one_habit()
    {
        InIt(() =>
        {
            var sut = new ScaleEditorViewModel();
            sut.Load(Mood(), new ScaleUsage(1, null, null, false));
            sut.UsageText.Should().BeNull();
            sut.Load(Mood(), new ScaleUsage(2, null, null, false));
            sut.UsageText.Should().Be("Usata da 2 abitudini");
            return 0;
        });
    }

    [Fact]
    public void Editing_raises_Changed_but_loading_does_not()
    {
        var sut = new ScaleEditorViewModel();
        var raised = 0;
        sut.Changed += (_, _) => raised++;
        sut.Load(Mood(), null);
        raised.Should().Be(0);

        sut.Rows[0].Label = "x";
        sut.MaxValue = 6;
        raised.Should().Be(2);
    }
}
