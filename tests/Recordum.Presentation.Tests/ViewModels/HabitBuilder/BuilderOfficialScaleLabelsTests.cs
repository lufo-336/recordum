using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.SeedData;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitBuilder;

/// <summary>UX-11: un campo su una scala ufficiale nasce con le etichette nella lingua dell'interfaccia (fissate nella
/// <c>LabelsJson</c> della metrica); una scala dell'utente tiene le sue. Le culture si fissano: in inglese le etichette
/// tradotte coincidono con le seminate e il test non distinguerebbe niente.</summary>
[Collection(ProcessGlobalCultureCollection.Name)]
public sealed class BuilderOfficialScaleLabelsTests : IDisposable
{
    private readonly CultureInfo? _previous = Strings.Culture;

    public void Dispose() => Strings.Culture = _previous;

    private static async Task<(HabitBuilderViewModel Sut, ScaleTemplate Scale)> VmOnAsync(ScaleTemplate scale)
    {
        var refData = new FakeReferenceData();
        refData.Scales.Clear();
        refData.Scales.Add(scale);
        var settings = new FakeAppSettingService();
        var sut = new HabitBuilderViewModel(new FakeComposedHabitService(), new FakeHabitTemplateService(), refData, new FakeClock(),
            new RecentColorStore(settings), new FavoriteColorStore(settings), new FakeScreenColorPicker());
        await sut.InitializeAsync();
        sut.Name = "Umore";
        sut.AddFieldCommand.Execute(FieldNature.Scale);
        sut.Fields[0].ScaleTemplateId = scale.Id;
        return (sut, scale);
    }

    private static ScaleTemplate Mood5() => ScaleTemplateSeed.All.Single(s => s.Code == "mood-5");

    private static string?[] LabelsOf(HabitBuilderViewModel sut) =>
        JsonSerializer.Deserialize<string[]>(sut.BuildRequestForTest().Metrics.Single().LabelsJson!)!;

    [Fact]
    public async Task A_field_on_an_official_scale_is_born_with_translated_labels()
    {
        Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
        var (sut, _) = await VmOnAsync(Mood5());

        sut.BuildRequestForTest().Metrics.Single().LabelsJson
            .Should().Be("""["Molto basso","Basso","Neutro","Buono","Molto buono"]""");

        Strings.Culture = CultureInfo.GetCultureInfo("en-US");
        LabelsOf(sut).Should().Equal("Very low", "Low", "Neutral", "Good", "Very good");
    }

    [Fact]
    public async Task Translated_labels_are_stored_readable_not_escaped()
    {
        Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
        var (sut, _) = await VmOnAsync(ScaleTemplateSeed.All.Single(s => s.Code == "likert-5"));

        sut.BuildRequestForTest().Metrics.Single().LabelsJson.Should().Contain("D'accordo").And.NotContain("\\u");
    }

    [Fact]
    public async Task A_field_on_a_user_scale_keeps_its_own_labels()
    {
        Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
        var mine = new ScaleTemplate { Id = Guid.NewGuid(), Code = "mood-5", Name = "Mio", LabelsJson = """["Male","Bene"]""", IsOfficial = false };
        var (sut, _) = await VmOnAsync(mine);

        sut.BuildRequestForTest().Metrics.Single().LabelsJson.Should().Be("""["Male","Bene"]""");
    }
}
