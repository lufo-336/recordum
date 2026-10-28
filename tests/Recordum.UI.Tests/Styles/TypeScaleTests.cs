using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Recordum.UI.Tests.Styles;

/// <summary>La scala tipografica della direzione visiva (§4.1): cinque gradini, nessun altro.</summary>
public class TypeScaleTests
{
    [AvaloniaTheory]
    [InlineData("FontSize.Small", 12d)]
    [InlineData("FontSize.Label", 13d)]
    [InlineData("FontSize.Body", 14d)]
    [InlineData("FontSize.Heading", 16d)]
    [InlineData("FontSize.Title", 20d)]
    public void The_scale_has_its_five_steps(string key, double size)
    {
        Assert.True(global::Avalonia.Application.Current!.TryFindResource(key, out var value), key);
        Assert.Equal(size, (double)value!);
    }
}
