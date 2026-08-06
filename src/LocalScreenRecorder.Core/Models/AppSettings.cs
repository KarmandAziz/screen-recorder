namespace LocalScreenRecorder.Core.Models;

public sealed record AppSettings
{
    public CaptureSourceKind RecordingSource { get; init; } = CaptureSourceKind.EntireScreen;
    public string? LastSelectedMonitor { get; init; }
    public PixelRect? LastCustomArea { get; init; }
    public QualityPresetKind QualityPreset { get; init; } = QualityPresetKind.Medium;
    public int FrameRate { get; init; } = 30;
    public bool RecordSystemAudio { get; init; } = true;
    public bool RecordMicrophone { get; init; }
    public string? SelectedMicrophone { get; init; }
    public double SystemAudioVolume { get; init; } = 0.75;
    public double MicrophoneVolume { get; init; } = 0.75;
    public string OutputFolder { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Screen Recordings");
    public HotkeySettings Hotkeys { get; init; } = new();
    public bool MinimizeWhenRecording { get; init; } = true;
    public bool ShowRecordingIndicator { get; init; } = true;
    public CustomQualitySettings CustomQuality { get; init; } = new();
}
