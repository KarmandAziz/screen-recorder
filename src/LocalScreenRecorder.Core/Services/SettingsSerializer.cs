using System.Text.Json;
using System.Text.Json.Serialization;
using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.Core.Services;

public sealed class SettingsSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, _options);

    public AppSettings DeserializeOrDefault(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<AppSettings>(json, _options) ?? new AppSettings());
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var defaults = new AppSettings();
        var custom = settings.CustomQuality ?? defaults.CustomQuality;
        var area = settings.LastCustomArea is { Width: >= 16, Height: >= 16 }
            ? settings.LastCustomArea
            : null;

        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            RecordingSource = Enum.IsDefined(settings.RecordingSource) ? settings.RecordingSource : defaults.RecordingSource,
            LastSelectedMonitor = NullIfWhiteSpace(settings.LastSelectedMonitor),
            LastCustomArea = area,
            QualityPreset = Enum.IsDefined(settings.QualityPreset) ? settings.QualityPreset : defaults.QualityPreset,
            FrameRate = settings.FrameRate is 15 or 30 or 60 ? settings.FrameRate : defaults.FrameRate,
            SystemAudioVolume = Math.Clamp(settings.SystemAudioVolume, 0, 1),
            MicrophoneVolume = Math.Clamp(settings.MicrophoneVolume, 0, 1),
            SelectedMicrophone = NullIfWhiteSpace(settings.SelectedMicrophone),
            OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder)
                ? defaults.OutputFolder
                : settings.OutputFolder.Trim(),
            LastSavedPath = NullIfWhiteSpace(settings.LastSavedPath),
            Hotkeys = NormalizeHotkeys(settings.Hotkeys),
            CountdownSeconds = settings.CountdownSeconds is 0 or 3 or 5 ? settings.CountdownSeconds : defaults.CountdownSeconds,
            CustomQuality = custom with
            {
                Width = Math.Clamp(custom.Width, 160, 16384),
                Height = Math.Clamp(custom.Height, 90, 16384),
                FrameRate = Math.Clamp(custom.FrameRate, 1, 120),
                VideoBitrateKbps = Math.Clamp(custom.VideoBitrateKbps, 250, 100_000),
                AudioBitrateKbps = QualityPresetService.NormalizeAudioBitrate(custom.AudioBitrateKbps)
            }
        };
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static HotkeySettings NormalizeHotkeys(HotkeySettings? settings)
    {
        var defaults = new HotkeySettings();
        if (settings is null) return defaults;
        var candidate = new HotkeySettings
        {
            StartStop = settings.StartStop ?? defaults.StartStop,
            PauseResume = settings.PauseResume ?? defaults.PauseResume,
            SelectArea = settings.SelectArea ?? defaults.SelectArea
        };
        return new HotkeyValidator().ValidateSet(candidate) is null ? candidate : defaults;
    }
}
