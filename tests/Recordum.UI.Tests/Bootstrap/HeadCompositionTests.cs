using Avalonia.Headless.XUnit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Home;
using Recordum.UI.Bootstrap;
using Recordum.UI.Tests.Fixtures;
using Xunit;

namespace Recordum.UI.Tests.Bootstrap;

/// <summary>Onda 4, Task 2: cio' che cambia fra computer e telefono lo dice la testa, e la composizione lo ascolta.
/// Ogni caso prova il valore che il default NON darebbe.</summary>
public sealed class HeadCompositionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"recordum-head-{Guid.NewGuid():N}");

    private sealed class FixedKey : IDbKeyProvider
    {
        public string ProviderId => "fixed-test-key";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(new KeyMaterial([.. Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 1))]));
    }

    private ServiceProvider Build(FakeAppHead head) => AppComposition.BuildServices(
        RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, _dir], _ => null), new FixedKey(), new NullSecretStore(), head);

    [AvaloniaFact]
    public void The_head_adds_its_shell_services_once()
    {
        var head = new FakeAppHead();
        using var sp = Build(head);
        head.ShellServicesCalls.Should().Be(1);
    }

    [AvaloniaTheory]
    [InlineData(HomeRows.Compact)]
    [InlineData(HomeRows.Comfortable)]
    public void Home_rows_default_comes_from_the_head(HomeRows rows)
    {
        using var sp = Build(new FakeAppHead { DefaultHomeRows = rows });
        // Prima di InitializeAsync Current e' il ripiego: quello della testa.
        sp.GetRequiredService<HomeRowsPreference>().Current.Should().Be(rows);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dpapi_heads_hide_the_device_key_and_keep_the_auto_backup_passphrase_in_dpapi(bool dpapi)
    {
        using var sp = Build(new FakeAppHead { UsesWindowsDpapi = dpapi });
        sp.GetRequiredService<DataAndBackupPageViewModel>().ShowDeviceKey.Should().Be(!dpapi);
        var store = sp.GetRequiredService<IAutoBackupKeyStore>();
#pragma warning disable CA1416 // solo il tipo, non lo si usa: DPAPI non viene chiamata
        if (dpapi) store.Should().BeOfType<DpapiAutoBackupKeyStore>();
#pragma warning restore CA1416
        else store.Should().BeOfType<SecretStoreAutoBackupKeyStore>();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
