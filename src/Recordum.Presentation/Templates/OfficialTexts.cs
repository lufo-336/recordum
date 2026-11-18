using Recordum.Core.Models;
using Recordum.Data.SeedData;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.Templates;

/// <summary>I testi dei modelli, delle scale e delle unità ufficiali nella lingua dell'interfaccia. I dati seminati
/// restano in inglese e non si toccano (nessuna migrazione): la traduzione sta qui, nei <c>.resx</c>, ed è chiesta
/// per Id (modelli) o per codice (scale, unità) stabili, mai per nome. Ciò che l'utente ha scritto non passa di qui.
/// Un testo senza traduzione esce com'è stato seminato: mai vuoto, mai un'eccezione.</summary>
public static class OfficialTexts
{
    private static readonly HashSet<Guid> s_officialTemplateIds = HabitTemplateSeed.All.Select(t => t.Id).ToHashSet();

    private static readonly Dictionary<Guid, (Func<string> Name, Func<string> Description)> s_templates = new()
    {
        [Id(1)] = (() => Strings.Template_Official_01_Name, () => Strings.Template_Official_01_Description),
        [Id(2)] = (() => Strings.Template_Official_02_Name, () => Strings.Template_Official_02_Description),
        [Id(3)] = (() => Strings.Template_Official_03_Name, () => Strings.Template_Official_03_Description),
        [Id(4)] = (() => Strings.Template_Official_04_Name, () => Strings.Template_Official_04_Description),
        [Id(5)] = (() => Strings.Template_Official_05_Name, () => Strings.Template_Official_05_Description),
        [Id(6)] = (() => Strings.Template_Official_06_Name, () => Strings.Template_Official_06_Description),
        [Id(7)] = (() => Strings.Template_Official_07_Name, () => Strings.Template_Official_07_Description),
        [Id(8)] = (() => Strings.Template_Official_08_Name, () => Strings.Template_Official_08_Description),
    };

    /// <summary>Testo seminato nel contenuto dei modelli (nome del nodo, di una metrica, di una serie, di un segmento)
    /// → il suo accessor. Un solo dizionario per tutti: lo stesso testo ("Duration") è lo stesso in ogni modello.</summary>
    private static readonly Dictionary<string, Func<string>> s_texts = new(StringComparer.Ordinal)
    {
        ["Morning routine"] = () => Strings.Template_Text_MorningRoutine,
        ["Workout log"] = () => Strings.Template_Text_WorkoutLog,
        ["Daily journal"] = () => Strings.Template_Text_DailyJournal,
        ["Water intake"] = () => Strings.Template_Text_WaterIntake,
        ["Sleep tracker"] = () => Strings.Template_Text_SleepTracker,
        ["Stop smoking"] = () => Strings.Template_Text_StopSmoking,
        ["Mood tracker"] = () => Strings.Template_Text_MoodTracker,
        ["Food diary"] = () => Strings.Template_Text_FoodDiary,
        ["Duration"] = () => Strings.Template_Text_Duration,
        ["Reps"] = () => Strings.Template_Text_Reps,
        ["Weight"] = () => Strings.Template_Text_Weight,
        ["Amount"] = () => Strings.Template_Text_Amount,
        ["Hours slept"] = () => Strings.Template_Text_HoursSlept,
        ["Sleep quality"] = () => Strings.Template_Text_SleepQuality,
        ["Mood"] = () => Strings.Template_Text_Mood,
        ["Night"] = () => Strings.Template_Text_Night,
        ["Nap"] = () => Strings.Template_Text_Nap,
        ["Work"] = () => Strings.Template_Text_Work,
        ["Rest"] = () => Strings.Template_Text_Rest,
    };

    private static readonly Dictionary<string, (Func<string> Name, Func<string> Labels)> s_scales = new(StringComparer.Ordinal)
    {
        ["bristol"] = (() => Strings.Scale_Bristol_Name, () => Strings.Scale_Bristol_Labels),
        ["mayo-rectal-bleeding"] = (() => Strings.Scale_MayoRectalBleeding_Name, () => Strings.Scale_MayoRectalBleeding_Labels),
        ["nrs-pain"] = (() => Strings.Scale_NrsPain_Name, () => Strings.Scale_NrsPain_Labels),
        ["likert-5"] = (() => Strings.Scale_Likert5_Name, () => Strings.Scale_Likert5_Labels),
        ["sleep-quality"] = (() => Strings.Scale_SleepQuality_Name, () => Strings.Scale_SleepQuality_Labels),
        ["stress-10"] = (() => Strings.Scale_Stress10_Name, () => Strings.Scale_Stress10_Labels),
        ["mood-5"] = (() => Strings.Scale_Mood5_Name, () => Strings.Scale_Mood5_Labels),
    };

    private static readonly Dictionary<string, Func<string>> s_units = new(StringComparer.Ordinal)
    {
        ["km"] = () => Strings.Unit_Km_Name,
        ["m"] = () => Strings.Unit_M_Name,
        ["kg"] = () => Strings.Unit_Kg_Name,
        ["g"] = () => Strings.Unit_G_Name,
        ["ml"] = () => Strings.Unit_Ml_Name,
        ["l"] = () => Strings.Unit_L_Name,
        ["kcal"] = () => Strings.Unit_Kcal_Name,
        ["steps"] = () => Strings.Unit_Steps_Name,
        ["hours"] = () => Strings.Unit_Hours_Name,
        ["minutes"] = () => Strings.Unit_Minutes_Name,
        ["seconds"] = () => Strings.Unit_Seconds_Name,
        ["pages"] = () => Strings.Unit_Pages_Name,
        ["reps"] = () => Strings.Unit_Reps_Name,
        ["sets"] = () => Strings.Unit_Sets_Name,
    };

    private static Guid Id(int n) => Guid.Parse($"00000003-0000-0000-0000-{n:D12}");

    /// <summary>Vero per i soli modelli seminati: l'Id decide, non il nome né il flag <c>IsOfficial</c> di una riga.</summary>
    public static bool IsOfficialTemplate(Guid templateId) => s_officialTemplateIds.Contains(templateId);

    public static string TemplateName(HabitTemplate t) =>
        s_templates.TryGetValue(t.Id, out var x) ? x.Name() : t.Name;

    public static string? TemplateDescription(HabitTemplate t) =>
        s_templates.TryGetValue(t.Id, out var x) ? x.Description() : t.Description;

    /// <summary>Un testo del contenuto di un modello. Fuori dai modelli ufficiali, o se il testo non è noto,
    /// torna <paramref name="seeded"/>.</summary>
    public static string TemplateText(Guid templateId, string seeded) =>
        s_officialTemplateIds.Contains(templateId) && s_texts.TryGetValue(seeded, out var text) ? text() : seeded;

    public static string ScaleName(ScaleTemplate s) =>
        s.IsOfficial && s_scales.TryGetValue(s.Code, out var x) ? x.Name() : s.Name;

    /// <summary>Il nome tradotto dello scaglione <paramref name="value"/> di una scala ufficiale (posizione
    /// <c>value - MinValue</c>); null per una scala dell'utente, senza traduzione o fuori range.</summary>
    public static string? ScaleAnchorLabel(ScaleTemplate s, int value)
    {
        if (!s.IsOfficial || !s_scales.TryGetValue(s.Code, out var x)) return null;
        var labels = x.Labels().Split('|');
        var p = value - s.MinValue;
        return p >= 0 && p < labels.Length ? labels[p] : null;
    }

    public static string UnitName(string code, string name, bool isOfficial) =>
        isOfficial && s_units.TryGetValue(code, out var x) ? x() : name;

    public static string UnitName(UnitOfMeasure u) =>
        UnitName(u.Code, u.Name, u.IsOfficial);
}
