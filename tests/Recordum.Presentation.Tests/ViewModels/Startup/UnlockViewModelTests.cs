using System.Globalization;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Startup;

namespace Recordum.Presentation.Tests.ViewModels.Startup;

[Collection(ProcessGlobalCultureCollection.Name)]
public class UnlockViewModelTests
{
    private static readonly IDbKeyProvider Provider = new FixedKeyProvider(new byte[KeyMaterial.LengthBytes]);

    private static (UnlockViewModel Vm, FakeDbUnlockService Service) Build(DbUnlockState state)
    {
        var service = new FakeDbUnlockService { State = state };
        return (new UnlockViewModel(service, state, canRememberOnDevice: true, storedKeyRejected: false), service);
    }

    [Theory]
    [InlineData(DbUnlockState.NeedsCreation, true)]
    [InlineData(DbUnlockState.NeedsPassphrase, false)]
    [InlineData(DbUnlockState.UsePlatformKey, false)]
    [InlineData(DbUnlockState.Unsupported, false)]
    public void Only_creation_asks_for_a_confirmation(DbUnlockState state, bool expected)
    {
        var (vm, _) = Build(state);

        Assert.Equal(expected, vm.RequiresConfirmation);
    }

    [Fact]
    public void Creation_and_entry_show_different_headings_and_only_creation_shows_the_hint()
    {
        var (create, _) = Build(DbUnlockState.NeedsCreation);
        var (enter, _) = Build(DbUnlockState.NeedsPassphrase);

        Assert.Equal(Strings.Unlock_CreateHeading, create.Heading);
        Assert.Equal(Strings.Unlock_CreateHint, create.Hint);
        Assert.Equal(Strings.Unlock_EnterHeading, enter.Heading);
        Assert.Null(enter.Hint);
        Assert.Equal(Strings.Unlock_Title, create.Title);
    }

    [Fact]
    public async Task A_mismatched_confirmation_is_refused_before_the_service_is_asked()
    {
        var (vm, service) = Build(DbUnlockState.NeedsCreation);
        vm.Passphrase = "correct horse battery";
        vm.Confirmation = "correct horse batterX";

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Unlock_Mismatch, vm.ErrorMessage);
        Assert.Empty(service.Calls);                       // niente Argon2 per un errore di battitura
        Assert.False(vm.Result.IsCompleted);
    }

    [Fact]
    public async Task On_entry_the_confirmation_is_not_compared()
    {
        var (vm, service) = Build(DbUnlockState.NeedsPassphrase);
        service.Enqueue(UnlockOutcome.Unlocked, Provider);
        vm.Passphrase = "correct horse battery";
        vm.Confirmation = "something else entirely";

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Single(service.Calls);
        Assert.True(vm.Result.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Success_completes_Result_with_the_provider_and_clears_the_secrets()
    {
        var (vm, service) = Build(DbUnlockState.NeedsCreation);
        service.Enqueue(UnlockOutcome.Unlocked, Provider);
        vm.Passphrase = "correct horse battery";
        vm.Confirmation = "correct horse battery";

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal((DbUnlockState.NeedsCreation, "correct horse battery"), Assert.Single(service.Calls));
        Assert.Same(Provider, await vm.Result);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal("", vm.Passphrase);
        Assert.Equal("", vm.Confirmation);
    }

    [Fact]
    public async Task A_wrong_passphrase_shows_the_error_and_allows_another_attempt()
    {
        var (vm, service) = Build(DbUnlockState.NeedsPassphrase);
        service.Enqueue(UnlockOutcome.WrongPassphrase);
        service.Enqueue(UnlockOutcome.Unlocked, Provider);
        vm.Passphrase = "wrong wrong wrong";

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Unlock_WrongPassphrase, vm.ErrorMessage);
        Assert.False(vm.Result.IsCompleted);
        Assert.Equal("", vm.Passphrase);                   // la sbagliata non resta nel campo
        Assert.True(vm.SubmitCommand.CanExecute(null));    // e si può riprovare

        vm.Passphrase = "right right right";
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);                      // il messaggio vecchio non sopravvive al tentativo nuovo
        Assert.Same(Provider, await vm.Result);
    }

    [Theory]
    [InlineData(UnlockOutcome.PassphraseTooShort, "TooShort")]
    [InlineData(UnlockOutcome.Failed, "Failed")]
    public async Task Each_failure_outcome_maps_to_its_own_message(UnlockOutcome outcome, string key)
    {
        var (vm, service) = Build(DbUnlockState.NeedsPassphrase);
        service.Enqueue(outcome);
        vm.Passphrase = "whatever passphrase";

        await vm.SubmitCommand.ExecuteAsync(null);

        var expected = key == "TooShort" ? Strings.Unlock_TooShort : Strings.Unlock_Failed;
        Assert.Equal(expected, vm.ErrorMessage);
        Assert.False(vm.Result.IsCompleted);
    }

    [Fact]
    public async Task A_service_that_throws_is_a_failure_message_not_a_hung_window()
    {
        var (vm, service) = Build(DbUnlockState.NeedsPassphrase);
        service.Throw = new InvalidOperationException("boom");
        vm.Passphrase = "whatever passphrase";

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Unlock_Failed, vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        Assert.False(vm.Result.IsCompleted);
    }

    [Fact]
    public async Task IsBusy_is_true_while_the_service_runs_and_false_after()
    {
        // false -> true -> false: un test che asserisse solo false -> false non fallirebbe mai se
        // IsBusy non venisse mai impostato (è già successo in questo repo: W24-R3).
        var (vm, service) = Build(DbUnlockState.NeedsPassphrase);
        service.Gate = new TaskCompletionSource();
        service.Enqueue(UnlockOutcome.Unlocked, Provider);
        vm.Passphrase = "whatever passphrase";
        Assert.False(vm.IsBusy);

        var running = vm.SubmitCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.SubmitCommand.CanExecute(null));   // niente doppio invio durante Argon2
        service.Gate.SetResult();
        await running;
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Unsupported_shows_the_explanation_from_the_start_and_never_calls_the_service()
    {
        var (vm, service) = Build(DbUnlockState.Unsupported);

        Assert.True(vm.IsUnsupported);
        Assert.Equal(Strings.Unlock_Unsupported, vm.ErrorMessage);
        Assert.False(vm.SubmitCommand.CanExecute(null));
        await vm.SubmitCommand.ExecuteAsync(null);          // anche forzato: niente
        Assert.Empty(service.Calls);
    }

    [Fact]
    public void Cancel_cancels_Result_so_nobody_awaits_forever()
    {
        var (vm, _) = Build(DbUnlockState.NeedsPassphrase);

        vm.Cancel();

        Assert.True(vm.Result.IsCanceled);
    }

    [Fact]
    public void Every_Unlock_string_exists_in_English_and_Italian()
    {
        Func<string>[] accessors =
        [
            () => Strings.Unlock_Title, () => Strings.Unlock_CreateHeading, () => Strings.Unlock_CreateHint,
            () => Strings.Unlock_EnterHeading, () => Strings.Unlock_PassphraseLabel, () => Strings.Unlock_ConfirmLabel,
            () => Strings.Unlock_Submit, () => Strings.Unlock_WrongPassphrase, () => Strings.Unlock_TooShort,
            () => Strings.Unlock_Mismatch, () => Strings.Unlock_Unsupported, () => Strings.Unlock_Failed,
        ];
        var previous = Strings.Culture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en-US");
            var english = accessors.Select(a => a()).ToArray();
            Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
            var italian = accessors.Select(a => a()).ToArray();

            Assert.All(english, s => Assert.False(string.IsNullOrWhiteSpace(s)));
            Assert.All(italian, s => Assert.False(string.IsNullOrWhiteSpace(s)));
            // "Passphrase" si scrive uguale nelle due lingue; tutto il resto deve differire.
            for (var i = 0; i < english.Length; i++)
                if (english[i] != "Passphrase") Assert.NotEqual(english[i], italian[i]);
        }
        finally { Strings.Culture = previous; }
    }

    private static async Task<RememberChoice> SubmitRememberedAsync(bool canRemember, bool box)
    {
        var service = new FakeDbUnlockService { State = DbUnlockState.NeedsPassphrase };
        service.Enqueue(UnlockOutcome.Unlocked, Provider);
        var vm = new UnlockViewModel(service, DbUnlockState.NeedsPassphrase, canRemember, storedKeyRejected: false)
        {
            Passphrase = "correct horse battery",
            RememberOnDevice = box,
        };
        await vm.SubmitCommand.ExecuteAsync(null);
        return Assert.Single(service.RememberCalls);
    }

    [Fact]
    public void Remember_is_on_by_default_when_the_keyring_is_there_and_off_and_explained_when_it_is_not()
    {
        var with = new UnlockViewModel(new FakeDbUnlockService(), DbUnlockState.NeedsPassphrase, canRememberOnDevice: true, storedKeyRejected: false);
        var without = new UnlockViewModel(new FakeDbUnlockService(), DbUnlockState.NeedsPassphrase, canRememberOnDevice: false, storedKeyRejected: false);

        Assert.True(with.RememberOnDevice);
        Assert.True(with.CanRemember);
        Assert.False(with.ShowRememberUnavailable);
        Assert.False(without.RememberOnDevice);
        Assert.False(without.CanRemember);
        Assert.True(without.ShowRememberUnavailable);
    }

    [Theory]
    [InlineData(true, true, RememberChoice.Remember)]
    [InlineData(true, false, RememberChoice.Forget)]     // il parametro muto: la casella attiva e tolta cancella
    [InlineData(false, true, RememberChoice.LeaveAsIs)]  // casella non attiva: il portachiavi non si tocca
    [InlineData(false, false, RememberChoice.LeaveAsIs)]
    public async Task The_checkbox_decides_what_the_service_is_asked(bool canRemember, bool box, RememberChoice expected) =>
        Assert.Equal(expected, await SubmitRememberedAsync(canRemember, box));

    [Fact]
    public void A_rejected_stored_key_is_flagged_for_the_view()
    {
        var vm = new UnlockViewModel(new FakeDbUnlockService(), DbUnlockState.NeedsPassphrase, canRememberOnDevice: true, storedKeyRejected: true);

        Assert.True(vm.StoredKeyRejected);
        Assert.Equal(Strings.Unlock_StoredKeyRejected, vm.StoredKeyRejectedMessage);
    }
}
