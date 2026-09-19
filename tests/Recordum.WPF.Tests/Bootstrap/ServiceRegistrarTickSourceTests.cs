using Recordum.WPF.Bootstrap;
using Recordum.WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

/// <summary>Gate sulla registrazione DI del battito (review Task 4, giro 1): senza questa riga in
/// <c>ServiceRegistrar</c>, sugli anelli 2/3 il parametro <c>ITickSourceFactory?</c> è opzionale con
/// default <c>null</c>, quindi Home e i quattro orologi risolverebbero comunque — senza eccezioni,
/// senza test rosso altrove, senza errore di compilazione — e semplicemente non ticcherebbero mai in
/// produzione. L'unica sentinella accidentale è <c>ChartBuilderViewModel</c>, il cui parametro è
/// obbligatorio: solo la sua risoluzione esploderebbe se questa registrazione sparisse. Questo test
/// sorveglia la riga direttamente, per tutti gli altri.</summary>
public sealed class ServiceRegistrarTickSourceTests
{
    [Fact]
    public void Resolves_the_dispatcher_tick_source_factory()
    {
        var services = new ServiceCollection()
            .AddRecordumServices(new RecordumOptions { DatabasePath = ":memory:", KeyFilePath = "k", SaltFilePath = "s", AttachmentsDirectory = "a", BackupsDirectory = "b", AutoBackupKeyPath = "ak" });
        using var sp = services.BuildServiceProvider();

        // Tipo concreto, non solo NotNull: una registrazione sostituita per sbaglio con un'altra
        // ITickSourceFactory (es. NullTickSourceFactory) passerebbe un NotNull ma spegnerebbe ogni
        // orologio in silenzio — esattamente il fallimento che questo test esiste per intercettare.
        Assert.IsType<DispatcherTickSourceFactory>(sp.GetRequiredService<ITickSourceFactory>());
    }
}
