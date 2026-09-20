using Recordum.Core.Abstractions;

namespace Recordum.Core.Tests.Abstractions;

/// <summary>Un ITickSourceFactory pilotato a mano: i test decidono QUANDO passa un secondo, invece
/// di aspettarlo. Sostituisce la vecchia condizione "Application.Current è null, quindi non
/// ticchetta e questo pezzo non si prova".</summary>
public sealed class FakeTickSourceFactory : ITickSourceFactory
{
    private readonly List<FakeTickSource> _created = [];

    public IReadOnlyList<TimeSpan> RequestedIntervals => _created.Select(s => s.Interval).ToArray();

    /// <summary>Quante sorgenti create battono ora (avviate, non fermate, non disposte). È ciò che
    /// permette a un test di osservare il ciclo di vita della SORGENTE, non solo gli effetti a valle:
    /// una sorgente che continua a battere dopo lo smontaggio è esattamente la perdita che ITickSource
    /// esiste per evitare.</summary>
    public int RunningCount => _created.Count(s => s.IsRunning);

    public ITickSource Create(TimeSpan interval)
    {
        var source = new FakeTickSource(interval);
        _created.Add(source);
        return source;
    }

    /// <summary>Fa scattare un tick su ogni sorgente avviata e non disposta.</summary>
    public void TickAll()
    {
        foreach (var s in _created.ToArray())
            s.RaiseIfRunning();
    }

    private sealed class FakeTickSource(TimeSpan interval) : ITickSource
    {
        private bool _running;
        private bool _disposed;

        public TimeSpan Interval { get; } = interval;
        public bool IsRunning => _running && !_disposed;
        public event EventHandler? Tick;

        public void Start() => _running = !_disposed;
        public void Stop() => _running = false;

        public void RaiseIfRunning()
        {
            if (_running && !_disposed)
                Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _running = false;
            _disposed = true;
            Tick = null;
        }
    }
}
