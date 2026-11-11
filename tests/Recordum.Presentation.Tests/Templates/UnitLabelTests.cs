using System.Globalization;
using FluentAssertions;
using Recordum.Core.Models;
using Recordum.Data.SeedData;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Templates;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.Templates;

[Collection(ProcessGlobalCultureCollection.Name)]
public sealed class UnitLabelTests : IDisposable
{
    private readonly CultureInfo? _strings = Strings.Culture;
    private readonly CultureInfo _current = CultureInfo.CurrentCulture;
    private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;

    public void Dispose()
    {
        Strings.Culture = _strings;
        CultureInfo.CurrentCulture = _current;
        CultureInfo.CurrentUICulture = _ui;
    }

    private static void Pin(string name)
    {
        var c = CultureInfo.GetCultureInfo(name);
        Strings.Culture = c;
        CultureInfo.CurrentCulture = c;
        CultureInfo.CurrentUICulture = c;
    }

    [Fact]
    public void Unit_next_to_a_value_uses_the_symbol_when_there_is_one()
    {
        var hours = UnitOfMeasureSeed.All.Single(u => u.Code == "hours");
        var minutes = UnitOfMeasureSeed.All.Single(u => u.Code == "minutes");

        Pin("it-IT");
        UnitLabel.WithValue("2", UnitLabel.For(hours)).Should().Be("2 h").And.NotBe("2 hours");
        UnitLabel.WithValue("20", UnitLabel.For(minutes)).Should().Be("20 min");

        Pin("en-US");
        UnitLabel.WithValue("2", UnitLabel.For(hours)).Should().Be("2 h").And.NotBe("2 hours");
        UnitLabel.WithValue("20", UnitLabel.For(minutes)).Should().Be("20 min");
    }

    [Fact]
    public void Unit_next_to_a_value_otherwise_the_translated_name()
    {
        var pages = UnitOfMeasureSeed.All.Single(u => u.Code == "pages");
        var hours = UnitOfMeasureSeed.All.Single(u => u.Code == "hours");

        Pin("it-IT");
        UnitLabel.WithValue("12", UnitLabel.For(pages)).Should().Be("12 pagine");
        UnitLabel.WithValue("2", UnitLabel.For(hours)).Should().NotBe("2 hours");

        Pin("en-US");
        UnitLabel.WithValue("12", UnitLabel.For(pages)).Should().Be("12 pages");
        UnitLabel.WithValue("2", UnitLabel.For(hours)).Should().NotBe("2 hours");
    }

    [Fact]
    public void User_unit_without_symbol_uses_its_name()
    {
        var custom = new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Code = "tazze",
            Name = "Tazze",
            Symbol = null,
            IsOfficial = false,
        };

        Pin("en-US");
        UnitLabel.For(custom).Should().Be("Tazze");
        UnitLabel.WithValue("3", UnitLabel.For(custom)).Should().Be("3 Tazze");
    }
}
