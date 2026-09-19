namespace Recordum.Presentation.Services;

/// <summary>Curated, searchable list of Lucide icon resource keys available to the icon picker.
/// Each entry is a key into Resources/Icons.xaml. Keep in sync with that file (covered by a test).</summary>
public static class IconCatalog
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        // movement / sport
        "Icon.Dumbbell", "Icon.Bike", "Icon.Footprints", "Icon.Trophy", "Icon.Mountain",
        // health / medical
        "Icon.Heart", "Icon.HeartPulse", "Icon.Pill", "Icon.Stethoscope", "Icon.Activity",
        // mind / sleep
        "Icon.Brain", "Icon.Moon", "Icon.Sun", "Icon.Smile", "Icon.Leaf",
        // study / work
        "Icon.BookOpen", "Icon.GraduationCap", "Icon.PenLine", "Icon.Laptop", "Icon.Target",
        // food / hydration
        "Icon.Utensils", "Icon.Apple", "Icon.Coffee", "Icon.Droplet", "Icon.GlassWater",
        // home / care
        "Icon.House", "Icon.Bath", "Icon.Brush", "Icon.Shirt", "Icon.Sprout",
        // abstinence / control
        "Icon.Cigarette", "Icon.Wine", "Icon.Ban", "Icon.ShieldCheck", "Icon.Flame",
        // misc / general
        "Icon.Star", "Icon.CheckCircle", "Icon.Calendar", "Icon.Clock", "Icon.Music",
    };

    public static IReadOnlyList<string> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        var q = query.Trim();
        return All.Where(k => Name(k).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // "Icon.BookOpen" → "BookOpen" (the searchable name).
    private static string Name(string key) => key.StartsWith("Icon.", StringComparison.Ordinal) ? key[5..] : key;
}
