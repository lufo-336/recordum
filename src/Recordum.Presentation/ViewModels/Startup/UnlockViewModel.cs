using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.Startup;

/// <summary>La schermata che sta davanti all'app: chiede (o crea) la passphrase del database.
/// Non conosce Argon2 né SQLCipher — <see cref="IDbUnlockService"/> lo fa — e non sa in che finestra
/// vive: chi la mostra aspetta <see cref="Result"/>, che si completa solo quando lo sblocco riesce
/// (o viene annullato con <see cref="Cancel"/>).
/// <para>I testi sono proprietà del ViewModel, non letti dalla vista: la vista si lega e basta.
/// Sono letti alla costruzione. Su Avalonia la finestra di sblocco si mostra nella cultura del sistema operativo: la
/// lingua scelta dall'utente vive nel database cifrato, che a quel punto e' ancora chiuso.</para></summary>
public sealed partial class UnlockViewModel : ObservableObject
{
    private readonly IDbUnlockService _service;
    private readonly DbUnlockState _state;
    private readonly TaskCompletionSource<IDbKeyProvider> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public UnlockViewModel(IDbUnlockService service, DbUnlockState state)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        _state = state;
        if (state == DbUnlockState.Unsupported) _errorMessage = Strings.Unlock_Unsupported;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _passphrase = "";

    [ObservableProperty]
    private string _confirmation = "";

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Solo la creazione chiede di ripetere la passphrase: chi la sta scegliendo, e non ne
    /// esiste un recupero, non deve poterla sbagliare a digitare.</summary>
    public bool RequiresConfirmation => _state == DbUnlockState.NeedsCreation;

    /// <summary>Un database Windows su un sistema senza DPAPI: nessuna passphrase lo apre, il modulo
    /// è inerte e resta il messaggio.</summary>
    public bool IsUnsupported => _state == DbUnlockState.Unsupported;

#pragma warning disable CA1822 // proprietà d'istanza apposta: la vista si lega a un'istanza, non a un membro statico
    public string Title => Strings.Unlock_Title;

    public string Heading => RequiresConfirmation ? Strings.Unlock_CreateHeading : Strings.Unlock_EnterHeading;

    /// <summary>L'avvertenza "non si può recuperare": solo alla creazione, quando la si può ancora leggere.</summary>
    public string? Hint => RequiresConfirmation ? Strings.Unlock_CreateHint : null;

    public string PassphraseLabel => Strings.Unlock_PassphraseLabel;

    public string ConfirmLabel => Strings.Unlock_ConfirmLabel;

    public string SubmitLabel => Strings.Unlock_Submit;
#pragma warning restore CA1822

    /// <summary>Si completa col provider quando lo sblocco riesce; è annullato da <see cref="Cancel"/>.</summary>
    public Task<IDbKeyProvider> Result => _result.Task;

    /// <summary>La finestra è stata chiusa senza sbloccare: chi aspetta <see cref="Result"/> non resti appeso.</summary>
    public void Cancel() => _result.TrySetCanceled();

    private bool CanSubmit() => !IsBusy && !IsUnsupported && !_result.Task.IsCompleted;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        // AsyncRelayCommand.ExecuteAsync NON consulta CanExecute (solo Execute lo fa, via la UI): il
        // pulsante grigio da solo non basta, e il modulo "non supportato" chiamerebbe il servizio.
        if (!CanSubmit()) return;
        ErrorMessage = null;

        if (RequiresConfirmation && !string.Equals(Passphrase, Confirmation, StringComparison.Ordinal))
        {
            ErrorMessage = Strings.Unlock_Mismatch;   // prima del servizio: nessun Argon2 per un refuso
            return;
        }

        IsBusy = true;
        try
        {
            var outcome = await _service.UnlockAsync(_state, Passphrase);
            switch (outcome.Outcome)
            {
                case UnlockOutcome.Unlocked when outcome.Provider is not null:
                    Passphrase = "";
                    Confirmation = "";
                    _result.TrySetResult(outcome.Provider);
                    break;
                case UnlockOutcome.WrongPassphrase:
                    Passphrase = "";   // quella sbagliata non resta nel campo
                    ErrorMessage = Strings.Unlock_WrongPassphrase;
                    break;
                case UnlockOutcome.PassphraseTooShort:
                    ErrorMessage = Strings.Unlock_TooShort;
                    break;
                default:
                    ErrorMessage = Strings.Unlock_Failed;
                    break;
            }
        }
#pragma warning disable CA1031 // un servizio che lancia non deve lasciare la finestra appesa a "occupato"
        catch (Exception)
#pragma warning restore CA1031
        {
            ErrorMessage = Strings.Unlock_Failed;
        }
        finally
        {
            IsBusy = false;
            SubmitCommand.NotifyCanExecuteChanged();   // dopo un successo, CanSubmit diventa false
        }
    }
}
