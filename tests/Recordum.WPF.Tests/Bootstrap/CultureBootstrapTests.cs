using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FluentAssertions;
using Recordum.WPF.Bootstrap;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

/// <summary>W2V-1 / W2V-17c — the app used three conventions at once: strings from the .resx
/// (CurrentUICulture = it), numbers formatted in C# with CurrentCulture (it: "30,0"), and every XAML
/// binding formatted *and parsed* in en-US, because WPF leaves <c>FrameworkElement.Language</c> at
/// en-US no matter what the thread culture is. Dates read "8/14/2026" inside an Italian app, and a
/// decimal typed as "2,5" was silently refused while "2.5" was accepted.
///
/// <para>These tests assert the observable consequence, not the mechanism: given a culture, a bound
/// date renders in it and a bound decimal parses from it.</para></summary>
public sealed class CultureBootstrapTests
{
    private sealed class DateHolder
    {
        public DateTime Day { get; set; }
    }

    private sealed class AmountHolder : INotifyPropertyChanged
    {
        private decimal? _value;

        public decimal? Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [WpfFact]
    public void A_bound_date_renders_in_the_applied_culture()
    {
        CultureBootstrap.Apply(CultureInfo.GetCultureInfo("it-IT"));

        var block = new TextBlock { DataContext = new DateHolder { Day = new DateTime(2026, 8, 14) } };
        block.SetBinding(TextBlock.TextProperty, new Binding(nameof(DateHolder.Day)) { StringFormat = "d" });

        block.Text.Should().Be("14/08/2026");
    }

    [WpfFact]
    public void A_bound_decimal_parses_the_applied_culture_separator()
    {
        CultureBootstrap.Apply(CultureInfo.GetCultureInfo("it-IT"));

        var holder = new AmountHolder();
        var box = new TextBox { DataContext = holder };
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(AmountHolder.Value))
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        box.Text = "2,5";

        holder.Value.Should().Be(2.5m);
    }

    // XmlLanguage normalizes the tag to lower case; the assertion states that, rather than pretending
    // the round-trip is case-preserving.
    [Theory]
    [InlineData("it-IT", "it-it")]
    [InlineData("en-US", "en-us")]
    public void The_visual_tree_language_follows_the_culture(string culture, string expectedTag)
    {
        CultureBootstrap.LanguageFor(CultureInfo.GetCultureInfo(culture))
            .IetfLanguageTag.Should().Be(expectedTag);
    }
}
