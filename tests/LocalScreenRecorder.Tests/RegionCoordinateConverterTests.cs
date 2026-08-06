using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.Tests;

public sealed class RegionCoordinateConverterTests
{
    private readonly RegionCoordinateConverter _converter = new();

    [Fact]
    public void FromLogicalRect_UsesDpiScaleAndNegativeVirtualOrigin()
    {
        var result = _converter.FromLogicalRect(100, 50, 800, 600, 1.5, 1.25, -1920, -200);

        Assert.Equal(new PixelRect(-1770, -138, 1200, 750), result);
    }

    [Fact]
    public void CreateSlices_MapsCrossMonitorSelectionToSourceAndDestinationCoordinates()
    {
        var left = new DisplayInfo("DISPLAY2", "Left", new PixelRect(-1920, 0, 1920, 1080), false);
        var right = new DisplayInfo("DISPLAY1", "Right", new PixelRect(0, 0, 2560, 1440), true);
        var selection = new PixelRect(-200, 100, 500, 400);

        var slices = _converter.CreateSlices(selection, [left, right]);

        Assert.Equal(2, slices.Count);
        Assert.Equal(new PixelRect(1720, 100, 200, 400), slices[0].SourceRect);
        Assert.Equal(new PixelRect(0, 0, 200, 400), slices[0].DestinationRect);
        Assert.Equal(new PixelRect(0, 100, 300, 400), slices[1].SourceRect);
        Assert.Equal(new PixelRect(200, 0, 300, 400), slices[1].DestinationRect);
    }
}
