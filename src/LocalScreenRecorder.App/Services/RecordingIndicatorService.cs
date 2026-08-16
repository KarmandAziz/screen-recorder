using LocalScreenRecorder.App.Views;
using LocalScreenRecorder.Core.Models;

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

    public void Update(RecordingState state, TimeSpan elapsed) => _window?.Update(state, elapsed);
}
