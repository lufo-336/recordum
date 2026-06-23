using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeAppFontService : IAppFontService
{
    public AppFont CurrentFont { get; set; } = AppFont.Serif;
    private readonly List<AppFont> _setFontCalls = new();
    public IReadOnlyList<AppFont> SetFontCalls => _setFontCalls;
    public event EventHandler<AppFont>? FontChanged;

    public ValueTask InitializeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    public ValueTask SetFontAsync(AppFont font, CancellationToken ct = default)
    {
        _setFontCalls.Add(font);
        CurrentFont = font;
        FontChanged?.Invoke(this, font);
        return ValueTask.CompletedTask;
    }
}
