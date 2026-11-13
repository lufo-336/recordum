using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class BuilderHomeRowPinTests
{
    // Fissata PRIMA del refactor di BuildHomeRow: l'anteprima del builder non deve cambiare. Unico cambio voluto (GIO-3,
    // 2026-11-13): ogni tipo ha la striscia di 14 giorni, non più 7 fuori dall'astinenza.
    [Theory]
    [InlineData(HabitType.Action, "5", StripStyle.Squares, 14)]
    [InlineData(HabitType.Counter, "2", StripStyle.Bars, 14)]
    [InlineData(HabitType.Resistance, "20g 4:12", StripStyle.Band, 14)]
    [InlineData(HabitType.Journal, "1", StripStyle.Dots, 14)]
    [InlineData(HabitType.State, "2", StripStyle.Ramp, 14)]
    public void Builder_preview_is_unchanged(HabitType type, string keyFigure, StripStyle style, int length)
    {
        var input = new BuilderPreviewInput("Tè", "#123456", "star", type, false, false, JournalPrimaryGesture.Diary, [], [], [], null);

        var row = new SamplePreviewFactory(new FakeClock()).Build(input).HomeRow;

        int[] pattern = [1, 0, 1, 1, 0, 1, 1];
        var counts = Enumerable.Range(0, length).Select(i => pattern[i % 7]).ToImmutableArray();
        row.Name.Should().Be("Tè");
        row.Color.Should().Be("#123456");
        row.Icon.Should().Be("star");
        row.KeyFigure.Should().Be(keyFigure);
        row.StripStyle.Should().Be(style);
        row.Counts.Should().Equal(counts);
        row.Fractions.Should().Equal(counts.Select(c => c == 0 ? 0d : 0.4 + (0.2 * 1)));
    }
}
