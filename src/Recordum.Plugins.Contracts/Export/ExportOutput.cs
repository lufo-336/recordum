namespace Recordum.Plugins.Contracts.Export;

/// <summary>What a formatter produces: the document plus the accessory files it refers to (e.g. the images a
/// Markdown export links). <see cref="ExportFile.RelativePath"/> is relative to the document's folder. Contracts
/// stays IO-free: the host writes everything.</summary>
public sealed record ExportOutput(byte[] Document, IReadOnlyList<ExportFile> Files)
{
#pragma warning disable CA1720 // name fixed by the plan: a document with no accessory files
    public static ExportOutput Single(byte[] document) => new(document, []);
#pragma warning restore CA1720
}

public sealed record ExportFile(string RelativePath, byte[] Content);
