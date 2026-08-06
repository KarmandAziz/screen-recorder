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

    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        FrameRate = settings.FrameRate is 15 or 30 or 60 ? settings.FrameRate : 30,
        SystemAudioVolume = Math.Clamp(settings.SystemAudioVolume, 0, 1),
        MicrophoneVolume = Math.Clamp(settings.MicrophoneVolume, 0, 1),
        OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder)
            ? new AppSettings().OutputFolder
            : settings.OutputFolder,
        Hotkeys = settings.Hotkeys ?? new HotkeySettings(),
        CustomQuality = settings.CustomQuality ?? new CustomQualitySettings()
    };
}
