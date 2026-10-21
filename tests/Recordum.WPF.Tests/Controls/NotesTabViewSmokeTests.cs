using System.Windows;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Recordum.WPF.Views.HabitScreen;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.Controls;

/// <summary>Smoke tests for the Notes tab's attachment gallery + lightbox (Task 5, Onda 1b): renders
/// without throwing and reflects VM state in the actual visual tree. Uses
/// <c>FakeWebView2Availability(available: false)</c> so the embedded MarkdownEditorControl takes its
/// plain-TextBox fallback path instead of touching a real WebView2 runtime (same convention as
/// MarkdownEditorControlTests' Show()-ed tests).
///
/// <para>Unlike the other Controls/ smoke tests, NotesTabView is a fully composed View that leans on
/// App.xaml's merged converters/brushes/icons via StaticResource/DynamicResource (EnumToBool,
/// BoolToVisible, Brush.*, Icon.ChevronRight, ...). A bare test host has no Application, so those
/// lookups throw at InitializeComponent() time unless resolved first. <see cref="TestApp"/> is the
/// single process-wide bootstrap for a real, fully-resourced Application (see its doc comment for
/// why a per-call-site null-check isn't safe here).</para></summary>
public sealed class NotesTabViewSmokeTests
{
    private static NotesViewModel CreateVm(out FakeAttachmentService attachments, out FakeOpenFileDialog dialog)
    {
        var notes = new FakeNoteService();
        var bus = new FakeEventBus();
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero));
        attachments = new FakeAttachmentService();
        dialog = new FakeOpenFileDialog();
        var confirm = new FakeConfirmationDialog();
        return new NotesViewModel(
            notes, bus, clock, new FakeWebView2Availability(available: false), attachments, new FakePhotoImporter(), dialog, confirm);
    }

    [WpfFact]
    public async Task Renders_a_thumbnail_per_attachment_when_present()
    {
        TestApp.EnsureLoaded();
        var vm = CreateVm(out _, out var dialog);
        await vm.LoadAsync(Guid.NewGuid(), DayBoundary.Utc);
        await vm.NewCommand.ExecuteAsync(null);
        dialog.MultipleResult = ["C:\\photos\\a.jpg", "C:\\photos\\b.jpg"];
        await vm.AddAttachmentCommand.ExecuteAsync(null);

        var control = new NotesTabView { DataContext = vm };
        var window = new Window { Content = control, Width = 400, Height = 400 };
        window.Show();
        try
        {
            Assert.Equal(2, control.AttachmentGallery.Items.Count);
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public void Renders_with_no_attachments_without_throwing()
    {
        TestApp.EnsureLoaded();
        var vm = CreateVm(out _, out _);

        var control = new NotesTabView { DataContext = vm };
        var window = new Window { Content = control, Width = 400, Height = 400 };
        window.Show();
        try
        {
            Assert.Empty(control.AttachmentGallery.Items);
            Assert.Equal(Visibility.Collapsed, control.Lightbox.Visibility);
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public async Task Opening_the_lightbox_shows_the_overlay()
    {
        TestApp.EnsureLoaded();
        var vm = CreateVm(out _, out var dialog);
        await vm.LoadAsync(Guid.NewGuid(), DayBoundary.Utc);
        await vm.NewCommand.ExecuteAsync(null);
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await vm.AddAttachmentCommand.ExecuteAsync(null);

        var control = new NotesTabView { DataContext = vm };
        var window = new Window { Content = control, Width = 400, Height = 400 };
        window.Show();
        try
        {
            vm.OpenLightboxCommand.Execute(vm.Attachments.Single());

            Assert.Equal(Visibility.Visible, control.Lightbox.Visibility);
        }
        finally { window.Close(); }
    }
}
