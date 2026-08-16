using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;
using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.App.Services;

public sealed class SystemTrayService : ISystemTrayService
{
    private Forms.NotifyIcon? _icon;
    private Forms.ToolStripMenuItem? _startStopItem;
    private Forms.ToolStripMenuItem? _pauseResumeItem;
    private RecordingState? _lastState;
    private long _lastElapsedSecond = -1;

    public event EventHandler<TrayAction>? ActionRequested;

    public void Initialize()
    {
        if (_icon is not null) return;

        _startStopItem = CreateItem("Start recording", TrayAction.StartStop);
        _pauseResumeItem = CreateItem("Pause recording", TrayAction.PauseResume);
        _pauseResumeItem.Enabled = false;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(CreateItem("Open Local Screen Recorder", TrayAction.OpenApplication, bold: true));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_startStopItem);
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(CreateItem("Open recordings folder", TrayAction.OpenRecordingsFolder));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(CreateItem("Exit", TrayAction.Exit));

        var executableIcon = TryGetApplicationIcon();
        _icon = new Forms.NotifyIcon
        {
            Icon = executableIcon,
            Text = "Local Screen Recorder · Ready",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => Raise(TrayAction.OpenApplication);
    }

    public void Update(RecordingState state, TimeSpan elapsed)
    {
        if (_icon is null || _startStopItem is null || _pauseResumeItem is null) return;
        var elapsedSecond = (long)elapsed.TotalSeconds;
        if (_lastState == state && _lastElapsedSecond == elapsedSecond) return;
        _lastState = state;
        _lastElapsedSecond = elapsedSecond;
        var active = state is RecordingState.Countdown or RecordingState.Starting or RecordingState.Recording
            or RecordingState.Paused or RecordingState.Stopping or RecordingState.Finalizing;
        _startStopItem.Text = active ? "Stop recording" : "Start recording";
        _startStopItem.Enabled = state is not (RecordingState.Stopping or RecordingState.Finalizing);
        _pauseResumeItem.Text = state == RecordingState.Paused ? "Resume recording" : "Pause recording";
        _pauseResumeItem.Enabled = state is RecordingState.Recording or RecordingState.Paused;
        var stateText = state switch
        {
            RecordingState.Recording => $"Recording · {FormatElapsed(elapsed)}",
            RecordingState.Paused => $"Paused · {FormatElapsed(elapsed)}",
            RecordingState.Finalizing => "Saving recording",
            _ => state.ToString()
        };
        var tooltip = $"Local Screen Recorder · {stateText}";
        _icon.Text = tooltip[..Math.Min(63, tooltip.Length)];
    }

    public void ShowNotification(string title, string message, bool isError = false)
    {
        if (_icon is null) return;
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = isError ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info;
        _icon.ShowBalloonTip(3500);
    }

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _icon = null;
        GC.SuppressFinalize(this);
    }

    private Forms.ToolStripMenuItem CreateItem(string text, TrayAction action, bool bold = false)
    {
        var item = new Forms.ToolStripMenuItem(text);
        if (bold) item.Font = new System.Drawing.Font(item.Font, System.Drawing.FontStyle.Bold);
        item.Click += (_, _) => Raise(action);
        return item;
    }

    private void Raise(TrayAction action) => ActionRequested?.Invoke(this, action);

    private static DrawingIcon TryGetApplicationIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } path && DrawingIcon.ExtractAssociatedIcon(path) is { } icon)
                return icon;
        }
        catch
        {
            // Fall back to a stable system icon when the host does not expose an executable icon.
        }
        return (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
}
