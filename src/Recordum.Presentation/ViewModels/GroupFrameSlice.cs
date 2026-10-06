namespace Recordum.Presentation.ViewModels;

/// <summary>The part of an expanded group's frame that one row of a flat Home list draws: the
/// group's depth and colour, and whether this row opens the frame (the group's own row) or closes
/// it (its last visible descendant).</summary>
public sealed record GroupFrameSlice(int Level, string ColorHex, bool IsTop, bool IsBottom);
