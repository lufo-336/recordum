using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Views;

/// <summary>Sceglie il ramo della regione albero dallo stato della Home. I quattro template si
/// dichiarano in XAML come proprietà: la vista resta leggibile e la logica è un solo switch.
/// <para>Perché non un DataTemplate unico con quattro IsVisible: così viene costruito SOLO il ramo
/// corrente. È la stessa regola che in WPF aveva fatto istanziare un WebView2 fantasma (C1 dell'Onda
/// 2.4): un ContentControl con un ContentTemplate statico materializza comunque il template.</para></summary>
public sealed class HomeStateTemplate : IDataTemplate
{
    public IDataTemplate? Loading { get; set; }
    public IDataTemplate? Loaded { get; set; }
    public IDataTemplate? Empty { get; set; }
    public IDataTemplate? Error { get; set; }

    public bool Match(object? data) => data is HomeState;

    public Control? Build(object? param) => param switch
    {
        HomeState.Loading => Loading?.Build(param),
        HomeState.Loaded => Loaded?.Build(param),
        HomeState.Empty => Empty?.Build(param),
        HomeState.Error => Error?.Build(param),
        _ => null,
    };
}
