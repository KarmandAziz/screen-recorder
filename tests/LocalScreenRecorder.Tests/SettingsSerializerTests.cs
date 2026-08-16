using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.Tests;

public sealed class SettingsSerializerTests
{
    private readonly SettingsSerializer _serializer = new();

    [Fact]
    public void Serialize_RoundTripsHumanReadableSettings()
    {
        var expected = new AppSettings
        {
            RecordingSource = CaptureSourceKind.CustomArea,
            LastCustomArea = new PixelRect(-400, 25, 1200, 700),
            FrameRate = 60,
            RecordMicrophone = true,
            Hotkeys = new HotkeySettings
            {
                StartStop = new HotkeyGesture(HotkeyModifiers.Alt | HotkeyModifiers.Shift, "F8")
            }
        };

        var json = _serializer.Serialize(expected);
        var actual = _serializer.DeserializeOrDefault(json);

        Assert.Contains("\n", json);
        Assert.Equal(expected.RecordingSource, actual.RecordingSource);
        Assert.Equal(expected.LastCustomArea, actual.LastCustomArea);
        Assert.Equal(expected.Hotkeys.StartStop, actual.Hotkeys.StartStop);
    }

    [Fact]
    public void DeserializeOrDefault_RecoversFromCorruptJson()
    {
        var result = _serializer.DeserializeOrDefault("{ definitely not json");

        Assert.Equal(CaptureSourceKind.EntireScreen, result.RecordingSource);
        Assert.Equal(30, result.FrameRate);
    }

    [Fact]
    public void DeserializeOrDefault_NormalizesNewAndOutOfRangeSettings()
    {
        var json = _serializer.Serialize(new AppSettings
        {
            SchemaVersion = 1,
            CountdownSeconds = 17,
            FrameRate = 144,
            SystemAudioVolume = 4,
            MicrophoneVolume = -2,
            LastCustomArea = new PixelRect(0, 0, 3, 4),
            CustomQuality = new CustomQualitySettings
            {
                Width = 10,
                Height = 99_999,
                FrameRate = 0,
                VideoBitrateKbps = 200_000,
                AudioBitrateKbps = 256
            }
        });

        var result = _serializer.DeserializeOrDefault(json);

        Assert.Equal(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Equal(3, result.CountdownSeconds);
        Assert.Equal(30, result.FrameRate);
        Assert.Equal(1, result.SystemAudioVolume);
        Assert.Equal(0, result.MicrophoneVolume);
        Assert.Null(result.LastCustomArea);
        Assert.Equal(160, result.CustomQuality.Width);
        Assert.Equal(16384, result.CustomQuality.Height);
        Assert.Equal(1, result.CustomQuality.FrameRate);
        Assert.Equal(100_000, result.CustomQuality.VideoBitrateKbps);
        Assert.Equal(192, result.CustomQuality.AudioBitrateKbps);
    }

    [Fact]
    public void DeserializeOrDefault_ReplacesInvalidHotkeySet()
    {
        const string json = """
            {
              "Hotkeys": {
                "StartStop": { "Modifiers": "None", "Key": "" },
                "PauseResume": null,
                "SelectArea": null
              }
            }
            """;

        var result = _serializer.DeserializeOrDefault(json);

        Assert.Equal(new HotkeySettings(), result.Hotkeys);
    }
}
