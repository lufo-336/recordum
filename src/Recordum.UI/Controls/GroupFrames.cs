using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Controls;

/// <summary>Disegna, dietro la card di una riga della Home, le fette delle cornici dei gruppi aperti
/// che la attraversano. La Home è una lista piatta e virtualizzata (F1-PERF1): la cornice che in WPF
/// era un Border attorno ai figli qui si compone riga per riga. Ogni fetta è il rettangolo arrotondato
/// intero, prolungato oltre la riga dal lato in cui la cornice continua e tagliato dai bordi del
/// controllo: restano gli spigoli solo dove la cornice si apre o si chiude. Nessun figlio visuale.</summary>
public sealed class GroupFrames : Control
{
    // Le misure sono quelle del layout annidato di WPF e del primo Avalonia: cornice con margine 1
    // sopra e sotto, bordo 1, padding 3; figli rientrati 20 a sinistra, 2 a destra, 2 sopra, 1 sotto.

    /// <summary>Di quanto rientra ogni livello: bordo 1 + padding 3 + 20 di rientro dei figli.</summary>
    public const double IndentStep = 24;
    /// <summary>Di quanto rientra a destra ogni livello: bordo 1 + padding 3 + 2 di margine dei figli.</summary>
    public const double RightStep = 6;
    /// <summary>Distanza fra i fondi di due cornici che si chiudono sulla stessa riga, e fra la card
    /// e il fondo della cornice che si chiude: 1 sotto i figli + padding 3 + bordo 1 + margine 1.</summary>
    public const double ClosingStep = 6;

    private const double Radius = 8;

    public static readonly StyledProperty<IReadOnlyList<GroupFrameSlice>?> SlicesProperty =
        AvaloniaProperty.Register<GroupFrames, IReadOnlyList<GroupFrameSlice>?>(nameof(Slices));

    static GroupFrames() => AffectsRender<GroupFrames>(SlicesProperty);

    public GroupFrames()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    public IReadOnlyList<GroupFrameSlice>? Slices
    {
        get => GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Slices is not { Count: > 0 } slices) return;
        var (w, h) = (Bounds.Width, Bounds.Height);
        var closed = 0;   // cornici già chiuse su questa riga: la più esterna ha il fondo più in basso
        foreach (var s in slices)   // dalla più esterna
        {
            var bottom = s.IsBottom ? h - 1 - ClosingStep * closed++ : h + 2 * Radius;
            if (!Color.TryParse(s.ColorHex, out var c)) continue;
            var top = s.IsTop ? 1 : -2 * Radius;
            var rect = new Rect(s.Level * IndentStep, top, w - s.Level * (IndentStep + RightStep), bottom - top)
                .Deflate(0.5);   // il tratto da 1 cade sul pixel, non a cavallo di due
            // Stessi alfa della cornice WPF: fondo 10, bordo 80.
            context.DrawRectangle(new ImmutableSolidColorBrush(Color.FromArgb(10, c.R, c.G, c.B)),
                new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(80, c.R, c.G, c.B))),
                rect, Radius, Radius);
        }
    }
}
