using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenColumnWidthsTests
{
    [Fact]
    public void Widths_default_to_per_column_values()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out _);

        sut.CronologiaWidth.Should().Be(460);
        sut.DettaglioWidth.Should().Be(520);
        sut.NoteWidth.Should().Be(460);
        sut.ConfiguraWidth.Should().Be(480);
    }

    [Fact]
    public void Width_setter_clamps_to_min_and_max()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out _);

        sut.CronologiaWidth = 100;
        sut.CronologiaWidth.Should().Be(HabitScreenViewModel.MinColumnWidth);

        sut.CronologiaWidth = 5000;
        sut.CronologiaWidth.Should().Be(HabitScreenViewModel.MaxColumnWidth);
    }

    [Fact]
    public async Task Persist_writes_csv_and_roundtrips_on_restore()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        sut.SetColumnWidth(HabitColumn.Note, 600);
        await sut.PersistColumnWidthsAsync();

        (await settings.GetAsync(HabitScreenViewModel.ColumnWidthsSettingKey))
            .Should().Be("Cronologia=460,Dettaglio=520,Note=600,Configura=480,Panoramica=460");

        var (sut2, _) = HabitScreenViewModelFactory.CreateOver(settings);
        await sut2.RestoreOpenColumnsAsync();
        sut2.NoteWidth.Should().Be(600);
        sut2.DettaglioWidth.Should().Be(520);
    }

    [Fact]
    public async Task Restore_ignores_malformed_or_out_of_range_tokens()
    {
        var (_, _) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        await settings.SetAsync(HabitScreenViewModel.ColumnWidthsSettingKey,
            "Cronologia=abc,Bogus=400,Note=9999,Dettaglio=600,Configura");

        var (sut, _) = HabitScreenViewModelFactory.CreateOver(settings);
        await sut.RestoreOpenColumnsAsync();

        sut.CronologiaWidth.Should().Be(460);   // valore non numerico → default
        sut.NoteWidth.Should().Be(460);          // fuori range → default
        sut.DettaglioWidth.Should().Be(600);     // token valido applicato
        sut.ConfiguraWidth.Should().Be(480);     // token senza '=' → default
    }

    [Fact]
    public void Reset_returns_the_single_column_to_its_default()
    {
        var (sut, _) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        sut.SetColumnWidth(HabitColumn.Dettaglio, 700);
        sut.SetColumnWidth(HabitColumn.Note, 700);

        sut.ResetColumnWidth(HabitColumn.Dettaglio);

        sut.DettaglioWidth.Should().Be(520);
        sut.NoteWidth.Should().Be(700);  // le altre restano
    }
}
