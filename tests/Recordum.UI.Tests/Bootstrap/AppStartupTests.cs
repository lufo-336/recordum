using FluentAssertions;
using Recordum.Presentation.Bootstrap;
using Recordum.UI.Tests.Fixtures;
using Xunit;

namespace Recordum.UI.Tests.Bootstrap;

public class AppStartupTests
{
    // Le opzioni vere della fixture (cartella temporanea), con la sola IsDefaultLocation scelta dal test:
    // Default() creerebbe la cartella dati vera dello sviluppatore.
    private static RecordumOptions Options(bool isDefaultLocation)
    {
        var o = ShellFixture.Shared.Options;
        return new RecordumOptions
        {
            DataDirectory = o.DataDirectory, IsDefaultLocation = isDefaultLocation, DatabasePath = o.DatabasePath,
            KeyFilePath = o.KeyFilePath, SaltFilePath = o.SaltFilePath, AttachmentsDirectory = o.AttachmentsDirectory,
            BackupsDirectory = o.BackupsDirectory, AutoBackupKeyPath = o.AutoBackupKeyPath, TempDirectory = o.TempDirectory,
        };
    }

    [Theory]
    [InlineData("--seed-demo")]
    [InlineData("--seed-habit")]
    public void A_seed_flag_on_the_real_data_folder_is_refused_before_the_unlock(string flag)
    {
        // F1 review m4: prima si sbloccava (e al primo avvio si creavano passphrase e database veri), poi l'errore.
        App.SeedRefusedOnRealData([flag, "Corsa"], Options(isDefaultLocation: true)).Should().BeTrue();
    }

    [Theory]
    [InlineData("--seed-demo", false)]
    [InlineData("--other", true)]
    public void A_scratch_folder_or_no_seed_flag_is_not_refused(string flag, bool isDefaultLocation)
        => App.SeedRefusedOnRealData([flag], Options(isDefaultLocation)).Should().BeFalse();
}
