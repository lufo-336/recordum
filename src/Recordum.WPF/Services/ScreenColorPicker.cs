using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Recordum.WPF.Services;

public sealed partial class ScreenColorPicker : IScreenColorPicker
{
    [LibraryImport("user32.dll")] private static partial IntPtr GetDC(IntPtr hWnd);
    [LibraryImport("user32.dll")] private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [LibraryImport("gdi32.dll")]  private static partial uint GetPixel(IntPtr hdc, int x, int y);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }

    public Task<string?> PickAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        var overlay = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true, ShowInTaskbar = false, Cursor = Cursors.Cross,
            Left = SystemParameters.VirtualScreenLeft, Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth, Height = SystemParameters.VirtualScreenHeight,
        };
        void Close(string? result) { overlay.Close(); tcs.TrySetResult(result); }
        overlay.MouseLeftButtonDown += (_, _) =>
        {
            GetCursorPos(out var p);
            var dc = GetDC(IntPtr.Zero);
            try
            {
                var bgr = GetPixel(dc, p.X, p.Y);
                var r = (byte)(bgr & 0xFF); var g = (byte)((bgr >> 8) & 0xFF); var b = (byte)((bgr >> 16) & 0xFF);
                Close($"#{r:x2}{g:x2}{b:x2}");
            }
            finally { _ = ReleaseDC(IntPtr.Zero, dc); }
        };
        overlay.MouseRightButtonDown += (_, _) => Close(null);
        overlay.KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(null); };
        overlay.Show();
        overlay.Activate();
        return tcs.Task;
    }
}
