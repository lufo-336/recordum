namespace Recordum.Core.Abstractions;

/// <summary>La fabbrica che produce sorgenti che non battono mai. È la forma esplicita di ciò che
/// prima era implicito nella guardia <c>if (Application.Current is not null)</c>: un ViewModel in
/// un test headless, o un'anteprima deliberatamente inerte come quella del builder, riceve questa
/// invece di null-e-una-guardia sparsa in quattro classi.</summary>
public sealed class NullTickSourceFactory : ITickSourceFactory
{
    public static NullTickSourceFactory Instance { get; } = new();

    private NullTickSourceFactory() { }

    public ITickSource Create(TimeSpan interval) => new NullTickSource();

    private sealed class NullTickSource : ITickSource
    {
        // Il campo non è mai invocato: esiste perché ITickSource lo richiede. CS0067 (evento mai
        // usato) sarebbe un errore con TreatWarningsAsErrors, quindi lo si sopprime qui e solo qui.
#pragma warning disable CS0067
        public event EventHandler? Tick;
#pragma warning restore CS0067

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
