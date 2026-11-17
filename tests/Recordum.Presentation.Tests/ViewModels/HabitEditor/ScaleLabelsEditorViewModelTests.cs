using FluentAssertions;
using Recordum.Presentation.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

[Collection(ProcessGlobalCultureCollection.Name)]
public class ScaleLabelsEditorViewModelTests
{
    [Fact]
    public void Existing_scale_has_one_row_per_value()
    {
        var sut = new ScaleLabelsEditorViewModel();
        sut.LoadFixed(0, 10, ["zero", "uno"]);

        sut.FixedCount.Should().BeTrue();
        sut.Rows.Select(r => r.Value).Should().Equal(Enumerable.Range(0, 11));
        sut.Rows.Take(3).Select(r => r.Text).Should().Equal("zero", "uno", "");
        sut.CanRemove.Should().BeFalse();
        sut.AddRowCommand.Execute(null);   // una scala esistente non cresce
        sut.Rows.Should().HaveCount(11);
    }

    [Fact]
    public void New_scale_adds_and_removes_rows_down_to_two()
    {
        var sut = new ScaleLabelsEditorViewModel();
        sut.LoadDraft();
        sut.FixedCount.Should().BeFalse();
        sut.Rows.Should().HaveCount(2);
        sut.CanRemove.Should().BeFalse();

        sut.AddRowCommand.Execute(null);
        sut.AddRowCommand.Execute(null);
        sut.Rows.Select(r => r.Value).Should().Equal(1, 2, 3, 4);
        sut.CanRemove.Should().BeTrue();

        sut.RemoveRowCommand.Execute(sut.Rows[1]);
        sut.Rows.Select(r => r.Value).Should().Equal(1, 2, 3);   // i valori scalano
        sut.RemoveRowCommand.Execute(sut.Rows[0]);
        sut.RemoveRowCommand.Execute(sut.Rows[0]);               // sotto il minimo: ignorato
        sut.Rows.Should().HaveCount(2);
        sut.CanRemove.Should().BeFalse();
    }

    [Fact]
    public void Editing_a_row_raises_LabelsChanged_but_loading_does_not()
    {
        var sut = new ScaleLabelsEditorViewModel();
        var raised = 0;
        sut.LabelsChanged += (_, _) => raised++;

        sut.LoadFixed(1, 3, ["a", "b", "c"]);
        sut.LoadDraft(["x", "y", "z"]);
        raised.Should().Be(0);

        sut.Rows[0].Text = "q";
        raised.Should().Be(1);
    }

    [Fact]
    public void New_scale_labels_drop_blank_rows_and_a_fixed_one_keeps_its_gaps()
    {
        var n = new ScaleLabelsEditorViewModel();
        n.LoadDraft(["Bassa", " ", "Alta"]);
        n.Labels.Should().Equal("Bassa", "Alta");

        var f = new ScaleLabelsEditorViewModel();
        f.LoadFixed(1, 3, ["a", "", "c"]);
        f.Labels.Should().Equal("a", "", "c");
        f.Rows[0].Text = "";
        f.Rows[2].Text = "";
        f.Labels.Should().BeEmpty();
    }

    [Fact]
    public void A_row_has_an_accessible_name_with_its_step_value()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            var sut = new ScaleLabelsEditorViewModel();
            sut.LoadFixed(1, 3, []);
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            sut.Rows[2].AutomationName.Should().Be("Label for 3");
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            sut.Rows[2].AutomationName.Should().Be("Etichetta per 3");
        }
        finally { Recordum.Presentation.Resources.Strings.Culture = previous; }
    }
}
