using System.Xml.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAssertions;
using Xunit;

namespace Recordum.UI.Tests.Icons;

/// <summary>Il catalogo delle 65 icone Lucide (Themes/Icons.axaml). Tre cose diverse possono andare
/// storte e ognuna ha il proprio test: le chiavi (WPF le chiede per nome, una mancante è un Path
/// vuoto muto), il parsing (una stringa malformata esplode alla prima richiesta, non al caricamento),
/// e il SIGNIFICATO dei tratti (una stringa che si legge senza errori ma disegna la cosa sbagliata).</summary>
public class IconCatalogTests
{
    private static readonly string[] ExpectedKeys =
    [
        "Icon.ChevronRight", "Icon.ChevronDown", "Icon.Pin", "Icon.Settings", "Icon.MoreVertical",
        "Icon.AlertTriangle", "Icon.Search", "Icon.Palette", "Icon.SlidersHorizontal", "Icon.LifeBuoy",
        "Icon.Info", "Icon.RefreshCw", "Icon.Puzzle", "Icon.Sparkles", "Icon.BarChart3", "Icon.Upload",
        "Icon.CircleHelp", "Icon.Dumbbell", "Icon.Bike", "Icon.Footprints", "Icon.Trophy", "Icon.Mountain",
        "Icon.Heart", "Icon.HeartPulse", "Icon.Pill", "Icon.Stethoscope", "Icon.Activity", "Icon.Brain",
        "Icon.Moon", "Icon.Sun", "Icon.Smile", "Icon.Leaf", "Icon.BookOpen", "Icon.GraduationCap",
        "Icon.PenLine", "Icon.Laptop", "Icon.Target", "Icon.Utensils", "Icon.Apple", "Icon.Coffee",
        "Icon.Droplet", "Icon.GlassWater", "Icon.House", "Icon.Bath", "Icon.Brush", "Icon.Shirt",
        "Icon.Sprout", "Icon.Cigarette", "Icon.Wine", "Icon.Ban", "Icon.ShieldCheck", "Icon.Flame",
        "Icon.Star", "Icon.CheckCircle", "Icon.Calendar", "Icon.Clock", "Icon.Square", "Icon.Music",
        "Icon.Play", "Icon.Pause", "Icon.Trash2", "Icon.Plus", "Icon.Maximize2", "Icon.X", "Icon.ImageOff",
    ];

    /// <summary>Le stringhe dei path così come stanno nel file (incorporato nell'assembly di test, v. il
    /// .csproj): è l'unico modo di guardare i segmenti, perché un <see cref="StreamGeometry"/> già
    /// risolto non li espone.</summary>
    private static Dictionary<string, string> RawPaths()
    {
        using var stream = typeof(IconCatalogTests).Assembly.GetManifestResourceStream("Icons.axaml")
            ?? throw new InvalidOperationException("Icons.axaml non è incorporato nell'assembly di test.");
        var doc = XDocument.Load(stream);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return doc.Root!.Elements()
            .Where(e => e.Attribute(x + "Key") is not null)
            .ToDictionary(e => (string)e.Attribute(x + "Key")!, e => e.Value.Trim());
    }

    private static List<PathFigure> Figures(string key) =>
        [.. PathGeometry.Parse(RawPaths()[key]).Figures!];

    [Fact]
    public void The_catalog_holds_exactly_the_65_expected_keys()
    {
        var keys = RawPaths().Keys;

        ExpectedKeys.Should().HaveCount(65);
        keys.Should().BeEquivalentTo(ExpectedKeys, "una chiave in meno è un'icona d'abitudine che sparisce; "
            + "una in più è un'icona che nessun test guarda");
    }

    [AvaloniaFact]
    public void Every_key_resolves_to_a_non_empty_StreamGeometry_through_the_application()
    {
        var app = Application.Current!;
        var problems = new List<string>();

        foreach (var key in ExpectedKeys)
        {
            try
            {
                if (!app.TryGetResource(key, ThemeVariant.Default, out var found))
                    problems.Add($"{key}: non trovata (manca il merge di Icons.axaml in App.axaml?)");
                else if (found is not StreamGeometry g)
                    problems.Add($"{key}: è {found?.GetType().Name ?? "null"}, non StreamGeometry");
                else if (g.Bounds.Width <= 0 && g.Bounds.Height <= 0)
                    problems.Add($"{key}: geometria vuota");
            }
            catch (Exception e)
            {
                problems.Add($"{key}: il parsing ha lanciato {e.GetType().Name}: {e.Message}");
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty();
    }

    [AvaloniaFact]
    public void Every_point_of_every_icon_stays_inside_the_24x24_viewbox()
    {
        // Le icone Lucide vivono in un viewbox 24x24. Un punto fuori è quasi sempre un lineto letto
        // come assoluto dove il disegnatore lo voleva relativo (v. Dumbbell/Sun/ShieldCheck/CheckCircle
        // nel file WPF): con Stretch=Uniform l'icona intera si rimpicciolisce per fare posto a un
        // tratto che non doveva esistere.
        var outside = new List<string>();

        foreach (var (key, data) in RawPaths())
        {
            foreach (var figure in PathGeometry.Parse(data).Figures!)
            {
                foreach (var p in PointsOf(figure))
                {
                    if (p.X < 0 || p.X > 24 || p.Y < 0 || p.Y > 24)
                        outside.Add($"{key}: ({p.X:0.##},{p.Y:0.##})");
                }
            }
        }

        string.Join("; ", outside).Should().BeEmpty();
    }

    [Fact]
    public void Corrected_strokes_end_where_Lucide_draws_them()
    {
        // Per ogni icona corretta, il TRATTO (start → end) come lo disegna Lucide. Le estremità sono
        // lette dai segmenti, non dai bounds: i bounds di Dumbbell, per dirne una, non si accorgono
        // che un tratto è finito a (1.4,-1.4) perché quel tratto non è il più esterno del disegno.
        AssertStroke("Icon.Dumbbell", start: (2.5, 21.5), end: (3.9, 20.1));
        AssertStroke("Icon.Dumbbell", start: (20.1, 3.9), end: (21.5, 2.5));
        AssertStroke("Icon.Dumbbell", start: (9.6, 14.4), end: (14.4, 9.6));

        // Sun: i quattro raggi diagonali vanno dal raggio 8 al raggio 10 dal centro (12,12).
        AssertStroke("Icon.Sun", start: (4.93, 4.93), end: (6.34, 6.34));
        AssertStroke("Icon.Sun", start: (17.66, 17.66), end: (19.07, 19.07));
        AssertStroke("Icon.Sun", start: (6.34, 17.66), end: (4.93, 19.07));
        AssertStroke("Icon.Sun", start: (19.07, 4.93), end: (17.66, 6.34));

        // La spunta: (9,12) → (11,14) → (15,10). Vale per due icone.
        foreach (var key in new[] { "Icon.ShieldCheck", "Icon.CheckCircle" })
        {
            var tick = Figures(key).Single(f => Near(f.StartPoint, (9, 12)));
            var pts = tick.Segments!.OfType<LineSegment>().Select(s => s.Point).ToList();
            pts.Should().HaveCount(2, key);
            Near(pts[0], (11, 14)).Should().BeTrue($"{key}: primo tratto della spunta a ({pts[0].X},{pts[0].Y})");
            Near(pts[1], (15, 10)).Should().BeTrue($"{key}: secondo tratto della spunta a ({pts[1].X},{pts[1].Y})");
        }

        // Pill: il divisore attraversa il centro (12,12) della capsula lungo la diagonale.
        AssertStroke("Icon.Pill", start: (8.5, 8.5), end: (15.5, 15.5));
    }

    [Fact]
    public void Relative_move_with_implicit_lineto_is_read_as_the_SVG_spec_says()
    {
        // I due casi che il porting doveva controllare a mano: `m` relativo seguito da più coppie
        // (il primo `m` di un path è assoluto, le coppie dopo sono lineto relativi).
        var mountain = Figures("Icon.Mountain").Single();
        Near(mountain.StartPoint, (8, 3)).Should().BeTrue();
        var pts = mountain.Segments!.OfType<LineSegment>().Select(s => s.Point).ToList();
        pts.Should().HaveCount(5);
        // m8 3 → (8,3); 4 8 → (12,11); 5-5 → (17,6); 5 15 → (22,21); H2 → (2,21); L8 3 → (8,3)
        Near(pts[0], (12, 11)).Should().BeTrue();
        Near(pts[1], (17, 6)).Should().BeTrue();
        Near(pts[2], (22, 21)).Should().BeTrue();
        Near(pts[3], (2, 21)).Should().BeTrue();
        Near(pts[4], (8, 3)).Should().BeTrue();

        // Brush: `m11 10 3 3` in testa alla stringa → (11,10) → (14,13).
        AssertStroke("Icon.Brush", start: (11, 10), end: (14, 13));
        // Pill: primo sottopath `m10.5 20.5 10-10…` → (10.5,20.5) → (20.5,10.5).
        AssertStroke("Icon.Pill", start: (10.5, 20.5), end: (20.5, 10.5));
    }

    private static void AssertStroke(string key, (double X, double Y) start, (double X, double Y) end)
    {
        var figure = Figures(key).FirstOrDefault(f => Near(f.StartPoint, start));
        figure.Should().NotBeNull($"{key}: nessun sottopath parte da ({start.X},{start.Y})");
        var first = figure!.Segments!.OfType<LineSegment>().FirstOrDefault();
        first.Should().NotBeNull($"{key}: il sottopath da ({start.X},{start.Y}) non ha un lineto");
        Near(first!.Point, end).Should().BeTrue(
            $"{key}: il tratto da ({start.X},{start.Y}) finisce a ({first.Point.X:0.##},{first.Point.Y:0.##}), atteso ({end.X},{end.Y})");
    }

    private static bool Near(Point p, (double X, double Y) e) =>
        Math.Abs(p.X - e.X) < 0.005 && Math.Abs(p.Y - e.Y) < 0.005;

    private static IEnumerable<Point> PointsOf(PathFigure figure)
    {
        yield return figure.StartPoint;
        foreach (var s in figure.Segments!)
        {
            switch (s)
            {
                case LineSegment l: yield return l.Point; break;
                case ArcSegment a: yield return a.Point; break;
                case BezierSegment b: yield return b.Point1; yield return b.Point2; yield return b.Point3; break;
                case QuadraticBezierSegment q: yield return q.Point1; yield return q.Point2; break;
                default: throw new InvalidOperationException($"segmento non gestito: {s.GetType().Name}");
            }
        }
    }
}
