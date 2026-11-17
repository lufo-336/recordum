using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Controls;

/// <summary>Mette ogni figlio (il cui <c>DataContext</c> è un <see cref="ScalePoint"/>) alla sua frazione della larghezza,
/// centrato sul punto e tenuto dentro i bordi. Con <see cref="HideOverlaps"/> un figlio che toccherebbe il precedente
/// visibile si fa trasparente (i nomi fitti di una scala stretta non si sovrappongono; l'opacità, non
/// <c>IsVisible</c>, perché cambiare la visibilità rimisurerebbe il pannello).</summary>
public sealed class ScalePointsPanel : Panel
{
    public static readonly StyledProperty<bool> HideOverlapsProperty =
        AvaloniaProperty.Register<ScalePointsPanel, bool>(nameof(HideOverlaps));

    public bool HideOverlaps { get => GetValue(HideOverlapsProperty); set => SetValue(HideOverlapsProperty, value); }

    static ScalePointsPanel() => AffectsArrange<ScalePointsPanel>(HideOverlapsProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        double h = 0, w = 0;
        foreach (var c in Children)
        {
            c.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            h = Math.Max(h, c.DesiredSize.Height);
            w = Math.Max(w, c.DesiredSize.Width);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? w : availableSize.Width, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var placed = Children.Select(c => (C: c, W: c.DesiredSize.Width, F: (c.DataContext as ScalePoint)?.Fraction ?? 0))
            .OrderBy(t => t.F).ToList();
        var xs = placed.Select(t => Math.Clamp(t.F * finalSize.Width - t.W / 2, 0, Math.Max(0, finalSize.Width - t.W))).ToList();
        // I punti senza nome (larghezza 0) non occupano posto. Gli estremi restano sempre; il resto, da sinistra, solo
        // se non tocca il precedente ne' l'ultimo.
        var sized = Enumerable.Range(0, placed.Count).Where(i => placed[i].W > 0).ToList();
        var shown = new HashSet<int>();
        if (!HideOverlaps) shown.UnionWith(sized);
        else if (sized.Count > 0)
        {
            var (first, last) = (sized[0], sized[^1]);
            shown.Add(first);
            shown.Add(last);
            var lastRight = xs[first] + placed[first].W;
            foreach (var i in sized.Skip(1).Take(Math.Max(0, sized.Count - 2)))
                if (xs[i] >= lastRight + 4 && xs[i] + placed[i].W <= xs[last] - 4) { shown.Add(i); lastRight = xs[i] + placed[i].W; }
        }
        for (var i = 0; i < placed.Count; i++)
        {
            placed[i].C.Arrange(new Rect(xs[i], 0, placed[i].W, finalSize.Height));
            if (HideOverlaps && placed[i].W > 0) placed[i].C.Opacity = shown.Contains(i) ? 1 : 0;
        }
        return finalSize;
    }
}
