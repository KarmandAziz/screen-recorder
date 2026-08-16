using LocalScreenRecorder.App.Models;
using LocalScreenRecorder.Core.Models;
using ScreenRecorderLib;

namespace LocalScreenRecorder.App.Services;

public sealed class AudioCaptureService(IAudioMixerService mixer, ILoggingService logger) : IAudioCaptureService
{
    public IReadOnlyList<AudioDeviceInfo> GetMicrophones()
        => GetDevices(AudioDeviceSource.InputDevices, "microphone");

    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
        => GetDevices(AudioDeviceSource.OutputDevices, "playback");

    private IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceSource source, string deviceKind)
    {
        try
        {
            return Recorder.GetSystemAudioDevices(source)
                .Select(device => new AudioDeviceInfo(device.DeviceName, device.FriendlyName))
                .OrderBy(device => device.Name)
                .ToArray();
        }
        catch (Exception exception)
        {
            logger.Error($"Could not enumerate {deviceKind} devices.", exception);
            return [];
        }
    }

    public AudioOptions CreateOptions(RecordingRequest request, int audioBitrateKbps)
    {
        var enabled = request.RecordSystemAudio || request.RecordMicrophone;
        var microphoneId = ResolveMicrophoneId(request.MicrophoneDeviceId, request.RecordMicrophone);
        var volumes = mixer.Normalize(
            request.SystemAudioVolume,
            request.MicrophoneVolume,
            request.RecordSystemAudio,
            request.RecordMicrophone);

        return new AudioOptions
        {
            IsAudioEnabled = enabled,
            IsOutputDeviceEnabled = request.RecordSystemAudio,
            IsInputDeviceEnabled = request.RecordMicrophone,
            AudioOutputDevice = string.Empty,
            AudioInputDevice = microphoneId,
            OutputVolume = volumes.SystemVolume,
            InputVolume = volumes.MicrophoneVolume,
            Channels = AudioChannels.Stereo,
            Bitrate = audioBitrateKbps switch
            {
                <= 96 => AudioBitrate.bitrate_96kbps,
                <= 128 => AudioBitrate.bitrate_128kbps,
                <= 160 => AudioBitrate.bitrate_160kbps,
                _ => AudioBitrate.bitrate_192kbps
            }
        };
    }

    private string ResolveMicrophoneId(string? requestedId, bool enabled)
    {
        if (!enabled || string.IsNullOrWhiteSpace(requestedId)) return string.Empty;
        var available = GetMicrophones();
        if (available.Any(device => device.Id.Equals(requestedId, StringComparison.OrdinalIgnoreCase)))
            return requestedId;

        logger.Warn("The saved microphone is unavailable; the Windows default microphone will be used.");
        return string.Empty;
    }
}
