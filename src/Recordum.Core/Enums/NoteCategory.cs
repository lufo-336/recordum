namespace Recordum.Core.Enums;

/// <summary>How a note is classified. Diario = a journal diary entry (paired 1:1 with a Completion);
/// Veloce = a free quick note with no completion. Foundation for richer note types via templates (Future).</summary>
public enum NoteCategory
{
    // Veloce is 0 so it equals both the CLR default and the column default ('Veloce'): this avoids
    // EF's store-generated-default sentinel, which would otherwise treat an explicit Diario==0 as
    // "unset" and silently persist the column default instead.
    Veloce = 0,
    Diario = 1,
}
