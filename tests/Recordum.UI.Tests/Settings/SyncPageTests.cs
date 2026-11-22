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

        public Task<SyncRunResult> RunAsync(CancellationToken ct) =>
            Task.FromResult(new SyncRunResult(true, true, []));

        public Task<SyncStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new SyncStatus(
                new DateTimeOffset(2026, 3, 15, 11, 30, 0, TimeSpan.Zero),
                [new SyncPeerStatus("Fedora", new DateTimeOffset(2026, 3, 15, 11, 25, 0, TimeSpan.Zero), null)],
                TextConflicts: 1,
                OwnFileBytes: 4096));

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
}
