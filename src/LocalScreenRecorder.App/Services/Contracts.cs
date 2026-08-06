using LocalScreenRecorder.App.Models;
using LocalScreenRecorder.Core.Models;
using ScreenRecorderLib;

namespace LocalScreenRecorder.App.Services;

public interface ILoggingService
{
    string LogFilePath { get; }
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

public interface ISettingsService
{
    string SettingsPath { get; }
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IDisplayService
{
    IReadOnlyList<DisplayInfo> GetDisplays();
    PixelRect GetVirtualBounds(IReadOnlyList<DisplayInfo>? displays = null);
}

public interface IAudioMixerService
{
    (float SystemVolume, float MicrophoneVolume) Normalize(double systemVolume, double microphoneVolume, bool useSystem, bool useMicrophone);
}

public interface IAudioCaptureService
{
    IReadOnlyList<AudioDeviceInfo> GetMicrophones();
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    AudioOptions CreateOptions(RecordingRequest request, int audioBitrateKbps);
}

public sealed record CapturePlan(SourceOptions SourceOptions, PixelRect SourceBounds);

public interface IScreenCaptureService
{
    CapturePlan CreatePlan(RecordingRequest request);
}

public interface IEncoderService
{
    OutputOptions CreateOutputOptions(EncodingSettings settings);
    VideoEncoderOptions CreateVideoOptions(EncodingSettings settings);
}

public interface IRecordingService : IDisposable
{
    event EventHandler<RecordingStateChangedEventArgs>? StateChanged;
    RecordingState State { get; }
    TimeSpan Elapsed { get; }
    string? LastSavedPath { get; }
    Task StartAsync(RecordingRequest request, CancellationToken cancellationToken = default);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync(CancellationToken cancellationToken = default);
}

public enum HotkeyAction
{
    StartStop,
    PauseResume,
    SelectArea
}

public interface IHotkeyService : IDisposable
{
    event EventHandler<HotkeyAction>? Pressed;
    void Initialize(nint windowHandle);
    bool TryRegister(HotkeySettings settings, out string error);
}

public interface IRegionSelectionService
{
    Task<PixelRect?> SelectAsync(PixelRect? currentRegion = null);
    void ShowSelectionBorder(PixelRect region);
    void HideSelectionBorder();
}

public interface IRecordingIndicatorService
{
    void Show();
    void Hide();
    void SetPaused(bool paused);
}

public interface IFolderPickerService
{
    string? PickFolder(string currentFolder);
}
