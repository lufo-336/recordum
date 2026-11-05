using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.WPF.Bootstrap;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

/// <summary>Il parametro opzionale <c>Func&lt;HabitBuilderViewModel&gt;?</c> del selettore dei modelli (3a, task 6) compila anche
/// senza registrazione e spegnerebbe in silenzio "Usa questo modello" via creatore: anche l'host WPF la fornisce.</summary>
public sealed class ServiceRegistrarTemplatePickerTests
{
    [Fact]
    public void Registers_the_builder_factory_for_the_template_picker()
    {
        var services = new ServiceCollection()
            .AddRecordumServices(new RecordumOptions { DatabasePath = ":memory:", KeyFilePath = "k", SaltFilePath = "s", AttachmentsDirectory = "a", BackupsDirectory = "b", AutoBackupKeyPath = "ak", DataDirectory = "d", TempDirectory = "t" });
        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<Func<HabitBuilderViewModel>>());
    }
}
