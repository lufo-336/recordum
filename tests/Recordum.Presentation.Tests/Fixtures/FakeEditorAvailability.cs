using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>Portable test double for <see cref="IEditorAvailability"/>: what the ViewModels see of the
/// markdown editor. Defaults to available; pass <c>available: false</c> to simulate a missing runtime.
/// The Windows-only <c>FakeEditorAvailability</c> (adds the WebView2 environment handle) lives in
/// Recordum.WPF.Tests, next to the views that need it.</summary>
public sealed class FakeEditorAvailability(bool available = true) : IEditorAvailability
{
    public bool IsAvailable { get; } = available;
    public Task ProbeAsync() => Task.CompletedTask;
}
