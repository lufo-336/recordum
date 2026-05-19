namespace Recordum.Core.Abstractions.Security;

public interface IKeyTransitionService
{
    ValueTask TransitionToPassphraseAsync(string newPassphrase, CancellationToken ct = default);

    ValueTask TransitionToDpapiAsync(string currentPassphrase, CancellationToken ct = default);
}
