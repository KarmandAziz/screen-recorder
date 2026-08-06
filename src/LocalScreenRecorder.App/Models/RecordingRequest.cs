using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.App.Models;

public sealed record RecordingRequest(
    CaptureSourceKind SourceKind,
    DisplayInfo? SelectedDisplay,
    PixelRect? SelectedRegion,
    IReadOnlyList<DisplayInfo> Displays,
    bool RecordSystemAudio,
    bool RecordMicrophone,
    string? MicrophoneDeviceId,
    double SystemAudioVolume,
    double MicrophoneVolume,
    QualityPresetKind QualityPreset,
    int FrameRate,
    CustomQualitySettings CustomQuality,
    string OutputFolder);

public sealed record RecordingStateChangedEventArgs(RecordingState State, string Message, string? Path = null);
