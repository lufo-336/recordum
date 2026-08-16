using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FluentAssertions;
using Recordum.WPF.Behaviors;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.Behaviors;

/// <summary>W2V-17c — what a numeric cell does with input WPF cannot read as the number the user
/// meant. Measured before writing this: the default binding conversion allows a *group* separator, so
/// under en-US "2,5" silently became <c>25</c> — and once the app applies an Italian culture the same
/// trap flips onto the point ("2.5" → 25). A tenfold-wrong value written without a word is worse than
/// a refusal, so the rule refuses anything that is not a plain number in the current culture, and the
/// refusal has to be visible.</summary>
public sealed class NumericInputRuleTests
{
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

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    [Theory]
    [InlineData("2,5")]      // the culture's own decimal separator
    [InlineData("2500")]     // a plain integer
    [InlineData("-2,5")]     // signed
    [InlineData("0,125")]    // more than two decimals
    [InlineData("")]         // clearing the field is not an error
    [InlineData("   ")]
    public void Accepts_a_plain_number_in_the_current_culture(string text) =>
        new NumericInputRule().Validate(text, Italian).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("2.5")]      // the OTHER culture's separator: WPF would read 25
    [InlineData("2.500")]    // grouping: WPF would read 2500
    [InlineData("1.234,5")]  // grouped and fractional
    [InlineData("abc")]
    [InlineData("2,5 l")]    // the unit typed into the value
    [InlineData("2,,5")]
    public void Refuses_anything_it_cannot_read_as_that_number(string text) =>
        new NumericInputRule().Validate(text, Italian).IsValid.Should().BeFalse();

    /// <summary>Characterizes what WPF does on its own, which is what makes the rule necessary and
    /// says how far the damage goes: text it cannot convert at all is flagged, but text it converts to
    /// the *wrong* number is not — nothing downstream can tell that "2.5" became 25.</summary>
    [WpfFact]
    public void Wpf_alone_flags_unreadable_text_but_not_a_misread_number()
    {
        CultureBootstrap.Apply(Italian);
        var holder = new AmountHolder { Value = 7m };
        var box = new TextBox { DataContext = holder };
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(AmountHolder.Value))
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        box.Text = "abc";
        Validation.GetHasError(box).Should().BeTrue("an unconvertible string is a conversion error");
        holder.Value.Should().Be(7m);

        box.Text = "2.5";
        Validation.GetHasError(box).Should().BeFalse("the group separator converts cleanly…");
        holder.Value.Should().Be(25m, "…to the wrong number");
    }

    [WpfFact]
    public void A_refused_value_never_reaches_the_source_and_the_field_says_so()
    {
        CultureBootstrap.Apply(Italian);
        var holder = new AmountHolder { Value = 7m };
        var box = new TextBox { DataContext = holder };
        var binding = new Binding(nameof(AmountHolder.Value))
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        };
        binding.ValidationRules.Add(new NumericInputRule());
        box.SetBinding(TextBox.TextProperty, binding);

        box.Text = "2.5";

        holder.Value.Should().Be(7m, "a value WPF would read as 25 must not be written silently");
        Validation.GetHasError(box).Should().BeTrue();
    }

    /// <summary>A refusal nobody can see is the same silence in a different costume, so the shared
    /// TextBox style has to carry the error state. Asserted on the real style from App.xaml.</summary>
    [WpfFact]
    public void The_shared_text_box_style_shows_the_refusal()
    {
        TestApp.EnsureLoaded();
        CultureBootstrap.Apply(Italian);
        var holder = new AmountHolder { Value = 7m };
        var box = new TextBox { DataContext = holder };
        var binding = new Binding(nameof(AmountHolder.Value))
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        };
        binding.ValidationRules.Add(new NumericInputRule());
        box.SetBinding(TextBox.TextProperty, binding);
        // An orphan element never resolves the implicit style: it has to be measured inside a tree
        // before Application.Resources reaches it.
        var root = new Grid { Children = { box } };
        root.Measure(new Size(400, 200));
        var normalBorder = box.BorderBrush;

        box.Text = "2.5";

        box.BorderBrush.Should().NotBeSameAs(normalBorder, "the underline marks the field");
        box.BorderBrush.Should().BeSameAs(Application.Current.TryFindResource("Brush.Danger"));
        box.ToolTip.Should().Be(new NumericInputRule().Validate("2.5", Italian).ErrorContent,
            "the reason has to be readable, not just the colour");
    }
}
