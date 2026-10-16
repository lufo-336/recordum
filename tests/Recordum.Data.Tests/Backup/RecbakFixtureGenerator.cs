using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;
using Recordum.Data.Security;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

/// <summary>
/// Writes <c>&lt;os&gt;.recbak</c> into the folder named by RECORDUM_GENERATE_RECBAK. Not a regular test: excluded from
/// both CI jobs and local runs (<c>Category=FixtureGenerator</c>), run only by the dedicated Windows CI step. The
/// source database uses the platform key (DPAPI on Windows), like a real install.
/// </summary>
[Trait("Category", "FixtureGenerator")]
public class RecbakFixtureGenerator
{
    [GeneratorFact]
    public async Task Generate()
    {
        var folder = Environment.GetEnvironmentVariable("RECORDUM_GENERATE_RECBAK");
        Directory.CreateDirectory(folder!);

        IDbKeyProvider? platformKey = null;
        if (OperatingSystem.IsWindows())
            platformKey = new DpapiKeyProvider(Path.Combine(Path.GetTempPath(), $"recbak-gen-{Guid.NewGuid():N}.key"));

        var fixture = await EncryptedDbFixture.SeedAsync(platformKey);
        try
        {
            await RecbakReferenceSeed.WriteAsync(fixture);
            var os = OperatingSystem.IsWindows() ? "windows" : "linux";
            var dest = Path.Combine(folder!, $"{os}.recbak");
            await fixture.NewBackupService().CreateAsync(dest, RecbakReferenceSeed.Passphrase, new BackupOptions(BackupKind.Manual));
            File.Exists(dest).Should().BeTrue();
        }
        finally { await fixture.DisposeAsync(); }
    }
}

/// <summary>A skip (never a green pass) when RECORDUM_GENERATE_RECBAK is not set.</summary>
public sealed class GeneratorFactAttribute : FactAttribute
{
    public GeneratorFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECORDUM_GENERATE_RECBAK")))
            Skip = "Set RECORDUM_GENERATE_RECBAK=<folder> to generate the reference .recbak.";
    }
}
