using LocalScreenRecorder.App.Views;

namespace LocalScreenRecorder.App.Services;

public sealed class RecordingIndicatorService : IRecordingIndicatorService
{
    private RecordingIndicatorWindow? _window;

    public void Show()
    {
        if (_window is not null) return;
        _window = new RecordingIndicatorWindow();
        _window.Show();
    }

    public void Hide()
    {
        _window?.Close();
        _window = null;
    }

    public void SetPaused(bool paused) => _window?.SetPaused(paused);
}
