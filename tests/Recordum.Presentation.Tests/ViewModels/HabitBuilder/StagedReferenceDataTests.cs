using FluentAssertions;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitBuilder;

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
}
