using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.Core.Services;

public sealed class RegionCoordinateConverter
{
    public PixelRect FromLogicalRect(
        double x,
        double y,
        double width,
        double height,
        double scaleX,
        double scaleY,
        int physicalOriginX = 0,
        int physicalOriginY = 0)
    {
        if (scaleX <= 0 || scaleY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleX), "DPI scale values must be positive.");
        }

        return new PixelRect(
            physicalOriginX + (int)Math.Round(x * scaleX),
            physicalOriginY + (int)Math.Round(y * scaleY),
            Math.Max(0, (int)Math.Round(width * scaleX)),
            Math.Max(0, (int)Math.Round(height * scaleY)));
    }

    public IReadOnlyList<CaptureSlice> CreateSlices(PixelRect selection, IEnumerable<DisplayInfo> displays)
    {
        if (selection.IsEmpty)
        {
            return [];
        }

        return displays
            .Select(display => (Display: display, Intersection: selection.Intersect(display.Bounds)))
            .Where(item => !item.Intersection.IsEmpty)
            .Select(item => new CaptureSlice(
                item.Display,
                new PixelRect(
                    item.Intersection.Left - item.Display.Bounds.Left,
                    item.Intersection.Top - item.Display.Bounds.Top,
                    item.Intersection.Width,
                    item.Intersection.Height),
                new PixelRect(
                    item.Intersection.Left - selection.Left,
                    item.Intersection.Top - selection.Top,
                    item.Intersection.Width,
                    item.Intersection.Height)))
            .ToArray();
    }
}
