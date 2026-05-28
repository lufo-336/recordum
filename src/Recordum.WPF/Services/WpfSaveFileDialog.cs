using System.IO;
using Microsoft.Win32;
using Recordum.Core.Enums;

namespace Recordum.WPF.Services;

public sealed class WpfSaveFileDialog : ISaveFileDialog
{
    public string? Prompt(string suggestedFileName, ExportFormat format)
    {
        var filter = format switch
        {
            ExportFormat.Pdf => "PDF document (*.pdf)|*.pdf",
            ExportFormat.Html => "HTML page (*.html)|*.html",
            ExportFormat.Markdown => "Markdown (*.md)|*.md",
            ExportFormat.Json => "JSON (*.json)|*.json",
            _ => "CSV (*.csv)|*.csv",
        };
        var dlg = new SaveFileDialog { FileName = suggestedFileName, Filter = filter };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public async Task WriteAsync(string path, byte[] bytes, CancellationToken ct = default)
        => await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
}
