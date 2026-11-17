namespace Recordum.Presentation.ViewModels.Home;

/// <summary>Una voce del menu "Tag" della Home (GIO-22): <see cref="Id"/> null = tutti i tag.</summary>
public sealed record HomeTagOption(Guid? Id, string Name, bool IsSelected);
