using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.Contracts.ExtensionPoints;

/// <summary>
/// Extension registered under <see cref="WellKnownExtensionPoints.ExportFormat"/>.
/// <see cref="Write"/> returns the encoded document bytes (UTF-8 text for MD/JSON/CSV/HTML,
/// binary for PDF); the host owns all file IO. Returning <see cref="byte"/>[] keeps Contracts IO-free.
/// </summary>
public interface IExportFormatter : IExtensionPoint
{
    ExportFormat Format { get; }
    bool CanFormat(ExportFormat format);
    byte[] Write(ExportModel model);
}
