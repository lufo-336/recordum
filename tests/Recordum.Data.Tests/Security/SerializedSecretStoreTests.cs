using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>F2c-4: una scrittura "ricorda" rimasta in volo (portachiavi in attesa di un prompt) non annulla un
/// "Dimentica" arrivato dopo.</summary>
public sealed class SerializedSecretStoreTests
{
    /// <summary>In memoria; la scrittura si ferma finche' non la si rilascia, come libsecret davanti a un prompt.</summary>
    private sealed class PromptingStore : ISecretStore
    {
        private readonly Dictionary<SecretKind, string> _items = [];
        public ManualResetEventSlim WriteEntered { get; } = new();
        public ManualResetEventSlim ReleaseWrite { get; } = new();
        public bool IsAvailable => true;
        public string? TryRead(SecretKind kind) { lock (_items) return _items.GetValueOrDefault(kind); }
        public void Write(SecretKind kind, string value)
        {
            WriteEntered.Set();
            // Tetto largo: su un runner saturo il test può riprendere dopo secondi, e una scrittura che si chiude da
            // sola per tempo lascia finire la cancellazione (falso rosso visto in CI Windows).
            ReleaseWrite.Wait(TimeSpan.FromSeconds(120));
            lock (_items) _items[kind] = value;
        }
        public void Delete(SecretKind kind) { lock (_items) _items.Remove(kind); }
    }

    // Thread propri, non il pool: il test aspetta in modo sincrono, e su un runner saturo un Task.Run non partiva
    // entro 5 s (rosso visto in CI Windows).
    private static Task OnOwnThread(Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    [Fact]
    public async Task A_forget_waits_for_a_late_remember_and_the_key_ends_up_forgotten()
    {
        var inner = new PromptingStore();
        var store = new SerializedSecretStore(inner);

        var remember = OnOwnThread(() => store.Write(SecretKind.DbKey, "k"));
        inner.WriteEntered.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
        var forget = OnOwnThread(() => store.Delete(SecretKind.DbKey));
        await Task.Delay(150);
        forget.IsCompleted.Should().BeFalse("la cancellazione aspetta la scrittura in volo");

        inner.ReleaseWrite.Set();
        await Task.WhenAll(remember, forget).WaitAsync(TimeSpan.FromSeconds(5));

        store.TryRead(SecretKind.DbKey).Should().BeNull("l'ultima parola e' del Dimentica");
    }

    [Fact]
    public async Task A_pending_write_of_one_secret_does_not_hold_the_other()
    {
        var inner = new PromptingStore();
        var store = new SerializedSecretStore(inner);

        var remember = OnOwnThread(() => store.Write(SecretKind.DbKey, "k"));
        inner.WriteEntered.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
        await OnOwnThread(() => store.Delete(SecretKind.AutoBackup)).WaitAsync(TimeSpan.FromSeconds(2));

        inner.ReleaseWrite.Set();
        await remember.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
