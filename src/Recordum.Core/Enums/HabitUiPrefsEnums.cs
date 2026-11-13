namespace Recordum.Core.Enums;

/// <summary>Se la riga della Home ha la striscia. Dal 2026-11-13 (GIO-3) vale solo sì o no: <see cref="Days7"/> resta
/// perché è nei dati salvati, e vale come <see cref="Days14"/>. Vedi <c>HomeRowPresentation.ResolveStrip</c>.</summary>
public enum HomeStrip { None, Days7, Days14 }

/// <summary>Primary journal gesture on the "Today" strip / landing. Null pref = template/type default.</summary>
public enum JournalPrimaryGesture { Diary, QuickNote }
