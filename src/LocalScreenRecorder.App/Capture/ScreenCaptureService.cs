using LocalScreenRecorder.App.Models;
using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;
using ScreenRecorderLib;

namespace LocalScreenRecorder.App.Services;

public sealed class ScreenCaptureService(RegionCoordinateConverter coordinateConverter, IDisplayService displayService) : IScreenCaptureService
{
    public CapturePlan CreatePlan(RecordingRequest request)
    {
        if (request.Displays.Count == 0)
        {
            throw new InvalidOperationException("No active monitor is available for recording.");
        }

        return request.SourceKind switch
        {
            CaptureSourceKind.EntireScreen => CreateEntireDesktopPlan(request.Displays),
            CaptureSourceKind.SelectedMonitor => CreateMonitorPlan(request.SelectedDisplay),
            CaptureSourceKind.CustomArea => CreateRegionPlan(request.SelectedRegion, request.Displays),
            _ => throw new ArgumentOutOfRangeException(nameof(request.SourceKind))
        };
    }

    private CapturePlan CreateEntireDesktopPlan(IReadOnlyList<DisplayInfo> displays)
    {
        var bounds = displayService.GetVirtualBounds(displays);
        var options = new SourceOptions();
        foreach (var display in displays)
        {
            options.RecordingSources.Add(CreateSource(
                display,
                null,
                new PixelRect(
                    display.Bounds.Left - bounds.Left,
                    display.Bounds.Top - bounds.Top,
                    display.Bounds.Width,
                    display.Bounds.Height)));
        }

        return new CapturePlan(options, bounds);
    }

    private static CapturePlan CreateMonitorPlan(DisplayInfo? display)
    {
        if (display is null)
        {
            throw new InvalidOperationException("Select an active monitor before starting the recording.");
        }

        var options = new SourceOptions();
        options.RecordingSources.Add(CreateSource(display, null,
            new PixelRect(0, 0, display.Bounds.Width, display.Bounds.Height)));
        return new CapturePlan(options, display.Bounds);
    }

    private CapturePlan CreateRegionPlan(PixelRect? region, IReadOnlyList<DisplayInfo> displays)
    {
        if (region is null || region.Value.Width < 16 || region.Value.Height < 16)
        {
            throw new InvalidOperationException("Select a custom area of at least 16 × 16 pixels before recording.");
        }

        var slices = coordinateConverter.CreateSlices(region.Value, displays);
        if (slices.Count == 0)
        {
            throw new InvalidOperationException("The selected area no longer overlaps an active monitor. Select the area again.");
        }

        var options = new SourceOptions();
        foreach (var slice in slices)
        {
            options.RecordingSources.Add(CreateSource(slice.Display, slice.SourceRect, slice.DestinationRect));
        }

        return new CapturePlan(options, region.Value);
    }

    private static DisplayRecordingSource CreateSource(DisplayInfo display, PixelRect? crop, PixelRect destination)
    {
        var source = new DisplayRecordingSource(display.DeviceName)
        {
            RecorderApi = RecorderApi.WindowsGraphicsCapture,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = true,
            AnchorPoint = Anchor.TopLeft,
            Position = new ScreenPoint(destination.X, destination.Y),
            OutputSize = new ScreenSize(destination.Width, destination.Height),
            Stretch = StretchMode.Fill
        };
        if (crop is not null)
        {
            source.SourceRect = new ScreenRect(crop.Value.X, crop.Value.Y, crop.Value.Width, crop.Value.Height);
        }
        return source;
    }
}
