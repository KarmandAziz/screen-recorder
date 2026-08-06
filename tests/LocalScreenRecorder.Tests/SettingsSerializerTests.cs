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
}
