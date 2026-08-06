using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using LocalScreenRecorder.App.Services;
using LocalScreenRecorder.App.Utilities;
using LocalScreenRecorder.App.ViewModels;
using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IHotkeyService _hotkeys;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel, IHotkeyService hotkeys)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _hotkeys = hotkeys;
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        Closed += OnClosed;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowCaptureExclusion.Apply(this);
        _hotkeys.Initialize(new WindowInteropHelper(this).Handle);
        await _viewModel.ActivateHotkeysAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.State)) return;
        if (_viewModel.State == RecordingState.Recording && _viewModel.MinimizeWhenRecording)
        {
            WindowState = WindowState.Minimized;
        }
        else if (_viewModel.State is RecordingState.Saved or RecordingState.Error)
        {
            WindowState = WindowState.Normal;
            Show();
            Activate();
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_viewModel.IsRecordingActive)
        {
            var answer = MessageBox.Show(
                "A recording is active. Stop and finalize it before closing?",
                "Recording in progress",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            await _viewModel.StopForShutdownAsync();
        }

        try { await _viewModel.SaveSettingsAsync(); } catch { }
        _allowClose = true;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
    }
}
