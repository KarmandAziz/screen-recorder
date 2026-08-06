using System.Windows;
using System.Windows.Interop;

namespace LocalScreenRecorder.App.Utilities;

internal static class WindowCaptureExclusion
{
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != nint.Zero && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WdaExcludeFromCapture);
        }
    }

    public static void MakeClickThrough(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle,
            new nint(style | NativeMethods.WsExTransparent | NativeMethods.WsExToolWindow));
    }
}
