using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Input;
using LocalScreenRecorder.App.Services;
using LocalScreenRecorder.App.Utilities;
using LocalScreenRecorder.App.ViewModels;
using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IHotkeyService _hotkeys;
    private readonly ISystemTrayService _tray;
    private bool _allowClose;
    private bool _closingInProgress;

    public MainWindow(MainViewModel viewModel, IHotkeyService hotkeys, ISystemTrayService tray)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _hotkeys = hotkeys;
        _tray = tray;
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        Closed += OnClosed;
        Loaded += OnLoaded;
        _tray.ActionRequested += OnTrayActionRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowCaptureExclusion.Apply(this);
        _tray.Initialize();
        _hotkeys.Initialize(new WindowInteropHelper(this).Handle);
        await _viewModel.ActivateHotkeysAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.StartMinimized) WindowState = WindowState.Minimized;
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
        if (_closingInProgress) return;
        _closingInProgress = true;
        if (_viewModel.IsRecordingActive)
        {
            var answer = MessageBox.Show(
                "A recording is active. Stop and finalize it before closing?",
                "Recording in progress",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                _closingInProgress = false;
                return;
            }
            if (!await _viewModel.StopForShutdownAsync())
            {
                MessageBox.Show(
                    "The recording is still active or could not be finalized. The application will stay open to protect the recording.",
                    "Unable to exit safely",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _closingInProgress = false;
                return;
            }
        }

        try { await _viewModel.SaveSettingsAsync(); } catch { }
        _allowClose = true;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _tray.ActionRequested -= OnTrayActionRequested;
        _viewModel.Dispose();
    }

    private void OnTrayActionRequested(object? sender, TrayAction action)
    {
        Dispatcher.BeginInvoke(() =>
        {
            switch (action)
            {
                case TrayAction.OpenApplication:
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    break;
                case TrayAction.Exit:
                    Close();
                    break;
            }
        });
    }

    private void OnHotkeyTextBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
        {
            textBox.Clear();
            e.Handled = true;
            return;
        }

        var modifiers = ToHotkeyModifiers(Keyboard.Modifiers);
        var keyName = ToHotkeyKeyName(key);
        if (modifiers == HotkeyModifiers.None || keyName is null) return;
        textBox.Text = new HotkeyGesture(modifiers, keyName).ToString();
        textBox.CaretIndex = textBox.Text.Length;
        e.Handled = true;
    }

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Windows;
        return result;
    }

    private static string? ToHotkeyKeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z) return key.ToString();
        if (key is >= Key.D0 and <= Key.D9) return ((int)(key - Key.D0)).ToString();
        if (key is >= Key.F1 and <= Key.F24) return key.ToString();
        return key switch
        {
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter or Key.Return => "Enter",
            Key.Escape => "Escape",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            _ => null
        };
    }
}
