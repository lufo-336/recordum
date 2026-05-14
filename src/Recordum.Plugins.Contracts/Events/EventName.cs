namespace Recordum.Plugins.Contracts.Events;

public readonly record struct EventName(string Value)
{
    public override string ToString() => Value;
}
