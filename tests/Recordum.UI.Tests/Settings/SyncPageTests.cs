using Avalonia.Automation;
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
using Recordum.Presentation.ViewModels.Settings;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Settings;
using Xunit;

namespace Recordum.UI.Tests.Settings;

/// <summary>Impostazioni › Unisci (Task 11): 8 voci con spiegazione e stato.</summary>
public class SyncPageTests
{
    private sealed class RecordingTheme : IThemeService
    {
        public ThemeMode CurrentMode => ThemeMode.System;
        public ThemeMode EffectiveTheme => ThemeMode.Light;
#pragma warning disable CS0067 // l'evento fa parte del contratto, il finto non lo alza
        public event EventHandler<ThemeMode>? EffectiveThemeChanged;
#pragma warning restore CS0067
        public ValueTask InitializeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask SetModeAsync(ThemeMode mode, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class FakeSyncService(SyncJoinState joinState) : ISyncService
    {
        public SyncJoinState State { get; set; } = joinState;
        public List<SyncItemToFix> Items { get; set; } = [];
        public List<Guid> AcceptedRepairs { get; } = [];

        public Task<SyncRunResult> RunAsync(CancellationToken ct) =>
            Task.FromResult(new SyncRunResult(true, true, []));

        public Task<SyncStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new SyncStatus(
                new DateTimeOffset(2026, 3, 15, 11, 30, 0, TimeSpan.Zero),
                [new SyncPeerStatus("Fedora", new DateTimeOffset(2026, 3, 15, 11, 25, 0, TimeSpan.Zero), null)],
                ItemsToFix: Items.Count,
                OwnFileBytes: 4096));

        public Task<IReadOnlyList<SyncItemToFix>> ListItemsToFixAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SyncItemToFix>>(Items.ToList());

        public Task AcceptRepairAsync(Guid periodId, CancellationToken ct)
        {
            AcceptedRepairs.Add(periodId);
            Items.RemoveAll(i => i.EntityId == periodId && i.Kind == SyncItemKind.RepairedPeriod);
            return Task.CompletedTask;
        }

        public Task SetPassphraseAsync(string passphrase, CancellationToken ct) => Task.CompletedTask;
        public Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct) => Task.FromResult(State);
        public Task JoinAsync(SyncJoinChoice choice, CancellationToken ct) { State = SyncJoinState.Joined; return Task.CompletedTask; }
        public Task DisconnectAsync(CancellationToken ct) { State = SyncJoinState.NotConfigured; return Task.CompletedTask; }
        public Task ExportPlainAsync(Stream rcsync, Stream zipOut, string passphrase, CancellationToken ct) => Task.CompletedTask;
        public Task ReencryptAsync(Stream zipIn, Stream rcsyncOut, string passphrase, CancellationToken ct) => Task.CompletedTask;
    }

    private static PrivateDbHost NewHost(string name, SyncJoinState joinState = SyncJoinState.NeedsChoice) =>
        new(name, s =>
        {
            s.AddSingleton<IThemeService>(new RecordingTheme());
            s.AddSingleton<ISyncService>(new FakeSyncService(joinState));
        });

    private static async Task<(SettingsWindow Window, Task Shown)> OpenAsync(
        PrivateDbHost host, ThemeVariant? variant = null)
    {
        var launcher = new AvaloniaSettingsWindowLauncher(host.Provider, () => null);
        SettingsWindow? created = null;
        launcher.WindowCreatedForTests += w =>
        {
            created = (SettingsWindow)w;
            if (variant is not null) w.RequestedThemeVariant = variant;
        };
        var shown = launcher.ShowAsync(SettingsCategoryId.Sync);
        for (var i = 0; i < 200 && created is not { IsVisible: true }; i++) { await Task.Delay(25); PrivateDbHost.Pump(); }
        created.Should().NotBeNull();
        PrivateDbHost.Pump();
        return (created!, shown);
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task SyncPageShowsEveryItemWithExplanation()
    {
        using var culture = TestCulture.Pin();
        var (window, shown) = await OpenAsync(NewHost("sync-items", SyncJoinState.NeedsChoice));
        try
        {
            Find<RadioButton>(window, "SyncTab").IsChecked.Should().BeTrue();
            var page = Find<SyncPage>(window, "SyncPage");
            page.IsVisible.Should().BeTrue();

            string[] hintNames =
            [
                "AutoMergeHint",
                "MergeNowHint",
                "IncludePhotosHint",
                "FolderHint",
                "PassphraseHint",
                "PlainHint",
                "DisconnectHint",
                "JoinHint",
            ];
            hintNames.Should().HaveCount(8);
            foreach (var name in hintNames)
            {
                var hint = Find<TextBlock>(page, name);
                hint.IsEffectivelyVisible.Should().BeTrue($"{name} deve essere visibile");
                hint.Text.Should().NotBeNullOrWhiteSpace($"{name} deve spiegare la voce");
            }

            Find<TextBlock>(page, "JoinStartFromOthersHint").Text.Should().NotBeNullOrWhiteSpace();
            Find<TextBlock>(page, "JoinKeepBothHint").Text.Should().NotBeNullOrWhiteSpace();
            Find<TextBlock>(page, "StatusHint").Text.Should().NotBeNullOrWhiteSpace();

            foreach (var passName in new[] { "NewPassphraseBox", "ConfirmPassphraseBox", "PlainPassphraseBox" })
            {
                var box = Find<TextBox>(page, passName);
                box.PasswordChar.Should().Be('●');
                AutomationProperties.GetName(box).Should().NotBeNullOrWhiteSpace();
            }

            var controls = page.GetVisualDescendants()
                .Where(c => c is TextBlock or Button or TextBox or CheckBox)
                .Cast<Control>()
                .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0)
                .ToList();
            controls.Should().NotBeEmpty();
            foreach (var c in controls)
            {
                var origin = c.TranslatePoint(new Point(0, 0), window)!.Value;
                origin.X.Should().BeGreaterThanOrEqualTo(-0.5, $"{c.GetType().Name} {c.Name}");
                (origin.X + c.Bounds.Width).Should().BeLessThanOrEqualTo(640.5, $"{c.GetType().Name} {c.Name}");
            }
        }
        finally
        {
            window.Close();
            await shown;
        }
    }

    [AvaloniaFact]
    public async Task JoinSection_is_hidden_when_already_joined()
    {
        using var culture = TestCulture.Pin();
        var (window, shown) = await OpenAsync(NewHost("sync-joined", SyncJoinState.Joined));
        try
        {
            var page = Find<SyncPage>(window, "SyncPage");
            Find<Border>(page, "JoinSection").IsVisible.Should().BeFalse();
        }
        finally
        {
            window.Close();
            await shown;
        }
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Png_settings_sync(string variant)
    {
        using var culture = TestCulture.Pin();
        var (window, shown) = await OpenAsync(
            NewHost($"sync-png-{variant}", SyncJoinState.NeedsChoice),
            variant == "dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        try
        {
            var page = Find<SyncPage>(window, "SyncPage");
            page.IsVisible.Should().BeTrue();
            Find<TextBlock>(page, "AutoMergeHint").Bounds.Height.Should().BeGreaterThan(0);
            Find<ItemsControl>(page, "StatusList").Items.Count.Should().BeGreaterThan(0);

            ScreenshotProbeTests.Capture(window, $"settings-sync-{variant}");
        }
        finally
        {
            window.Close();
            await shown;
        }
    }

    private sealed class RecordingHabitNavigator : IHabitNavigator
    {
        public List<(Guid HabitNodeId, string Title, DateOnly Day)> Calls { get; } = [];

        public Task OpenHabitAtDayAsync(Guid habitNodeId, string title, DateOnly day)
        {
            Calls.Add((habitNodeId, title, day));
            return Task.CompletedTask;
        }
    }

    private static List<SyncItemToFix> SampleItems() =>
    [
        new SyncItemToFix(
            SyncItemKind.DiaryEntry,
            Guid.NewGuid(),
            new Guid("11111111-0000-0000-0000-000000000001"),
            "Diario della sera",
            "#A855F7",
            new DateOnly(2026, 11, 22),
            "— Telefono · 2026-11-22 22:15 — Giornata intensa ma serena"),
        new SyncItemToFix(
            SyncItemKind.RecordNote,
            Guid.NewGuid(),
            new Guid("11111111-0000-0000-0000-000000000002"),
            "Corsa",
            "#3B82F6",
            new DateOnly(2026, 11, 21),
            "— Laptop · 2026-11-21 08:30 — Ritmo costante fino al ponte"),
        new SyncItemToFix(
            SyncItemKind.RepairedPeriod,
            Guid.NewGuid(),
            new Guid("11111111-0000-0000-0000-000000000003"),
            "Niente fumo",
            "#CC4422",
            new DateOnly(2026, 11, 19),
            string.Empty),
    ];

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task ItemsToFix_list_opens_shows_accept_only_on_period_and_renders_png(string variant)
    {
        using var culture = TestCulture.Pin();
        var sync = new FakeSyncService(SyncJoinState.Joined) { Items = SampleItems() };
        var host = new PrivateDbHost($"sync-items-{variant}", s =>
        {
            s.AddSingleton<IThemeService>(new RecordingTheme());
            s.AddSingleton<ISyncService>(sync);
            s.AddSingleton<IHabitNavigator>(new RecordingHabitNavigator());
        });
        var (window, shown) = await OpenAsync(host, variant == "dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        try
        {
            var page = Find<SyncPage>(window, "SyncPage");
            var section = Find<StackPanel>(page, "ItemsToFixSection");
            section.IsVisible.Should().BeTrue();

            var toggle = Find<Button>(page, "ItemsToFixToggle");
            toggle.Command!.Execute(null);
            PrivateDbHost.Pump();

            var panel = Find<StackPanel>(page, "ItemsToFixPanel");
            panel.IsVisible.Should().BeTrue();

            var list = Find<ItemsControl>(page, "ItemsToFixList");
            list.Items.Count.Should().Be(3);

            // Il bottone "Va bene così" è visibile SOLO sulla riga del periodo riparato.
            var acceptButtons = list.GetVisualDescendants()
                .OfType<Button>()
                .Where(b => b.Name == "AcceptRepairButton" && b.IsEffectivelyVisible)
                .ToList();
            acceptButtons.Should().ContainSingle();
            (acceptButtons[0].DataContext as SyncItemToFixViewModel)!.Kind.Should().Be(SyncItemKind.RepairedPeriod);

            var chevrons = list.GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Path>()
                .Where(p => p.Name == "ItemChevron" && p.IsEffectivelyVisible)
                .ToList();
            chevrons.Should().HaveCount(2);

            foreach (var c in page.GetVisualDescendants().Where(c => c is TextBlock or Button).Cast<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0))
            {
                var origin = c.TranslatePoint(new Point(0, 0), window)!.Value;
                origin.X.Should().BeGreaterThanOrEqualTo(-0.5, $"{c.GetType().Name} {c.Name}");
                (origin.X + c.Bounds.Width).Should().BeLessThanOrEqualTo(640.5, $"{c.GetType().Name} {c.Name}");
            }

            // La sezione sta in fondo alla pagina: senza scorrere il PNG mostrerebbe solo la cima.
            section.BringIntoView();
            PrivateDbHost.Pump();
            ScreenshotProbeTests.Capture(window, $"settings-sync-items-{variant}");
        }
        finally
        {
            window.Close();
            await shown;
        }
    }

    [AvaloniaFact]
    public async Task Tapping_item_closes_settings_window_and_calls_navigator()
    {
        using var culture = TestCulture.Pin();
        var items = SampleItems();
        var sync = new FakeSyncService(SyncJoinState.Joined) { Items = items };
        var nav = new RecordingHabitNavigator();
        var host = new PrivateDbHost("sync-tap-nav", s =>
        {
            s.AddSingleton<IThemeService>(new RecordingTheme());
            s.AddSingleton<ISyncService>(sync);
            s.AddSingleton<IHabitNavigator>(nav);
        });
        var (window, shown) = await OpenAsync(host);
        try
        {
            var page = Find<SyncPage>(window, "SyncPage");
            Find<Button>(page, "ItemsToFixToggle").Command!.Execute(null);
            PrivateDbHost.Pump();

            var list = Find<ItemsControl>(page, "ItemsToFixList");
            var firstOpenButton = list.GetVisualDescendants()
                .OfType<Button>()
                .First(b => b.Name == "OpenItemButton");
            firstOpenButton.Command!.Execute(firstOpenButton.CommandParameter);
            PrivateDbHost.Pump();
            await shown;

            window.IsVisible.Should().BeFalse();
            nav.Calls.Should().ContainSingle().Which.Should().Be((items[0].HabitNodeId, items[0].HabitName, items[0].Day));
        }
        finally
        {
            if (window.IsVisible)
            {
                window.Close();
                await shown;
            }
        }
    }

    [AvaloniaFact]
    public void SyncSettingsViewModel_resolves_from_production_DI()
    {
        using var scope = ShellFixture.Shared.Services.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<SyncSettingsViewModel>();
        vm.Should().NotBeNull();
    }
}
