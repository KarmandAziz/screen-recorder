using System.Windows;
using System.Windows.Media;
using LocalScreenRecorder.App.Utilities;

namespace LocalScreenRecorder.App.Views;

public partial class RecordingIndicatorWindow : Window
{
    public RecordingIndicatorWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowCaptureExclusion.Apply(this);
        Loaded += (_, _) =>
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 18;
            Top = workArea.Top + 18;
        };
    }

    public void SetPaused(bool paused)
    {
        StateText.Text = paused ? "Paused" : "Recording";
        StateDot.Fill = new SolidColorBrush(paused ? Color.FromRgb(245, 158, 11) : Color.FromRgb(239, 68, 68));
    }
}
