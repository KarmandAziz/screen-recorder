namespace LocalScreenRecorder.App.Services;

public sealed class AudioMixerService : IAudioMixerService
{
    public (float SystemVolume, float MicrophoneVolume) Normalize(
        double systemVolume,
        double microphoneVolume,
        bool useSystem,
        bool useMicrophone)
    {
        var system = useSystem ? Math.Clamp(systemVolume, 0, 1) : 0;
        var microphone = useMicrophone ? Math.Clamp(microphoneVolume, 0, 1) : 0;
        var total = system + microphone;
        if (useSystem && useMicrophone && total > 1)
        {
            system /= total;
            microphone /= total;
        }

        return ((float)system, (float)microphone);
    }
}
