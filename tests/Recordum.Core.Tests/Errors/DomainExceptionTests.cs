using Recordum.Core.Errors;
using Xunit;

namespace Recordum.Core.Tests.Errors;

public class DomainExceptionTests
{
    [Fact]
    public void HabitNodeNotFound_carries_id()
    {
        var id = Guid.NewGuid();
        var ex = new HabitNodeNotFoundException(id);
        Assert.Equal(id, ex.Id);
        Assert.IsAssignableFrom<RecordumDomainException>(ex);
    }

    [Fact]
    public void CyclicMove_carries_ids()
    {
        var id = Guid.NewGuid();
        var newParent = Guid.NewGuid();
        var ex = new CyclicHabitNodeMoveException(id, newParent);
        Assert.Equal(id, ex.Id);
        Assert.Equal(newParent, ex.NewParentId);
    }

    [Fact]
    public void KeyUnavailable_carries_provider_and_reason()
    {
        var ex = new KeyUnavailableException("dpapi", "key file missing");
        Assert.Equal("dpapi", ex.ProviderId);
        Assert.Equal("key file missing", ex.Reason);
    }

    [Fact]
    public void KeyTransitionFailed_preserves_providers_and_inner_exception()
    {
        var inner = new InvalidOperationException("rekey aborted");
        var ex = new KeyTransitionFailedException("dpapi", "passphrase", inner);
        Assert.Equal("dpapi", ex.FromProvider);
        Assert.Equal("passphrase", ex.ToProvider);
        Assert.Same(inner, ex.InnerException);
    }
}
