using System.Windows;
using LocalScreenRecorder.App.Views;
using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.App.Services;

public sealed class RegionSelectionService(IDisplayService displays) : IRegionSelectionService
{
    private SelectionBorderWindow? _borderWindow;

    public Task<PixelRect?> SelectAsync(PixelRect? currentRegion = null)
    {
        HideSelectionBorder();
        var overlay = new RegionSelectionWindow(displays.GetVirtualBounds(), currentRegion);
        var accepted = overlay.ShowDialog() == true;
        var selected = accepted ? overlay.SelectedRegion : currentRegion;
        if (selected is not null) ShowSelectionBorder(selected.Value);
        return Task.FromResult(selected);
    }

    public void ShowSelectionBorder(PixelRect region)
    {
        HideSelectionBorder();
        _borderWindow = new SelectionBorderWindow(region);
        _borderWindow.Show();
    }

    public void HideSelectionBorder()
    {
        _borderWindow?.Close();
        _borderWindow = null;
    }
}
