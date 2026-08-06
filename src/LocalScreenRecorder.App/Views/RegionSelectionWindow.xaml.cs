using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LocalScreenRecorder.App.Utilities;
using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.App.Views;

public partial class RegionSelectionWindow : Window
{
    private readonly PixelRect _virtualBounds;
    private NativeMethods.NativePoint _start;
    private bool _dragging;

    public RegionSelectionWindow(PixelRect virtualBounds, PixelRect? currentRegion)
    {
        InitializeComponent();
        _virtualBounds = virtualBounds;
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            if (currentRegion is not null) RenderSelection(currentRegion.Value);
        };
    }

    public PixelRect? SelectedRegion { get; private set; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(handle, new nint(NativeMethods.HwndTopmost),
            _virtualBounds.X, _virtualBounds.Y, _virtualBounds.Width, _virtualBounds.Height, 0);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!NativeMethods.GetCursorPos(out _start)) return;
        _dragging = true;
        CaptureMouse();
        RenderSelection(new PixelRect(_start.X, _start.Y, 0, 0));
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || !NativeMethods.GetCursorPos(out var current)) return;
        RenderSelection(PixelRect.FromPoints(_start.X, _start.Y, current.X, current.Y));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging || !NativeMethods.GetCursorPos(out var current)) return;
        _dragging = false;
        ReleaseMouseCapture();
        var selection = PixelRect.FromPoints(_start.X, _start.Y, current.X, current.Y).Intersect(_virtualBounds);
        if (selection.Width < 16 || selection.Height < 16)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            SizeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        SelectedRegion = selection;
        DialogResult = true;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DialogResult = false;
        Close();
    }

    private void RenderSelection(PixelRect selection)
    {
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = transform.Transform(new Point(selection.Left - _virtualBounds.Left, selection.Top - _virtualBounds.Top));
        var bottomRight = transform.Transform(new Point(selection.Right - _virtualBounds.Left, selection.Bottom - _virtualBounds.Top));
        var width = Math.Max(0, bottomRight.X - topLeft.X);
        var height = Math.Max(0, bottomRight.Y - topLeft.Y);

        Canvas.SetLeft(SelectionRectangle, topLeft.X);
        Canvas.SetTop(SelectionRectangle, topLeft.Y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
        SelectionRectangle.Visibility = Visibility.Visible;

        SizeText.Text = $"{selection.Width} × {selection.Height}";
        SizeBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(SizeBadge, Math.Max(8, topLeft.X + 8));
        Canvas.SetTop(SizeBadge, Math.Max(8, topLeft.Y + height - SizeBadge.DesiredSize.Height - 8));
        SizeBadge.Visibility = selection.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
    }
}
