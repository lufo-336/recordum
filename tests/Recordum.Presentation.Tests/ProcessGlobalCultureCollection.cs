namespace Recordum.Presentation.Tests;

/// <summary>Tests that mutate process-global culture state (<c>Strings.Culture</c>,
/// <c>CultureInfo.Default*</c>) join this collection. <c>DisableParallelization</c> makes xunit run it
/// alone — never alongside any other collection — which is what actually protects the ~900 ViewModel
/// tests that read <c>Strings</c>: putting the mutator in a collection of its own would not, because
/// collections normally run in parallel with each other. A test here must restore what it changed
/// in a <c>finally</c>.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessGlobalCultureCollection
{
    public const string Name = "Process-global culture";
}
