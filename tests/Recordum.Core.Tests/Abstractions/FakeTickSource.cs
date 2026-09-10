using Recordum.Core.Abstractions;

namespace Recordum.Core.Tests.Abstractions;

/// <summary>Un ITickSourceFactory pilotato a mano: i test decidono QUANDO passa un secondo, invece
/// di aspettarlo. Sostituisce la vecchia condizione "Application.Current è null, quindi non
/// ticchetta e questo pezzo non si prova".</summary>
public sealed class FakeTickSourceFactory : ITickSourceFactory
{
    private readonly List<FakeTickSource> _created = [];

    public IReadOnlyList<TimeSpan> RequestedIntervals => _created.Select(s => s.Interval).ToArray();

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
