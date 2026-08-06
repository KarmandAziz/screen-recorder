using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.Tests;

public sealed class QualityPresetServiceTests
{
    private readonly QualityPresetService _service = new();

    [Fact]
    public void LowPreset_Scales4KTo720pAndHonors15Fps()
    {
        var result = _service.Resolve(QualityPresetKind.Low, 3840, 2160, 15);

        Assert.Equal(1280, result.Width);
        Assert.Equal(720, result.Height);
        Assert.Equal(15, result.FrameRate);
        Assert.Equal(2_000, result.VideoBitrateKbps);
        Assert.Equal(96, result.AudioBitrateKbps);
    }

    [Fact]
    public void MediumPreset_MaintainsAspectRatioAndEvenDimensions()
    {
        var result = _service.Resolve(QualityPresetKind.Medium, 3440, 1440, 60);

        Assert.True(result.Width <= 1920);
        Assert.True(result.Height <= 1080);
        Assert.Equal(0, result.Width % 2);
        Assert.Equal(0, result.Height % 2);
        Assert.InRange(result.Width / (double)result.Height, 2.38, 2.40);
        Assert.Equal(30, result.FrameRate);
    }

    [Theory]
    [InlineData(100, 96)]
    [InlineData(145, 160)]
    [InlineData(180, 192)]
    [InlineData(256, 192)]
    public void CustomPreset_MapsToSupportedMediaFoundationAacRate(int requested, int expected)
    {
        var result = _service.Resolve(QualityPresetKind.Custom, 1920, 1080, 30,
            new CustomQualitySettings { AudioBitrateKbps = requested });

        Assert.Equal(expected, result.AudioBitrateKbps);
    }
}
