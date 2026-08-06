namespace LocalScreenRecorder.Core.Models;

public sealed record DisplayInfo(
    string DeviceName,
    string FriendlyName,
    PixelRect Bounds,
    bool IsPrimary,
    double DpiScaleX = 1.0,
    double DpiScaleY = 1.0)
{
    public string Label => $"{FriendlyName} — {Bounds.Width} × {Bounds.Height}{(IsPrimary ? " (Primary)" : string.Empty)}";
}
