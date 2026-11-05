using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitBuilder;

/// <summary>Onda 3a Task 5: cio' che la vista del creatore legge dal ViewModel — perche' "Crea abitudine" e' spento, cosa
/// dice ogni tessera (e perche' e' spenta), la riga del tipo derivato.</summary>
public class BuilderViewHelpersTests
{
    private static HabitBuilderViewModel NewSut()
    {
        var settings = new FakeAppSettingService();
        return new(new FakeComposedHabitService(), new FakeHabitTemplateService(), new FakeReferenceData(), new FakeClock(),
            new RecentColorStore(settings), new FavoriteColorStore(settings), new FakeScreenColorPicker());
    }

    private static IDisposable Culture(string name)
    {
        var saved = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);
        return new Restore(() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = saved);
    }

    private sealed class Restore(Action undo) : IDisposable { public void Dispose() => undo(); }

    // ---- LaunchBlockedReason -----------------------------------------------------------------------------

    [Fact]
    public void A_launchable_composition_has_no_reason()
    {
        var sut = NewSut();
        sut.Name = "Corsa";
        sut.CanLaunch.Should().BeTrue();
        sut.LaunchBlockedReason.Should().BeNull();
    }

    [Fact]
    public void The_reason_names_the_first_failing_rule_in_order()
    {
        using var _ = Culture("en-US");
        var sut = NewSut();
        sut.LaunchBlockedReason.Should().Be(Strings.HabitBuilder_Blocked_Name);

        sut.Name = "Allenamento";
        sut.AddFieldCommand.Execute(FieldNature.Scale);
        sut.Fields[0].Name = "Fatica";
        sut.LaunchBlockedReason.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_Scale, "Fatica"));
        sut.Fields[0].ScaleTemplateId = Guid.NewGuid();

        sut.AddFieldCommand.Execute(FieldNature.Time);
        sut.LaunchBlockedReason.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_TimeUnit, sut.Fields[1].Name));
        sut.Fields[1].UnitOfMeasureId = Guid.NewGuid();
        sut.LaunchBlockedReason.Should().BeNull();

        sut.GoalBrick.HasGoal = true;
        sut.LaunchBlockedReason.Should().Be(Strings.HabitBuilder_Blocked_GoalTarget);
        sut.GoalBrick.Target = 3m;
        sut.GoalBrick.RollingWindowDays = 0;
        sut.LaunchBlockedReason.Should().Be(RollingReason());
        sut.GoalBrick.RollingWindowDays = 7;

        sut.Schedule.StreakToleranceMinutes = 1441;
        sut.LaunchBlockedReason.Should().Be(Strings.HabitBuilder_Blocked_Tolerance);
        sut.Schedule.StreakToleranceMinutes = 0;
        sut.LaunchBlockedReason.Should().Be(Strings.HabitBuilder_Blocked_Tolerance);
        sut.Schedule.StreakToleranceMinutes = 1440;

        sut.SessionBrick.HasSession = true;
        sut.SessionBrick.RestTimerSeconds = 0;
        sut.LaunchBlockedReason.Should().Be(Strings.HabitBuilder_Blocked_Rest);
        sut.SessionBrick.RestTimerSeconds = 60;

        sut.Schedule.ScheduleType = ScheduleType.SpecificDays;
        sut.LaunchBlockedReason.Should().Be(Strings.HabitBuilder_Blocked_Schedule);
        sut.Schedule.Days[0].IsSelected = true;
        sut.LaunchBlockedReason.Should().BeNull();
        sut.CanLaunch.Should().BeTrue();
    }

    private static string RollingReason() => string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_RollingDays,
        string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Goal_RollingRow, "…"));

    [Fact]
    public void A_number_box_with_rejected_text_blocks_creation_and_says_which()
    {
        using var _ = Culture("it-IT");
        var sut = NewSut();
        sut.Name = "Corsa";
        sut.SetInputRejected(BuilderNumberInput.Tolerance, true);
        sut.CanLaunch.Should().BeFalse("la casella e' rossa: il valore non puo' sparire in silenzio");
        sut.LaunchCommand.CanExecute(null).Should().BeFalse();
        sut.LaunchBlockedReason.Should().Be("Tolleranza (minuti): non è un numero intero.");

        sut.SetInputRejected(BuilderNumberInput.Tolerance, false);
        sut.CanLaunch.Should().BeTrue();
        sut.LaunchBlockedReason.Should().BeNull();
    }

    [Fact]
    public void A_rejected_box_that_is_not_part_of_the_composition_does_not_block()
    {
        var sut = NewSut();
        sut.Name = "Corsa";
        sut.SetInputRejected(BuilderNumberInput.Rest, true);         // nessuna sessione: la casella del riposo non c'e'
        sut.SetInputRejected(BuilderNumberInput.RollingDays, true);  // nessun obiettivo: neanche la finestra
        sut.CanLaunch.Should().BeTrue();

        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.SessionBrick.HasSession = true;
        sut.CanLaunch.Should().BeFalse("con la sessione accesa il riposo rifiutato torna a contare");
    }

    [Fact]
    public async Task Launch_refuses_a_rejected_box_even_when_called_directly()
    {
        var composed = new FakeComposedHabitService();
        var settings = new FakeAppSettingService();
        var sut = new HabitBuilderViewModel(composed, new FakeHabitTemplateService(), new FakeReferenceData(), new FakeClock(),
            new RecentColorStore(settings), new FavoriteColorStore(settings), new FakeScreenColorPicker()) { Name = "Corsa" };
        sut.SetInputRejected(BuilderNumberInput.Tolerance, true);
        await sut.LaunchCommand.ExecuteAsync(null);
        sut.CreatedHabitNodeId.Should().BeNull();
    }

    [Theory]
    [InlineData("en-US", "Something is still missing: check the name, the fields, the schedule and the goal.",
        "With no field it is an action to tick off; add what you want to measure each time.", "It's a break", "Count over the last {0} days")]
    [InlineData("it-IT", "Manca ancora qualcosa: controlla il nome, i campi, il programma e l'obiettivo.",
        "Senza campi è un'azione da spuntare; aggiungi quello che vuoi misurare ogni volta.", "È una pausa", "Conta sugli ultimi {0} giorni")]
    public void The_fix_round_words(string culture, string hint, string tiles, string isBreak, string row)
    {
        using var _ = Culture(culture);
        Strings.HabitBuilder_LaunchHint.Should().Be(hint);
        Strings.HabitBuilder_Tiles_Hint.Should().Be(tiles);
        Strings.HabitBuilder_Segment_IsBreak.Should().Be(isBreak);
        Strings.HabitBuilder_Goal_RollingRow.Should().Be(row);
        Strings.HabitBuilder_Goal_RollingRow_Help.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_field_without_a_name_is_called_by_its_kind()
    {
        using var _ = Culture("en-US");
        var sut = NewSut();
        sut.Name = "Umore";
        sut.AddFieldCommand.Execute(FieldNature.Scale);
        sut.Fields[0].Name = "  ";
        sut.LaunchBlockedReason.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_Scale,
            Strings.HabitBuilder_DefaultField_Scale));
    }

    [Fact]
    public void A_rule_without_its_own_line_still_says_something()
    {
        // Il creatore non offre il periodo fisso; se arriva comunque, il pulsante e' spento e la riga non tace.
        var sut = NewSut();
        sut.Name = "Acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.GoalBrick.HasGoal = true;
        sut.GoalBrick.Target = 2m;
        sut.GoalBrick.PeriodType = GoalPeriodType.Fixed;
        sut.CanLaunch.Should().BeFalse();
        sut.LaunchBlockedReason.Should().Be(Strings.HabitBuilder_LaunchHint);
    }

    [Fact]
    public void The_reason_is_announced_when_it_changes()
    {
        var sut = NewSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        sut.Name = "Corsa";
        raised.Should().Contain(nameof(HabitBuilderViewModel.LaunchBlockedReason));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("it-IT")]
    public void Every_reason_is_translated(string culture)
    {
        using var _ = Culture(culture);
        string[] reasons =
        [
            Strings.HabitBuilder_Blocked_Name, Strings.HabitBuilder_Blocked_Scale, Strings.HabitBuilder_Blocked_TimeUnit,
            Strings.HabitBuilder_Blocked_GoalTarget, Strings.HabitBuilder_Blocked_RollingDays, Strings.HabitBuilder_Blocked_Tolerance,
            Strings.HabitBuilder_Blocked_Rest, Strings.HabitBuilder_Blocked_Schedule, Strings.HabitBuilder_Blocked_NotANumber,
        ];
        reasons.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r)).And.OnlyHaveUniqueItems();
        if (culture == "it-IT") Strings.HabitBuilder_Blocked_Name.Should().Be("Dai un nome all'abitudine.");
        else Strings.HabitBuilder_Blocked_Name.Should().Be("Give the habit a name.");
    }

    // ---- FieldTileHint ------------------------------------------------------------------------------------

    private static readonly FieldNature[] Natures = [FieldNature.Quantity, FieldNature.Time, FieldNature.Scale, FieldNature.Text];

    private static string UsualHint(FieldNature n) => n switch
    {
        FieldNature.Quantity => Strings.HabitBuilder_Tile_Quantity_Hint,
        FieldNature.Time => Strings.HabitBuilder_Tile_Time_Hint,
        FieldNature.Scale => Strings.HabitBuilder_Tile_Scale_Hint,
        _ => Strings.HabitBuilder_Tile_Text_Hint,
    };

    [Fact]
    public void An_open_tile_says_what_it_is_for()
    {
        using var _ = Culture("en-US");
        var sut = NewSut();
        foreach (var n in Natures) sut.FieldTileHint(n).Should().Be(UsualHint(n));
        Strings.HabitBuilder_Tile_Quantity_Hint.Should().Be("A number with a unit: litres, kilometres, pages");
    }

    [Fact]
    public void With_the_tap_counter_every_tile_says_the_tap_is_the_record()
    {
        using var _ = Culture("it-IT");
        var sut = NewSut();
        sut.CountsOccurrences = true;
        foreach (var n in Natures)
        {
            sut.CanAddField(n).Should().BeFalse();
            sut.FieldTileHint(n).Should().Be("Non con il contatore a tocco: il tocco è già la registrazione");
        }
    }

    /// <summary>Ogni composizione raggiungibile (fino a tre campi, contatore, astinenza): dove la grammatica vieta una tessera
    /// la riga ne dice il perche' (mai quella d'uso), dove la permette dice a cosa serve.</summary>
    [Fact]
    public void A_closed_tile_always_says_why_and_an_open_one_never_does()
    {
        var reasons = new HashSet<string>();
        foreach (var fields in Compositions())
            foreach (var abstinence in new[] { false, true })
            {
                var sut = NewSut();
                sut.Abstinence = abstinence;
                foreach (var f in fields) sut.AddFieldCommand.Execute(f);
                if (sut.Fields.Count != fields.Length) continue;   // composizione che la grammatica non costruisce
                foreach (var n in Natures)
                {
                    var hint = sut.FieldTileHint(n);
                    if (sut.CanAddField(n)) hint.Should().Be(UsualHint(n));
                    else
                    {
                        hint.Should().NotBe(UsualHint(n)).And.NotBeNullOrWhiteSpace();
                        reasons.Add(hint);
                    }
                }
            }
        reasons.Should().HaveCountGreaterThanOrEqualTo(4, "una riga per regola: testo gia' presente, astinenza, misure accanto al testo, durata doppia nel diario");
    }

    private static IEnumerable<FieldNature[]> Compositions()
    {
        yield return [];
        foreach (var a in Natures)
        {
            yield return [a];
            foreach (var b in Natures)
            {
                yield return [a, b];
                foreach (var c in Natures) yield return [a, b, c];
            }
        }
    }

    [Fact]
    public void The_tiles_follow_the_composition()
    {
        var sut = NewSut();
        sut.TextTile.IsEnabled.Should().BeTrue();
        sut.TextTile.Hint.Should().Be(Strings.HabitBuilder_Tile_Text_Hint);
        sut.AddFieldCommand.Execute(FieldNature.Text);
        sut.TextTile.IsEnabled.Should().BeFalse();
        sut.TextTile.Hint.Should().Be(sut.FieldTileHint(FieldNature.Text));
        sut.QuantityTile.IsEnabled.Should().BeFalse();
        sut.TimeTile.IsEnabled.Should().BeTrue();
        sut.ScaleTile.IsEnabled.Should().BeFalse();
    }

    // ---- DerivedTypeLine ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("en-US", "Behaves like: counter · changes by itself with what you record")]
    [InlineData("it-IT", "Si comporta come contatore · cambia da solo con quello che registri")]
    public void The_type_line_names_the_derived_type_in_the_home_word(string culture, string expected)
    {
        using var _ = Culture(culture);
        var sut = NewSut();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.DerivedTypeWord.Should().Be(Strings.Home_HabitType_Counter);
        sut.DerivedTypeLine.Should().Be(expected);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("it-IT")]
    public void The_type_line_marks_the_word_for_bold_in_both_languages(string culture)
    {
        using var _ = Culture(culture);
        Strings.HabitBuilder_DerivedType_Line.Should().Contain("**{0}**");
    }

    [Fact]
    public void The_type_line_follows_the_composition()
    {
        var sut = NewSut();
        sut.DerivedTypeWord.Should().Be(Strings.Home_HabitType_Action);
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        sut.AddFieldCommand.Execute(FieldNature.Text);
        sut.DerivedTypeWord.Should().Be(Strings.Home_HabitType_Journal);
        raised.Should().Contain(nameof(HabitBuilderViewModel.DerivedTypeWord)).And.Contain(nameof(HabitBuilderViewModel.DerivedTypeLine));
    }

    // ---- strings changed by this task ---------------------------------------------------------------------

    [Theory]
    [InlineData("en-US", "Create habit", "Color", "Work", "Rest", "+ Timed session")]
    [InlineData("it-IT", "Crea abitudine", "Colore", "Lavoro", "Riposo", "+ Sessione a tempo")]
    public void The_creator_words(string culture, string launch, string color, string work, string rest, string session)
    {
        using var _ = Culture(culture);
        Strings.HabitBuilder_Launch.Should().Be(launch);
        Strings.HabitBuilder_Color.Should().Be(color);
        Strings.HabitBuilder_Segment_Work.Should().Be(work);
        Strings.HabitBuilder_Segment_Rest.Should().Be(rest);
        Strings.HabitBuilder_AddSession.Should().Be(session);
    }
}
