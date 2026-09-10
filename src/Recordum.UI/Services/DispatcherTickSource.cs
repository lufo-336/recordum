using Avalonia.Threading;
using Recordum.Core.Abstractions;

namespace Recordum.UI.Services;

/// <summary>ITickSourceFactory su DispatcherTimer di Avalonia. Gemella di quella WPF: stesso nome
/// di tipo del timer, altro assembly. È tutta la differenza che c'era da astrarre.</summary>
public sealed class DispatcherTickSourceFactory : ITickSourceFactory
{
    public ITickSource Create(TimeSpan interval) => new DispatcherTickSource(interval);

    private sealed class DispatcherTickSource : ITickSource
    {
        private readonly DispatcherTimer _timer;

        public DispatcherTickSource(TimeSpan interval)
        {
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += OnTick;
        }

        public event EventHandler? Tick;

        private void OnTick(object? sender, EventArgs e) => Tick?.Invoke(this, EventArgs.Empty);

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            Tick = null;
        }
    }
}
