using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.UI.Controls;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Settings;
using Xunit;

namespace Recordum.UI.Tests.Settings;

/// <summary>Impostazioni › Aspetto (Task 10 dell'onda Giornata): il tema e le righe della Home, ognuna con la sua riga d'esempio.</summary>
public class AppearancePageTests
{
    /// <summary>Un tema finto: il vero cambierebbe la variante dell'intera app, e l'assembly gira in serie.</summary>
    private sealed class RecordingTheme : IThemeService
    {
        public readonly List<ThemeMode> Calls = [];
        public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;
        public ThemeMode EffectiveTheme => ThemeMode.Light;
#pragma warning disable CS0067 // l'evento fa parte del contratto, il finto non lo alza
        public event EventHandler<ThemeMode>? EffectiveThemeChanged;
#pragma warning restore CS0067
        public ValueTask InitializeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask SetModeAsync(ThemeMode mode, CancellationToken ct = default)
        {
            Calls.Add(mode);
            CurrentMode = mode;
            return ValueTask.CompletedTask;
        }
    }

    private static PrivateDbHost NewHost(string name, RecordingTheme theme) =>
        new(name, s => s.AddSingleton<IThemeService>(theme));

    private static async Task<(SettingsWindow Window, Task Shown)> OpenAsync(
        PrivateDbHost host, SettingsCategoryId? category = null, ThemeVariant? variant = null)
    {
        var launcher = new AvaloniaSettingsWindowLauncher(host.Provider, () => null);
        SettingsWindow? created = null;
        launcher.WindowCreatedForTests += w =>
        {
            created = (SettingsWindow)w;
            if (variant is not null) w.RequestedThemeVariant = variant;
        };
        var shown = launcher.ShowAsync(category);
        for (var i = 0; i < 200 && created is not { IsVisible: true }; i++) { await Task.Delay(25); PrivateDbHost.Pump(); }
        created.Should().NotBeNull();
        PrivateDbHost.Pump();
        return (created!, shown);
    }

    private static async Task CloseAsync(SettingsWindow window, Task shown)
    {
        window.Close();
        await shown;
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static HeatmapStrip Strip(RowPreview preview) => preview.GetVisualDescendants().OfType<HeatmapStrip>().Single();

    [AvaloniaFact]
    public async Task Appearance_page_shows_three_choices_each_with_its_own_row()
    {
        using var culture = TestCulture.Pin();
        var (window, shown) = await OpenAsync(NewHost("app-rows", new RecordingTheme()));

        var compact = Find<RowPreview>(window, "PreviewCompact");
        var marks = Find<RowPreview>(window, "PreviewMarks");
        var comfortable = Find<RowPreview>(window, "PreviewComfortable");
        foreach (var p in new[] { compact, marks, comfortable })
        {
            p.IsVisible.Should().BeTrue();
            p.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "Lettura", "la riga d'esempio è una lettura");
        }

        // Le altezze della tabella: Larga 36, Compatta e Segni 30 (più il bordo della scheda).
        Find<Grid>(comfortable, "HomeRowGrid").Bounds.Height.Should().Be(36);
        Find<Grid>(compact, "HomeRowGrid").Bounds.Height.Should().Be(30);
        Find<Grid>(marks, "HomeRowGrid").Bounds.Height.Should().Be(30);

        // Compatta scrive i valori dei sette giorni; Segni disegna sette quadratini senza testo, oggi col contorno.
        Strip(compact).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Should().Equal("20", "·", "30", "25", "·", "40", "35");
        var marksStrip = Strip(marks);
        marksStrip.GetVisualDescendants().OfType<TextBlock>().Should().BeEmpty("in Segni la striscia non scrive");
        marksStrip.GetVisualDescendants().OfType<Border>().Count(b => b.Bounds.Width == 14 && b.Bounds.Height == 14).Should().Be(7);
        marksStrip.GetVisualDescendants().OfType<Border>().Should().ContainSingle(b => b.Name == HeatmapStrip.SelectedCellName);

        // La predefinita del computer è Larga, ed è quella segnata.
        Find<RadioButton>(window, "RowsComfortable").IsChecked.Should().BeTrue();
        Find<RadioButton>(window, "RowsCompact").IsChecked.Should().BeFalse();
        Find<RadioButton>(window, "RowsMarks").IsChecked.Should().BeFalse();
        await CloseAsync(window, shown);
    }

    /// <summary>GIO-19: senza l'intestazione dei giorni Compatta e Larga erano la stessa riga. Ogni esempio ha la sua
    /// testa (iniziali in Compatta e Segni, i nomi e "oggi" in Larga), sopra le colonne della striscia.</summary>
    [AvaloniaFact]
    public async Task Each_choice_shows_its_day_header_over_the_strip()
    {
        using var culture = TestCulture.Pin();
        var (window, shown) = await OpenAsync(NewHost("app-header", new RecordingTheme()));

        foreach (var (name, initials) in new[] { ("Compact", true), ("Marks", true), ("Comfortable", false) })
        {
            var header = Find<Recordum.UI.Views.Home.DayHeaderView>(window, "Header" + name);
            var days = header.GetVisualDescendants().OfType<Button>().ToList();
            days.Should().HaveCount(7, name);
            if (initials) days.Should().OnlyContain(b => ((string)b.Content!).Length == 1, name + " scrive le iniziali");
            else
            {
                days[^1].Content.Should().Be("oggi");
                days.Take(6).Should().OnlyContain(b => ((string)b.Content!).Length == 3, "Larga scrive i nomi");
            }
            days[^1].Classes.Should().Contain("selected");

            var strip = Strip(Find<RowPreview>(window, "Preview" + name));
            var cells = name == "Marks"
                ? strip.GetVisualDescendants().OfType<Border>().Where(b => b.Bounds.Width == 14 && b.Bounds.Height == 14).Cast<Control>().ToList()
                : strip.GetVisualDescendants().OfType<TextBlock>().Cast<Control>().ToList();
            cells.Should().HaveCount(7);
            for (var i = 0; i < 7; i++)
            {
                var day = days[i].TranslatePoint(new Avalonia.Point(days[i].Bounds.Width / 2, 0), window)!.Value.X;
                var cell = cells[i].TranslatePoint(new Avalonia.Point(cells[i].Bounds.Width / 2, 0), window)!.Value.X;
                day.Should().BeApproximately(cell, 1.0, $"{name}: il giorno {i} sta sopra la sua colonna");
            }
        }
        await CloseAsync(window, shown);
    }

    [AvaloniaFact]
    public async Task Picking_marks_updates_the_preference()
    {
        var host = NewHost("app-pick", new RecordingTheme());
        var rows = host.Provider.GetRequiredService<HomeRowsPreference>();
        var changed = 0;
        rows.Changed += (_, _) => changed++;
        var (window, shown) = await OpenAsync(host);

        Find<RadioButton>(window, "RowsMarks").IsChecked = true;
        PrivateDbHost.Pump();

        rows.Current.Should().Be(HomeRows.Marks);
        changed.Should().Be(1);
        Find<RadioButton>(window, "RowsComfortable").IsChecked.Should().BeFalse();
        using (var scope = host.Provider.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IAppSettingService>().GetAsync("home.rows")).Should().Be("Marks");
        await CloseAsync(window, shown);
    }

    [AvaloniaFact]
    public async Task Settings_open_on_appearance_and_backup_deep_link_still_works()
    {
        var host = NewHost("app-deeplink", new RecordingTheme());

        var (window, shown) = await OpenAsync(host);
        Find<StackPanel>(window, "PageTabs").IsVisible.Should().BeTrue();
        Find<AppearancePage>(window, "AppearancePage").IsVisible.Should().BeTrue("senza categoria si apre Aspetto");
        Find<DataAndBackupPage>(window, "Page").IsVisible.Should().BeFalse();

        // Dal segmento si passa all'altra pagina.
        Find<RadioButton>(window, "DataTab").IsChecked = true;
        PrivateDbHost.Pump();
        Find<AppearancePage>(window, "AppearancePage").IsVisible.Should().BeFalse();
        Find<DataAndBackupPage>(window, "Page").IsVisible.Should().BeTrue();
        await CloseAsync(window, shown);

        var (backup, backupShown) = await OpenAsync(host, SettingsCategoryId.Backup);
        Find<DataAndBackupPage>(backup, "Page").IsVisible.Should().BeTrue("il rimando al backup apre Dati e backup");
        Find<AppearancePage>(backup, "AppearancePage").IsVisible.Should().BeFalse();
        Find<RadioButton>(backup, "DataTab").IsChecked.Should().BeTrue();
        await CloseAsync(backup, backupShown);
    }

    [AvaloniaFact]
    public async Task Theme_segments_change_the_theme()
    {
        var theme = new RecordingTheme();
        var (window, shown) = await OpenAsync(NewHost("app-theme", theme));
        Find<RadioButton>(window, "ThemeSystem").IsChecked.Should().BeTrue("il tema corrente è segnato");
        theme.Calls.Should().BeEmpty("aprire la pagina non cambia il tema");

        Find<RadioButton>(window, "ThemeDark").IsChecked = true;
        PrivateDbHost.Pump();

        theme.Calls.Should().Equal(ThemeMode.Dark);
        Find<RadioButton>(window, "ThemeSystem").IsChecked.Should().BeFalse();
        await CloseAsync(window, shown);
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Png_settings_appearance(string variant)
    {
        using var culture = TestCulture.Pin();
        var (window, shown) = await OpenAsync(NewHost($"app-png-{variant}", new RecordingTheme()), null,
            variant == "dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        Find<AppearancePage>(window, "AppearancePage").IsVisible.Should().BeTrue();
        Find<RowPreview>(window, "PreviewMarks").Bounds.Width.Should().BeGreaterThan(0);

        ScreenshotProbeTests.Capture(window, $"settings-appearance-{variant}");
        await CloseAsync(window, shown);
    }
}
