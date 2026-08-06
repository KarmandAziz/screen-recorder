using LocalScreenRecorder.Core.Models;
using ScreenRecorderLib;

namespace LocalScreenRecorder.App.Services;

public sealed class EncoderService : IEncoderService
{
    public OutputOptions CreateOutputOptions(EncodingSettings settings) => new()
    {
        RecorderMode = RecorderMode.Video,
        OutputFrameSize = new ScreenSize(settings.Width, settings.Height),
        Stretch = StretchMode.Uniform
    };

    public VideoEncoderOptions CreateVideoOptions(EncodingSettings settings) => new()
    {
        Encoder = new H264VideoEncoder
        {
            EncoderProfile = H264Profile.High,
            BitrateMode = H264BitrateControlMode.UnconstrainedVBR
        },
        Bitrate = checked(settings.VideoBitrateKbps * 1000),
        Framerate = settings.FrameRate,
        IsFixedFramerate = false,
        IsHardwareEncodingEnabled = settings.UseHardwareEncoding,
        IsLowLatencyEnabled = false,
        IsThrottlingDisabled = false,
        IsMp4FastStartEnabled = true,
        IsFragmentedMp4Enabled = false
    };
}
