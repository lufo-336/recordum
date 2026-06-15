using System.Runtime.InteropServices;
using System.Windows;

namespace Recordum.WPF.Interop;

/// <summary>Win32 interop usato dal drag ghost. Singolo scopo: posizione reale del cursore.</summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>Posizione corrente del cursore in pixel fisici dello schermo virtuale, o null se la
    /// chiamata fallisce. <see cref="QueryContinueDragEventArgs"/> non porta la posizione del mouse,
    /// quindi il ghost la legge da qui.</summary>
    public static Point? TryGetCursorPos()
        => GetCursorPos(out var p) ? new Point(p.X, p.Y) : null;
}
