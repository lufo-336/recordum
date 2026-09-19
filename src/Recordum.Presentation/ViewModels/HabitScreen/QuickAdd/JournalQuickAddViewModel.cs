using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>Journal QuickAdd: writing the note IS what completes the habit. Routes through
/// IDiaryService so the Note(Diario)+Completion pair is created together (spec §6); never touches
/// Completion.Notes directly.</summary>
public sealed partial class JournalQuickAddViewModel(ICompletionService completions, IClock clock, IDiaryService diary)
    : QuickAddViewModelBase(completions, clock)
{
    [ObservableProperty] private string? _notes;

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        if (string.IsNullOrWhiteSpace(Notes))
        {
            ErrorMessage = Strings.QuickAdd_Error_NotesRequired;
            return;
        }
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var day = DateOnly.FromDateTime(CompletionDate.Date);
            await diary.SaveEntryAsync(Node.Id, day, Notes!, ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
