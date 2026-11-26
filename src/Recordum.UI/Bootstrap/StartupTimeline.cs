using System.Globalization;
using System.Text;

namespace Recordum.UI.Bootstrap;

/// <summary>Raccoglie i tempi dei passi d'avvio da thread diversi e li formatta su una riga diagnostica.</summary>
public sealed class StartupTimeline(Func<TimeSpan> elapsed)
{
    private readonly Func<TimeSpan> _elapsed = elapsed ?? throw new ArgumentNullException(nameof(elapsed));
    private readonly Lock _gate = new();
    private readonly List<(string Step, TimeSpan At)> _marks = [];

    public void Mark(string step)
    {
        ArgumentException.ThrowIfNullOrEmpty(step);
        var at = _elapsed();
        lock (_gate)
        {
            _marks.Add((step, at));
        }
    }

    public IReadOnlyList<(string Step, TimeSpan At)> Marks
    {
        get
        {
            lock (_gate)
            {
                return [.. _marks];
            }
        }
    }

    public string Format()
    {
        (string Step, TimeSpan At)[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _marks];
        }

        if (snapshot.Length == 0) return "startup";

        var sb = new StringBuilder("startup");
        foreach (var (step, at) in snapshot)
        {
            sb.Append(' ')
              .Append(step)
              .Append('=')
              .Append(((long)at.TotalMilliseconds).ToString(CultureInfo.InvariantCulture))
              .Append("ms");
        }
        return sb.ToString();
    }
}
