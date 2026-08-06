using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.Core.Services;

public sealed class QualityPresetService
{
    public EncodingSettings Resolve(
        QualityPresetKind preset,
        int sourceWidth,
        int sourceHeight,
        int requestedFrameRate,
        CustomQualitySettings? custom = null)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "The capture source must have a positive size.");
        }

        requestedFrameRate = requestedFrameRate is 15 or 30 or 60 ? requestedFrameRate : 30;
        custom ??= new CustomQualitySettings();

        return preset switch
        {
            QualityPresetKind.Low => Create(sourceWidth, sourceHeight, 1280, 720,
                requestedFrameRate == 15 ? 15 : 30, 2_000, 96, true),
            QualityPresetKind.Medium => Create(sourceWidth, sourceHeight, 1920, 1080,
                30, 5_000, 128, true),
            QualityPresetKind.High => Create(sourceWidth, sourceHeight, sourceWidth, sourceHeight,
                requestedFrameRate == 60 ? 60 : 30, 10_000, 192, true),
            QualityPresetKind.VeryHigh => Create(sourceWidth, sourceHeight, sourceWidth, sourceHeight,
                requestedFrameRate, 20_000, 192, true),
            QualityPresetKind.Custom => Create(sourceWidth, sourceHeight,
                Math.Clamp(custom.Width, 160, 16384), Math.Clamp(custom.Height, 90, 16384),
                Math.Clamp(custom.FrameRate, 1, 120),
                Math.Clamp(custom.VideoBitrateKbps, 250, 100_000),
                NormalizeAudioBitrate(custom.AudioBitrateKbps), custom.UseHardwareEncoding, true),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };
    }

    public static int NormalizeAudioBitrate(int requestedKbps)
    {
        int[] supported = [96, 128, 160, 192];
        return supported.MinBy(value => Math.Abs(value - requestedKbps));
    }

    private static EncodingSettings Create(
        int sourceWidth,
        int sourceHeight,
        int maxWidth,
        int maxHeight,
        int frameRate,
        int videoBitrate,
        int audioBitrate,
        bool hardware,
        bool allowUpscale = false)
    {
        var scale = Math.Min(maxWidth / (double)sourceWidth, maxHeight / (double)sourceHeight);
        if (!allowUpscale) scale = Math.Min(1d, scale);
        var width = MakeEven(Math.Max(2, (int)Math.Round(sourceWidth * scale)));
        var height = MakeEven(Math.Max(2, (int)Math.Round(sourceHeight * scale)));
        return new EncodingSettings(width, height, frameRate, videoBitrate, audioBitrate, hardware);
    }

    private static int MakeEven(int value) => value % 2 == 0 ? value : value - 1;
}
