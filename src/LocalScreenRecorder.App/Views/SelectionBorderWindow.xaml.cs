using System.Windows;
using System.Windows.Interop;
using LocalScreenRecorder.App.Utilities;
using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.App.Views;

public partial class SelectionBorderWindow : Window
{
    private readonly PixelRect _region;

    public SelectionBorderWindow(PixelRect region)
    {
        InitializeComponent();
        _region = region;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        const int margin = 3;
        NativeMethods.SetWindowPos(handle, new nint(NativeMethods.HwndTopmost),
            _region.X - margin, _region.Y - margin, _region.Width + margin * 2, _region.Height + margin * 2,
            NativeMethods.SwpNoActivate);
        WindowCaptureExclusion.Apply(this);
        WindowCaptureExclusion.MakeClickThrough(this);
    }
}
