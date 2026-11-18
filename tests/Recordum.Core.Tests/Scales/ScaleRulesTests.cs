using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Scales;
using Xunit;

namespace Recordum.Core.Tests.Scales;

public class ScaleRulesTests
{
    [Fact] public void Valid_shape() => ScaleRules.ShapeErrors(1, 100, [1, 25]).Should().BeEmpty();
    [Fact] public void Range() => ScaleRules.ShapeErrors(-101, 0, []).Should().Equal("range");
    [Fact] public void Order() => ScaleRules.ShapeErrors(5, 5, []).Should().Equal("order");
    [Fact] public void Outside() => ScaleRules.ShapeErrors(1, 10, [11]).Should().Equal("anchor-outside");
    [Fact] public void Duplicate() => ScaleRules.ShapeErrors(1, 10, [3, 3]).Should().Equal("anchor-duplicate");

    [Fact] public void Narrow_max() => ScaleRules.EditErrors(1, 50, ScalePrecision.Integer, 1, 80, false).Should().Equal("narrow-max");
    [Fact] public void Narrow_min() => ScaleRules.EditErrors(10, 100, ScalePrecision.Integer, 5, 80, false).Should().Equal("narrow-min");
    [Fact] public void Precision() => ScaleRules.EditErrors(1, 100, ScalePrecision.Integer, 1, 80, true).Should().Equal("precision");
    [Fact] public void No_records() => ScaleRules.EditErrors(1, 100, ScalePrecision.Integer, null, null, false).Should().BeEmpty();
    [Fact] public void Decimal_keeps_decimals() => ScaleRules.EditErrors(1, 100, ScalePrecision.Decimal, 1, 80, true).Should().BeEmpty();

    [Fact] public void Normalize_integer() => ScaleRules.Normalize(4.6m, 1, 10, ScalePrecision.Integer).Should().Be(5m);
    [Fact] public void Normalize_decimal() => ScaleRules.Normalize(1.374m, 1, 10, ScalePrecision.Decimal).Should().Be(1.37m);
    [Fact] public void Normalize_clamps() => ScaleRules.Normalize(120, 1, 100, ScalePrecision.Decimal).Should().Be(100m);
}
