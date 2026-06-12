namespace Recordum.Core.Enums;

/// <summary>La "natura" di un campo misurato nel costruttore block-first.</summary>
public enum FieldNature
{
    // ritirato: il conteggio è il mattone CountsOccurrences del builder, non un campo (grammatica v2)
    Count,
    Quantity,
    Time,
    Scale,
    Text,
}
