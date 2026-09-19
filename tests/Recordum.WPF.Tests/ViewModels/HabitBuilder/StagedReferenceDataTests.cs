using FluentAssertions;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitBuilder;

/// <summary>Onda 2.5 T7 — the two micro-forms that stage a custom unit / scale from inside the
/// builder, without a round-trip to Configura.</summary>
public class StagedReferenceDataTests
{
    [Fact]
    public void Unit_form_requires_a_name()
    {
        var form = new NewUnitFormViewModel();
        form.CanConfirm.Should().BeFalse();

        form.Name = "   ";
        form.CanConfirm.Should().BeFalse();

        form.Name = "Tazze";
        form.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public void Scale_form_requires_two_labels()
    {
        var form = new NewScaleFormViewModel();
        form.Name = "Energia";
        form.LabelsText = "Bassa";
        form.CanConfirm.Should().BeFalse();
        form.LabelsText = "Bassa, Alta";
        form.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public void Scale_form_requires_a_name_too()
    {
        var form = new NewScaleFormViewModel { LabelsText = "Bassa, Alta" };
        form.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public void Scale_form_trims_labels_and_drops_the_blanks()
    {
        var form = new NewScaleFormViewModel { Name = "Energia", LabelsText = " Bassa , , Media ,Alta, " };

        form.Labels.Should().Equal("Bassa", "Media", "Alta");
    }
}
