namespace Recordum.Presentation.ViewModels;

/// <summary>Bulk actions over the multi-selection (spec §C.3). A row asks its host to handle
/// an action as bulk; the host answers false when the row isn't part of a multi-selection,
/// and the row falls back to its single-node path.</summary>
public interface IRowSelectionHost
{
    Task<bool> TryBulkAsync(HomeRowViewModel origin, RowBulkAction action);
}

public enum RowBulkAction { Archive, Unarchive, SoftDelete, Restore, Purge }
