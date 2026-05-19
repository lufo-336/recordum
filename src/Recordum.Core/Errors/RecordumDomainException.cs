namespace Recordum.Core.Errors;

/// <summary>
/// Base exception for domain invariant violations in Recordum.
/// </summary>
public abstract class RecordumDomainException : Exception
{
    protected RecordumDomainException(string message) : base(message)
    {
    }

    protected RecordumDomainException(string message, Exception inner) : base(message, inner)
    {
    }
}
