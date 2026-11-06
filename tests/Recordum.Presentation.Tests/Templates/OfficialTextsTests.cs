using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Models;
using Recordum.Data.SeedData;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Templates;
using Xunit;

namespace Recordum.Presentation.Tests.Templates;

/// <summary>UX-11: i testi dei modelli, delle scale e delle unità ufficiali vengono dai <c>.resx</c>, per Id o codice
/// stabili. In <c>it-IT</c> ogni testo deve differire dal seminato (l'inglese coincide per costruzione), e un testo
/// seminato di domani senza chiave fa fallire l'elenco qui sotto.</summary>
[Collection(ProcessGlobalCultureCollection.Name)]
public sealed class OfficialTextsTests : IDisposable
{
    private readonly CultureInfo? _previous = Strings.Culture;

    public void Dispose() => Strings.Culture = _previous;

    private static void Pin(string culture) => Strings.Culture = CultureInfo.GetCultureInfo(culture);

    private static readonly Guid DailyJournalId = Guid.Parse("00000003-0000-0000-0000-000000000003");

    /// <summary>Ogni testo che l'utente vede nel contenuto del payload: nodo, metriche, serie, segmenti.</summary>
    private static IEnumerable<string> PayloadTexts(HabitTemplate t)
    {
        using var doc = JsonDocument.Parse(t.PayloadJson);
        var root = doc.RootElement;
        yield return root.GetProperty("node").GetProperty("name").GetString()!;
        foreach (var m in root.GetProperty("metrics").EnumerateArray()) yield return m.GetProperty("name").GetString()!;
        if (root.GetProperty("config").TryGetProperty("series_labels", out var labels))
            foreach (var l in labels.EnumerateArray()) yield return l.GetString()!;
        foreach (var s in root.GetProperty("segments").EnumerateArray()) yield return s.GetProperty("name").GetString()!;
    }

    [Fact]
    public void Every_seeded_template_text_has_a_translation()
    {
        foreach (var t in HabitTemplateSeed.All)
        {
            Pin("en-US");
            OfficialTexts.TemplateName(t).Should().Be(t.Name);
            OfficialTexts.TemplateDescription(t).Should().Be(t.Description);
            foreach (var text in PayloadTexts(t)) OfficialTexts.TemplateText(t.Id, text).Should().Be(text, $"{t.Name}: «{text}» in English is the seeded text");

            Pin("it-IT");
            OfficialTexts.TemplateName(t).Should().NotBe(t.Name, t.Name);
            OfficialTexts.TemplateDescription(t).Should().NotBe(t.Description, t.Name);
            foreach (var text in PayloadTexts(t)) OfficialTexts.TemplateText(t.Id, text).Should().NotBe(text, $"{t.Name}: «{text}» has no Italian text");
        }
    }

    /// <summary>La scheda usa <c>Template.Official.NN.Name</c>, l'abitudine creata <c>Template.Text.&lt;Nome&gt;</c>: due chiavi
    /// con lo stesso testo, che un traduttore potrebbe far divergere. Qui restano uguali.</summary>
    [Fact]
    public void The_card_name_and_the_created_habit_name_are_the_same_text()
    {
        foreach (var culture in new[] { "en-US", "it-IT" })
        {
            Pin(culture);
            foreach (var t in HabitTemplateSeed.All)
            {
                var seededNode = PayloadTexts(t).First();
                OfficialTexts.TemplateName(t).Should().Be(OfficialTexts.TemplateText(t.Id, seededNode), $"{culture}: {t.Name}");
            }
        }
    }

    [Fact]
    public void Only_the_eight_seeded_ids_are_official()
    {
        HabitTemplateSeed.All.Should().HaveCount(8).And.OnlyContain(t => OfficialTexts.IsOfficialTemplate(t.Id));
        OfficialTexts.IsOfficialTemplate(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Every_official_scale_and_unit_has_a_name()
    {
        foreach (var s in ScaleTemplateSeed.All)
        {
            Pin("en-US");
            OfficialTexts.ScaleName(s).Should().Be(s.Name);
            Pin("it-IT");
            OfficialTexts.ScaleName(s).Should().NotBe(s.Name, s.Code);
        }
        foreach (var u in UnitOfMeasureSeed.All)
        {
            Pin("en-US");
            OfficialTexts.UnitName(u).Should().Be(u.Name);
            Pin("it-IT");
            OfficialTexts.UnitName(u).Should().NotBe(u.Name, u.Code);
        }
    }

    [Fact]
    public void Official_scale_labels_match_the_seeded_count()
    {
        foreach (var s in ScaleTemplateSeed.All)
        {
            var seeded = JsonSerializer.Deserialize<List<string>>(s.LabelsJson)!;
            Pin("en-US");
            OfficialTexts.ScaleLabels(s).Should().Equal(seeded, s.Code);
            Pin("it-IT");
            var italian = OfficialTexts.ScaleLabels(s)!;
            italian.Should().HaveCount(seeded.Count, s.Code);
            italian.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l));
            italian.Should().NotEqual(seeded, s.Code);
        }
    }

    [Fact]
    public void Templates_translate_by_id_not_by_name()
    {
        Pin("it-IT");
        var user = new HabitTemplate { Id = Guid.NewGuid(), Name = "Daily journal", Description = "Mine", IsOfficial = false };
        var official = HabitTemplateSeed.All.Single(t => t.Id == DailyJournalId);

        OfficialTexts.TemplateName(user).Should().Be("Daily journal");
        OfficialTexts.TemplateDescription(user).Should().Be("Mine");
        OfficialTexts.TemplateName(official).Should().Be("Diario quotidiano");
    }

    [Fact]
    public void An_unknown_text_falls_back_to_the_seeded_one()
    {
        Pin("it-IT");

        OfficialTexts.TemplateText(DailyJournalId, "Qualcosa").Should().Be("Qualcosa");
        OfficialTexts.TemplateText(Guid.NewGuid(), "Night").Should().Be("Night");   // non ufficiale: mai tradotto
    }

    [Fact]
    public void User_scales_and_units_keep_their_names_and_have_no_translated_labels()
    {
        Pin("it-IT");
        var scale = new ScaleTemplate { Code = "mood-5", Name = "Mio umore", LabelsJson = """["a","b"]""", IsOfficial = false };
        var unit = new UnitOfMeasure { Code = "l", Name = "Bicchieri", IsOfficial = false };

        OfficialTexts.ScaleName(scale).Should().Be("Mio umore");
        OfficialTexts.ScaleLabels(scale).Should().BeNull();
        OfficialTexts.UnitName(unit).Should().Be("Bicchieri");
    }
}
