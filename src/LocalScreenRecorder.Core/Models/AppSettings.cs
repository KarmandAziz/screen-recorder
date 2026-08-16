namespace LocalScreenRecorder.Core.Models;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public CaptureSourceKind RecordingSource { get; init; } = CaptureSourceKind.EntireScreen;
    public string? LastSelectedMonitor { get; init; }
    public PixelRect? LastCustomArea { get; init; }
    public QualityPresetKind QualityPreset { get; init; } = QualityPresetKind.High;
    public int FrameRate { get; init; } = 30;
    public bool RecordSystemAudio { get; init; } = true;
    public bool RecordMicrophone { get; init; }
    public string? SelectedMicrophone { get; init; }
    public double SystemAudioVolume { get; init; } = 0.75;
    public double MicrophoneVolume { get; init; } = 0.75;
    public string OutputFolder { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Screen Recordings");
    public string? LastSavedPath { get; init; }
    public HotkeySettings Hotkeys { get; init; } = new();
    public bool MinimizeWhenRecording { get; init; } = true;
    public bool ShowRecordingIndicator { get; init; } = true;
    public bool ShowNotificationAfterRecording { get; init; } = true;
    public bool StartMinimized { get; init; }
    public bool IncludeCursor { get; init; } = true;
    public int CountdownSeconds { get; init; } = 3;
    public CustomQualitySettings CustomQuality { get; init; } = new();
}
