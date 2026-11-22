namespace Recordum.Data.Sync;

public enum SyncFileError
{
    NotASyncFile,
    UnsupportedEnvelope,
    CannotOpen, // frase sbagliata o file incompleto: GCM non li distingue
}

public sealed class SyncFileException(
    SyncFileError error,
    string? message = null,
    Exception? innerException = null)
    : Exception(message ?? error.ToString(), innerException)
{
    public SyncFileError Error { get; } = error;
}
