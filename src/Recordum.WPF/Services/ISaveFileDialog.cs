using Recordum.Core.Enums;

namespace Recordum.WPF.Services;

/// <summary>Abstracts the OS save-file dialog + file write so the export VM is unit-testable.</summary>
public interface ISaveFileDialog
{
    /// <summary>Returns the chosen path, or null if the user cancelled.</summary>
    string? Prompt(string suggestedFileName, ExportFormat format);

    Task WriteAsync(string path, byte[] bytes, CancellationToken ct = default);
}
