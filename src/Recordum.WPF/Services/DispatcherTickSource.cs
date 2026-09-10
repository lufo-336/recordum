using System.Windows.Threading;
using Recordum.Core.Abstractions;

namespace Recordum.WPF.Services;

/// <summary>ITickSourceFactory su DispatcherTimer di WPF. Muore con la shell WPF: la gemella
/// Avalonia è src/Recordum.UI/Services/DispatcherTickSource.cs, identica salvo il namespace del
/// timer.</summary>
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
            // Un DispatcherTimer in moto è radicato dal dispatcher: senza Stop() + il distacco
            // dell'handler continua a battere e tiene in vita il proprio proprietario.
            _timer.Stop();
            _timer.Tick -= OnTick;
            Tick = null;
        }
    }
}
