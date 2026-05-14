namespace Recordum.Plugins.Contracts.ExtensionPoints;

public readonly record struct ExtensionPointId(string Value)
{
    public override string ToString() => Value;
}
