namespace Recordum.WPF.Services;

/// <summary>Generates a light→dark tonal ramp around a base colour (HSV: vary V, ease S),
/// with the base colour at the centre. Pure; used by the colour picker's "Tonalità" row.</summary>
public static class ShadeRamp
{
    public static IReadOnlyList<string> For(string hex, int steps = 7)
    {
        if (HsvColor.FromHex(hex) is not { } hsv) return [];
        var (h, s, v) = hsv;
        var mid = steps / 2;
        var result = new string[steps];
        for (var i = 0; i < steps; i++)
        {
            double ns, nv;
            if (i == mid) { ns = s; nv = v; }
            else if (i < mid)            // lighter: raise V toward 1, ease S down
            {
                var k = (mid - i) / (double)mid;
                nv = v + (1.0 - v) * k * 0.9;
                ns = s * (1 - 0.6 * k);
            }
            else                         // darker: lower V, nudge S up
            {
                var k = (i - mid) / (double)(steps - 1 - mid);
                nv = v * (1 - 0.65 * k);
                ns = s + (1 - s) * 0.2 * k;
            }
            result[i] = HsvColor.ToHex(h, System.Math.Clamp(ns, 0, 1), System.Math.Clamp(nv, 0, 1));
        }
        return result;
    }
}
