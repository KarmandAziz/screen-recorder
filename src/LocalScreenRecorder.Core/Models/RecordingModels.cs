namespace LocalScreenRecorder.Core.Models;

public enum CaptureSourceKind
{
    EntireScreen,
    SelectedMonitor,
    CustomArea
}

public enum RecordingState
{
    Ready,
    Starting,
    Recording,
    Paused,
    Saving,
    Saved,
    Error
}

public enum QualityPresetKind
{
    Low,
    Medium,
    High,
    VeryHigh,
    Custom
}

public sealed record CustomQualitySettings
{
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public int FrameRate { get; init; } = 30;
    public int VideoBitrateKbps { get; init; } = 8_000;
    public int AudioBitrateKbps { get; init; } = 192;
    public bool UseHardwareEncoding { get; init; } = true;
}

public sealed record EncodingSettings(
    int Width,
    int Height,
    int FrameRate,
    int VideoBitrateKbps,
    int AudioBitrateKbps,
    bool UseHardwareEncoding);

public sealed record RecordingPaths(string FinalPath, string TemporaryPath);

public sealed record CaptureSlice(DisplayInfo Display, PixelRect SourceRect, PixelRect DestinationRect);
