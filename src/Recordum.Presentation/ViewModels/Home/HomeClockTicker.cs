using Recordum.Core.Abstractions;

namespace Recordum.Presentation.ViewModels.Home;

/// <summary>
/// Un solo battito da 1 secondo condiviso da tutta la lista Home: le righe di astinenza registrano
/// una callback di refresh e una sola sorgente le serve tutte (spec §2 — un timer per la lista, non
/// uno per riga). <paramref name="ticks"/> null = non si batte: è il caso dei test headless, dove
/// <see cref="TickNow"/> pilota il fan-out a mano. Una sorgente in moto è radicata dal dispatcher,
/// quindi il proprietario DEVE <see cref="Clear"/> a ogni ricarica e <see cref="Dispose"/> allo
/// smontaggio, o continua a battere e perde memoria.
/// </summary>
public sealed class HomeClockTicker : IDisposable
{
    private readonly ITickSource? _tick;
    private readonly List<Action> _callbacks = new();

    public HomeClockTicker(ITickSourceFactory? ticks)
    {
        _tick = ticks?.Create(TimeSpan.FromSeconds(1));
        if (_tick is not null)
            _tick.Tick += OnTick;
    }

    /// <summary>Registra una callback per battito (il refresh dell'orologio di una riga di astinenza) e avvia la sorgente.</summary>
    public void Register(Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        _callbacks.Add(tick);
        _tick?.Start();
    }

    /// <summary>Scarta tutte le callback e ferma la sorgente. Chiamata prima di ogni ricostruzione della Home.</summary>
    public void Clear()
    {
        _callbacks.Clear();
        _tick?.Stop();
    }

    /// <summary>Invoca una volta ogni callback registrata. Cablata al battito, ed è anche l'ingresso
    /// manuale per i test. Una copia difende dalle callback che mutano la lista.</summary>
    public void TickNow()
    {
        foreach (var cb in _callbacks.ToArray())
            cb();
    }

    private void OnTick(object? sender, EventArgs e) => TickNow();

    public void Dispose()
    {
        Clear();
        if (_tick is not null)
        {
            _tick.Tick -= OnTick;
            _tick.Dispose();
        }
    }
}
