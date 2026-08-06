using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.App.Utilities;
using FormsScreen = System.Windows.Forms.Screen;

namespace LocalScreenRecorder.App.Services;

public sealed class DisplayService : IDisplayService
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        return FormsScreen.AllScreens
            .Select((screen, index) =>
            {
                var scale = GetDpiScale(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2);
                return new DisplayInfo(
                    screen.DeviceName,
                    $"Display {index + 1}",
                    new PixelRect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height),
                    screen.Primary,
                    scale.X,
                    scale.Y);
            })
            .OrderByDescending(display => display.IsPrimary)
            .ThenBy(display => display.Bounds.X)
            .ToArray();
    }

    public PixelRect GetVirtualBounds(IReadOnlyList<DisplayInfo>? displays = null)
    {
        displays ??= GetDisplays();
        if (displays.Count == 0) return default;
        var left = displays.Min(display => display.Bounds.Left);
        var top = displays.Min(display => display.Bounds.Top);
        var right = displays.Max(display => display.Bounds.Right);
        var bottom = displays.Max(display => display.Bounds.Bottom);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    private static (double X, double Y) GetDpiScale(int x, int y)
    {
        try
        {
            var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.NativePoint(x, y), 2);
            return NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out var dpiY) == 0
                ? (dpiX / 96d, dpiY / 96d)
                : (1d, 1d);
        }
        catch (DllNotFoundException)
        {
            return (1d, 1d);
        }
    }
}
