using System.Globalization;
using FluentAssertions;
using Recordum.WPF.Converters;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

public class ColorEqualsConverterTests
{
    private static readonly ColorEqualsConverter Sut = new();
    private static object? Conv(object? a, object? b) =>
        Sut.Convert(new[] { a, b }, typeof(bool), null, CultureInfo.InvariantCulture);

    [Fact] public void Equal_ignoring_case() => Conv("#3B82F6", "#3b82f6").Should().Be(true);
    [Fact] public void Not_equal() => Conv("#3b82f6", "#10b981").Should().Be(false);
    [Fact] public void Null_is_false() => Conv(null, "#3b82f6").Should().Be(false);
}
