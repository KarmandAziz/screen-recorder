using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LocalScreenRecorder.App.Models;
using LocalScreenRecorder.App.Services;
using LocalScreenRecorder.App.Utilities;
using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDisplayService _displayService;
    private readonly IAudioCaptureService _audioService;
    private readonly ISettingsService _settingsService;
    private readonly IRecordingService _recordingService;
    private readonly IRegionSelectionService _regionSelection;
    private readonly IRecordingIndicatorService _indicator;
    private readonly IHotkeyService _hotkeys;
    private readonly HotkeyValidator _hotkeyValidator;
    private readonly IFolderPickerService _folderPicker;
    private readonly ISystemTrayService _tray;
    private readonly ILoggingService _logger;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _countdownCancellation;
    private HotkeySettings _effectiveHotkeys = new();
    private CaptureSourceKind _sourceKind;
    private DisplayInfo? _selectedDisplay;
    private PixelRect? _selectedRegion;
    private bool _recordSystemAudio;
    private bool _recordMicrophone;
    private AudioDeviceInfo? _selectedMicrophone;
    private double _systemAudioVolume;
    private double _microphoneVolume;
    private QualityPresetKind _qualityPreset;
    private int _frameRate;
    private string _outputFolder = string.Empty;
    private string _startStopShortcutText = string.Empty;
    private string _pauseResumeShortcutText = string.Empty;
    private string _selectAreaShortcutText = string.Empty;
    private bool _minimizeWhenRecording;
    private bool _showRecordingIndicator;
    private bool _showNotificationAfterRecording;
    private bool _startMinimized;
    private bool _includeCursor;
    private int _countdownSeconds;
    private int _customWidth;
    private int _customHeight;
    private int _customFrameRate;
    private int _customVideoBitrate;
    private int _customAudioBitrate;
    private bool _customHardwareEncoding;
    private RecordingState _state = RecordingState.Ready;
    private string _statusText = "Choose what to record, then press Start.";
    private string _durationText = "00:00:00";
    private string? _savedPath;
    private bool _disposed;

    public MainViewModel(
        IDisplayService displayService,
        IAudioCaptureService audioService,
        ISettingsService settingsService,
        IRecordingService recordingService,
        IRegionSelectionService regionSelection,
        IRecordingIndicatorService indicator,
        IHotkeyService hotkeys,
        HotkeyValidator hotkeyValidator,
        IFolderPickerService folderPicker,
        ISystemTrayService tray,
        ILoggingService logger)
    {
        _displayService = displayService;
        _audioService = audioService;
        _settingsService = settingsService;
        _recordingService = recordingService;
        _regionSelection = regionSelection;
        _indicator = indicator;
        _hotkeys = hotkeys;
        _hotkeyValidator = hotkeyValidator;
        _folderPicker = folderPicker;
        _tray = tray;
        _logger = logger;

        StartCommand = new AsyncRelayCommand(StartRecordingAsync, CanStart);
        PauseCommand = new AsyncRelayCommand(_recordingService.PauseAsync, () => State == RecordingState.Recording, HandleCommandFailure);
        ResumeCommand = new AsyncRelayCommand(_recordingService.ResumeAsync, () => State == RecordingState.Paused, HandleCommandFailure);
        StopCommand = new AsyncRelayCommand(StopRecordingAsync, CanStop);
        SelectAreaCommand = new AsyncRelayCommand(SelectAreaAsync, () => !IsRecordingActive, HandleCommandFailure);
        BrowseFolderCommand = new RelayCommand(BrowseFolder, () => !IsRecordingActive);
        OpenFileCommand = new RelayCommand(OpenFile, () => File.Exists(SavedPath));
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Directory.Exists(OutputFolder));
        CopyPathCommand = new RelayCommand(CopySavedPath, () => File.Exists(SavedPath));
        ApplyShortcutsCommand = new AsyncRelayCommand(() => ApplyHotkeysAsync(true), () => !IsRecordingActive, HandleCommandFailure);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices, () => !IsRecordingActive);

        _recordingService.StateChanged += OnRecordingStateChanged;
        _hotkeys.Pressed += OnHotkeyPressed;
        _tray.ActionRequested += OnTrayActionRequested;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
        {
            var elapsed = _recordingService.Elapsed;
            DurationText = FormatDuration(elapsed);
            _indicator.Update(State, elapsed);
            _tray.Update(State, elapsed);
        }, Application.Current.Dispatcher);
        _timer.Start();
    }

    public ObservableCollection<DisplayInfo> Displays { get; } = [];
    public ObservableCollection<AudioDeviceInfo> Microphones { get; } = [];
    public IReadOnlyList<int> FrameRates => QualityPreset switch
    {
        QualityPresetKind.Low => [15, 30],
        QualityPresetKind.Medium => [30],
        QualityPresetKind.High => [30, 60],
        QualityPresetKind.VeryHigh => [15, 30, 60],
        _ => [30]
    };
    public IReadOnlyList<int> CountdownOptions { get; } = [0, 3, 5];

    public CaptureSourceKind SourceKind
    {
        get => _sourceKind;
        set
        {
            if (!SetProperty(ref _sourceKind, value)) return;
            OnPropertyChanged(nameof(IsMonitorSource));
            OnPropertyChanged(nameof(IsCustomAreaSource));
            OnPropertyChanged(nameof(SourceSummary));
            if (!IsRecordingActive)
            {
                if (value == CaptureSourceKind.CustomArea && SelectedRegion is not null)
                    _regionSelection.ShowSelectionBorder(SelectedRegion.Value);
                else
                    _regionSelection.HideSelectionBorder();
            }
            RaiseCommandStates();
        }
    }

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (SetProperty(ref _selectedDisplay, value)) OnPropertyChanged(nameof(SourceSummary));
        }
    }

    public PixelRect? SelectedRegion
    {
        get => _selectedRegion;
        private set
        {
            if (!SetProperty(ref _selectedRegion, value)) return;
            OnPropertyChanged(nameof(SelectedRegionText));
            OnPropertyChanged(nameof(SourceSummary));
        }
    }

    public string SelectedRegionText => SelectedRegion?.ToString() ?? "No area selected";
    public bool IsMonitorSource => SourceKind == CaptureSourceKind.SelectedMonitor;
    public bool IsCustomAreaSource => SourceKind == CaptureSourceKind.CustomArea;
    public string SourceSummary => SourceKind switch
    {
        CaptureSourceKind.EntireScreen => Displays.Count > 1 ? $"All {Displays.Count} displays" : "Entire display",
        CaptureSourceKind.SelectedMonitor => SelectedDisplay?.Label ?? "Choose a monitor",
        CaptureSourceKind.CustomArea => SelectedRegion is null ? "Choose an area" : SelectedRegionText,
        _ => "Screen"
    };

    public bool RecordSystemAudio
    {
        get => _recordSystemAudio;
        set
        {
            if (SetProperty(ref _recordSystemAudio, value)) OnPropertyChanged(nameof(AudioSummary));
        }
    }
    public bool RecordMicrophone
    {
        get => _recordMicrophone;
        set
        {
            if (!SetProperty(ref _recordMicrophone, value)) return;
            OnPropertyChanged(nameof(IsMicrophoneSelectionEnabled));
            OnPropertyChanged(nameof(AudioSummary));
        }
    }
    public bool IsMicrophoneSelectionEnabled => RecordMicrophone && Microphones.Count > 0;
    public AudioDeviceInfo? SelectedMicrophone
    {
        get => _selectedMicrophone;
        set
        {
            if (SetProperty(ref _selectedMicrophone, value)) OnPropertyChanged(nameof(AudioSummary));
        }
    }
    public double SystemAudioVolume { get => _systemAudioVolume; set => SetProperty(ref _systemAudioVolume, value); }
    public double MicrophoneVolume { get => _microphoneVolume; set => SetProperty(ref _microphoneVolume, value); }

    public QualityPresetKind QualityPreset
    {
        get => _qualityPreset;
        set
        {
            if (!SetProperty(ref _qualityPreset, value)) return;
            OnPropertyChanged(nameof(IsCustomQuality));
            OnPropertyChanged(nameof(IsStandardQuality));
            OnPropertyChanged(nameof(FrameRates));
            FrameRate = NormalizeFrameRate(value, FrameRate);
            OnPropertyChanged(nameof(QualitySummary));
            OnPropertyChanged(nameof(QualityDescription));
        }
    }
    public bool IsCustomQuality => QualityPreset == QualityPresetKind.Custom;
    public bool IsStandardQuality => !IsCustomQuality;
    public int FrameRate
    {
        get => _frameRate;
        set
        {
            value = NormalizeFrameRate(QualityPreset, value);
            if (SetProperty(ref _frameRate, value)) OnPropertyChanged(nameof(QualitySummary));
        }
    }
    public int CustomWidth { get => _customWidth; set => SetProperty(ref _customWidth, value); }
    public int CustomHeight { get => _customHeight; set => SetProperty(ref _customHeight, value); }
    public int CustomFrameRate
    {
        get => _customFrameRate;
        set
        {
            if (SetProperty(ref _customFrameRate, value)) OnPropertyChanged(nameof(QualitySummary));
        }
    }
    public int CustomVideoBitrate { get => _customVideoBitrate; set => SetProperty(ref _customVideoBitrate, value); }
    public int CustomAudioBitrate { get => _customAudioBitrate; set => SetProperty(ref _customAudioBitrate, value); }
    public bool CustomHardwareEncoding { get => _customHardwareEncoding; set => SetProperty(ref _customHardwareEncoding, value); }

    public string OutputFolder
    {
        get => _outputFolder;
        set
        {
            if (!SetProperty(ref _outputFolder, value)) return;
            OpenFolderCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(OutputSummary));
        }
    }
    public string StartStopShortcutText { get => _startStopShortcutText; set => SetProperty(ref _startStopShortcutText, value); }
    public string PauseResumeShortcutText { get => _pauseResumeShortcutText; set => SetProperty(ref _pauseResumeShortcutText, value); }
    public string SelectAreaShortcutText { get => _selectAreaShortcutText; set => SetProperty(ref _selectAreaShortcutText, value); }
    public bool MinimizeWhenRecording { get => _minimizeWhenRecording; set => SetProperty(ref _minimizeWhenRecording, value); }
    public bool ShowRecordingIndicator { get => _showRecordingIndicator; set => SetProperty(ref _showRecordingIndicator, value); }
    public bool ShowNotificationAfterRecording { get => _showNotificationAfterRecording; set => SetProperty(ref _showNotificationAfterRecording, value); }
    public bool StartMinimized { get => _startMinimized; set => SetProperty(ref _startMinimized, value); }
    public bool IncludeCursor { get => _includeCursor; set => SetProperty(ref _includeCursor, value); }
    public int CountdownSeconds { get => _countdownSeconds; set => SetProperty(ref _countdownSeconds, value); }

    public string AudioSummary => (RecordSystemAudio, RecordMicrophone) switch
    {
        (true, true) => $"Desktop + {SelectedMicrophone?.Name ?? "microphone"}",
        (true, false) => "Desktop audio",
        (false, true) => SelectedMicrophone?.Name ?? "Microphone",
        _ => "No audio"
    };

    public string QualitySummary => $"{QualityPreset switch { QualityPresetKind.VeryHigh => "Very High", _ => QualityPreset.ToString() }} · {(IsCustomQuality ? CustomFrameRate : FrameRate)} FPS · MP4";
    public string QualityDescription => QualityPreset switch
    {
        QualityPresetKind.Low => "Small files for quick sharing.",
        QualityPresetKind.Medium => "Balanced quality and file size.",
        QualityPresetKind.High => "Recommended for crisp everyday recordings.",
        QualityPresetKind.VeryHigh => "Maximum detail with larger files.",
        QualityPresetKind.Custom => "Advanced resolution and bitrate controls.",
        _ => string.Empty
    };
    public string OutputSummary => string.IsNullOrWhiteSpace(OutputFolder) ? "Choose an output folder" : OutputFolder;
    public string ShortcutSummary => _effectiveHotkeys.StartStop.ToString();

    public RecordingState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsRecordingActive));
            OnPropertyChanged(nameof(CanEditSettings));
            OnPropertyChanged(nameof(ShowStartAction));
            OnPropertyChanged(nameof(ShowStopAction));
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(StateLabel));
            OnPropertyChanged(nameof(StateAccent));
            RaiseCommandStates();
        }
    }
    public bool IsRecordingActive => State is RecordingState.Countdown or RecordingState.Starting or RecordingState.Recording
        or RecordingState.Paused or RecordingState.Stopping or RecordingState.Finalizing;
    public bool CanEditSettings => !IsRecordingActive;
    public bool ShowStartAction => !IsRecordingActive;
    public bool ShowStopAction => IsRecordingActive;
    public bool IsRecording => State == RecordingState.Recording;
    public bool IsPaused => State == RecordingState.Paused;
    public string StateLabel => State switch
    {
        RecordingState.Ready => "Ready",
        RecordingState.Countdown => "Get ready",
        RecordingState.Starting => "Preparing",
        RecordingState.Recording => "Recording",
        RecordingState.Paused => "Paused",
        RecordingState.Stopping => "Stopping",
        RecordingState.Finalizing => "Saving",
        RecordingState.Saved => "Saved",
        RecordingState.Error => "Needs attention",
        _ => State.ToString()
    };
    public string StateAccent => State switch
    {
        RecordingState.Recording => "#FFEF4444",
        RecordingState.Paused or RecordingState.Countdown => "#FFF59E0B",
        RecordingState.Error => "#FFDC2626",
        RecordingState.Saved => "#FF10B981",
        _ => "#FF2563EB"
    };
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string DurationText { get => _durationText; private set => SetProperty(ref _durationText, value); }
    public string? SavedPath
    {
        get => _savedPath;
        private set
        {
            if (!SetProperty(ref _savedPath, value)) return;
            OpenFileCommand.RaiseCanExecuteChanged();
            CopyPathCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSavedRecording));
            OnPropertyChanged(nameof(SavedFileName));
        }
    }
    public bool HasSavedRecording => File.Exists(SavedPath);
    public string SavedFileName => SavedPath is null ? string.Empty : Path.GetFileName(SavedPath);

    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand PauseCommand { get; }
    public AsyncRelayCommand ResumeCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand SelectAreaCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand CopyPathCommand { get; }
    public AsyncRelayCommand ApplyShortcutsCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }

    public async Task InitializeAsync()
    {
        var settings = await _settingsService.LoadAsync();
        SourceKind = settings.RecordingSource;
        RecordSystemAudio = settings.RecordSystemAudio;
        RecordMicrophone = settings.RecordMicrophone;
        SystemAudioVolume = settings.SystemAudioVolume;
        MicrophoneVolume = settings.MicrophoneVolume;
        QualityPreset = settings.QualityPreset;
        FrameRate = settings.FrameRate;
        OutputFolder = settings.OutputFolder;
        MinimizeWhenRecording = settings.MinimizeWhenRecording;
        ShowRecordingIndicator = settings.ShowRecordingIndicator;
        ShowNotificationAfterRecording = settings.ShowNotificationAfterRecording;
        StartMinimized = settings.StartMinimized;
        IncludeCursor = settings.IncludeCursor;
        CountdownSeconds = settings.CountdownSeconds;
        SavedPath = settings.LastSavedPath;
        SelectedRegion = settings.LastCustomArea;
        CustomWidth = settings.CustomQuality.Width;
        CustomHeight = settings.CustomQuality.Height;
        CustomFrameRate = settings.CustomQuality.FrameRate;
        CustomVideoBitrate = settings.CustomQuality.VideoBitrateKbps;
        CustomAudioBitrate = settings.CustomQuality.AudioBitrateKbps;
        CustomHardwareEncoding = settings.CustomQuality.UseHardwareEncoding;
        _effectiveHotkeys = settings.Hotkeys;
        StartStopShortcutText = settings.Hotkeys.StartStop.ToString();
        PauseResumeShortcutText = settings.Hotkeys.PauseResume.ToString();
        SelectAreaShortcutText = settings.Hotkeys.SelectArea.ToString();
        OnPropertyChanged(nameof(ShortcutSummary));

        RefreshDevices(settings.LastSelectedMonitor, settings.SelectedMicrophone);
        if (SourceKind == CaptureSourceKind.CustomArea && SelectedRegion is not null)
        {
            _regionSelection.ShowSelectionBorder(SelectedRegion.Value);
        }
    }

    public async Task ActivateHotkeysAsync()
    {
        if (!_hotkeys.TryRegister(_effectiveHotkeys, out var error))
        {
            StatusText = error;
            MessageBox.Show(error, "Global shortcut conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await Task.CompletedTask;
    }

    public Task SaveSettingsAsync() => _settingsService.SaveAsync(CreateSettingsSnapshot());

    public async Task<bool> StopForShutdownAsync()
    {
        if (State == RecordingState.Countdown)
        {
            _countdownCancellation?.Cancel();
            return true;
        }

        try
        {
            await _recordingService.StopAsync();
            return !IsRecordingActive;
        }
        catch (Exception exception)
        {
            _logger.Error("Recording could not be stopped during shutdown.", exception);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _recordingService.StateChanged -= OnRecordingStateChanged;
        _hotkeys.Pressed -= OnHotkeyPressed;
        _tray.ActionRequested -= OnTrayActionRequested;
        _countdownCancellation?.Cancel();
        _countdownCancellation?.Dispose();
        _indicator.Hide();
        _regionSelection.HideSelectionBorder();
        GC.SuppressFinalize(this);
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            if (!await RunCountdownAsync()) return;
            await SaveSettingsSafelyAsync();
            _regionSelection.HideSelectionBorder();
            var request = new RecordingRequest(
                SourceKind,
                SelectedDisplay,
                SelectedRegion,
                Displays.ToArray(),
                RecordSystemAudio,
                RecordMicrophone,
                SelectedMicrophone?.Id,
                SystemAudioVolume,
                MicrophoneVolume,
                QualityPreset,
                FrameRate,
                CreateCustomQuality(),
                OutputFolder,
                IncludeCursor);
            await _recordingService.StartAsync(request);
        }
        catch (Exception exception)
        {
            _logger.Error("Start command failed.", exception);
            if (State == RecordingState.Countdown)
            {
                State = RecordingState.Error;
                StatusText = exception.Message;
            }
            if (SourceKind == CaptureSourceKind.CustomArea && SelectedRegion is not null)
                _regionSelection.ShowSelectionBorder(SelectedRegion.Value);
            MessageBox.Show(exception.Message, "Unable to record", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopRecordingAsync()
    {
        if (State == RecordingState.Countdown)
        {
            _countdownCancellation?.Cancel();
            return;
        }

        try
        {
            await _recordingService.StopAsync();
        }
        catch (Exception exception)
        {
            _logger.Error("Stop command failed.", exception);
            MessageBox.Show("The recording could not be finalized. Check the local log for details.",
                "Recording error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SelectAreaAsync()
    {
        var selected = await _regionSelection.SelectAsync(SelectedRegion);
        if (selected is null) return;
        SelectedRegion = selected;
        SourceKind = CaptureSourceKind.CustomArea;
        await SaveSettingsSafelyAsync();
    }

    private async Task ApplyHotkeysAsync(bool showSuccess)
    {
        if (!TryCreateHotkeySettings(out var settings, out var error) || !_hotkeys.TryRegister(settings, out error))
        {
            StatusText = error;
            MessageBox.Show(error, "Invalid global shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _effectiveHotkeys = settings;
        await SaveSettingsAsync();
        StatusText = "Global shortcuts updated";
        OnPropertyChanged(nameof(ShortcutSummary));
        if (showSuccess)
            MessageBox.Show("Global shortcuts were updated.", "Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool TryCreateHotkeySettings(out HotkeySettings settings, out string error)
    {
        settings = _effectiveHotkeys;
        if (!_hotkeyValidator.TryParse(StartStopShortcutText, out var startStop, out error))
        {
            error = $"Start/Stop: {error}";
            return false;
        }
        if (!_hotkeyValidator.TryParse(PauseResumeShortcutText, out var pauseResume, out error))
        {
            error = $"Pause/Resume: {error}";
            return false;
        }
        if (!_hotkeyValidator.TryParse(SelectAreaShortcutText, out var selectArea, out error))
        {
            error = $"Select Area: {error}";
            return false;
        }

        settings = new HotkeySettings { StartStop = startStop, PauseResume = pauseResume, SelectArea = selectArea };
        error = _hotkeyValidator.ValidateSet(settings) ?? string.Empty;
        return error.Length == 0;
    }

    private void RefreshDevices() => RefreshDevices(SelectedDisplay?.DeviceName, SelectedMicrophone?.Id);

    private void RefreshDevices(string? preferredDisplay, string? preferredMicrophone)
    {
        Displays.Clear();
        foreach (var display in _displayService.GetDisplays()) Displays.Add(display);
        SelectedDisplay = Displays.FirstOrDefault(display => display.DeviceName.Equals(preferredDisplay, StringComparison.OrdinalIgnoreCase))
                          ?? Displays.FirstOrDefault(display => display.IsPrimary)
                          ?? Displays.FirstOrDefault();

        Microphones.Clear();
        var devices = _audioService.GetMicrophones();
        if (devices.Count > 0) Microphones.Add(new AudioDeviceInfo(string.Empty, "Default microphone"));
        foreach (var device in devices) Microphones.Add(device);
        SelectedMicrophone = Microphones.FirstOrDefault(device => device.Id == preferredMicrophone) ?? Microphones.FirstOrDefault();
        OnPropertyChanged(nameof(IsMicrophoneSelectionEnabled));
        OnPropertyChanged(nameof(SourceSummary));
        OnPropertyChanged(nameof(AudioSummary));
    }

    private void BrowseFolder()
    {
        var selected = _folderPicker.PickFolder(OutputFolder);
        if (selected is null) return;
        OutputFolder = selected;
        OpenFolderCommand.RaiseCanExecuteChanged();
    }

    private void OpenFile()
    {
        if (SavedPath is null || !File.Exists(SavedPath)) return;
        Process.Start(new ProcessStartInfo(SavedPath) { UseShellExecute = true });
    }

    private void OpenFolder()
    {
        if (!Directory.Exists(OutputFolder)) return;
        Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
    }

    private void CopySavedPath()
    {
        if (SavedPath is null || !File.Exists(SavedPath)) return;
        try
        {
            System.Windows.Clipboard.SetText(SavedPath);
            StatusText = "Recording path copied";
        }
        catch (Exception exception)
        {
            _logger.Warn($"The recording path could not be copied: {exception.Message}");
            StatusText = "The recording path could not be copied";
        }
    }

    private async Task<bool> RunCountdownAsync()
    {
        if (CountdownSeconds == 0) return true;

        _countdownCancellation?.Dispose();
        _countdownCancellation = new CancellationTokenSource();
        State = RecordingState.Countdown;
        try
        {
            for (var remaining = CountdownSeconds; remaining > 0; remaining--)
            {
                StatusText = $"Recording starts in {remaining}… Press Stop to cancel.";
                await Task.Delay(TimeSpan.FromSeconds(1), _countdownCancellation.Token);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            State = RecordingState.Ready;
            StatusText = "Countdown cancelled";
            return false;
        }
        finally
        {
            _countdownCancellation.Dispose();
            _countdownCancellation = null;
        }
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            State = e.State;
            StatusText = e.Message;
            if (e.Path is not null) SavedPath = e.Path;

            switch (e.State)
            {
                case RecordingState.Recording:
                    if (ShowRecordingIndicator) _indicator.Show();
                    _indicator.Update(e.State, _recordingService.Elapsed);
                    break;
                case RecordingState.Paused:
                    _indicator.Update(e.State, _recordingService.Elapsed);
                    break;
                case RecordingState.Saved:
                    if (ShowNotificationAfterRecording)
                        _tray.ShowNotification("Recording saved", $"Saved {SavedFileName}");
                    _ = SaveSettingsSafelyAsync();
                    goto case RecordingState.Error;
                case RecordingState.Error:
                    _indicator.Hide();
                    if (e.State == RecordingState.Error)
                    {
                        _tray.ShowNotification("Recording needs attention", e.Message, isError: true);
                        if (e.Path is not null) _ = SaveSettingsSafelyAsync();
                    }
                    if (SourceKind == CaptureSourceKind.CustomArea && SelectedRegion is not null)
                        _regionSelection.ShowSelectionBorder(SelectedRegion.Value);
                    break;
            }
            _tray.Update(e.State, _recordingService.Elapsed);
        });
    }

    private async Task SaveSettingsSafelyAsync()
    {
        try { await SaveSettingsAsync(); }
        catch (Exception exception) { _logger.Warn($"Settings could not be saved after recording: {exception.Message}"); }
    }

    private void HandleCommandFailure(Exception exception)
    {
        _logger.Error("A recorder command failed.", exception);
        StatusText = "The action could not be completed. See the local log for details.";
        _tray.ShowNotification("Action failed", StatusText, isError: true);
    }

    private async void OnHotkeyPressed(object? sender, HotkeyAction action)
    {
        try
        {
            switch (action)
            {
                case HotkeyAction.StartStop:
                    if (CanStop()) StopCommand.Execute(null);
                    else if (CanStart()) StartCommand.Execute(null);
                    break;
                case HotkeyAction.PauseResume:
                    if (State == RecordingState.Recording) PauseCommand.Execute(null);
                    else if (State == RecordingState.Paused) ResumeCommand.Execute(null);
                    break;
                case HotkeyAction.SelectArea when !IsRecordingActive:
                    SelectAreaCommand.Execute(null);
                    break;
            }
            await Task.CompletedTask;
        }
        catch (Exception exception)
        {
            _logger.Error("A global shortcut action failed.", exception);
        }
    }

    private void OnTrayActionRequested(object? sender, TrayAction action)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (action)
            {
                case TrayAction.StartStop:
                    if (CanStop()) StopCommand.Execute(null);
                    else if (CanStart()) StartCommand.Execute(null);
                    break;
                case TrayAction.PauseResume:
                    if (State == RecordingState.Recording) PauseCommand.Execute(null);
                    else if (State == RecordingState.Paused) ResumeCommand.Execute(null);
                    break;
                case TrayAction.OpenRecordingsFolder:
                    OpenFolderCommand.Execute(null);
                    break;
            }
        });
    }

    private bool CanStart() => State is RecordingState.Ready or RecordingState.Saved or RecordingState.Error;
    private bool CanStop() => State is RecordingState.Countdown or RecordingState.Starting or RecordingState.Recording or RecordingState.Paused;

    private void RaiseCommandStates()
    {
        StartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        SelectAreaCommand.RaiseCanExecuteChanged();
        BrowseFolderCommand.RaiseCanExecuteChanged();
        ApplyShortcutsCommand.RaiseCanExecuteChanged();
        RefreshDevicesCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
    }

    private CustomQualitySettings CreateCustomQuality() => new()
    {
        Width = CustomWidth,
        Height = CustomHeight,
        FrameRate = CustomFrameRate,
        VideoBitrateKbps = CustomVideoBitrate,
        AudioBitrateKbps = CustomAudioBitrate,
        UseHardwareEncoding = CustomHardwareEncoding
    };

    private AppSettings CreateSettingsSnapshot() => new()
    {
        RecordingSource = SourceKind,
        LastSelectedMonitor = SelectedDisplay?.DeviceName,
        LastCustomArea = SelectedRegion,
        QualityPreset = QualityPreset,
        FrameRate = FrameRate,
        RecordSystemAudio = RecordSystemAudio,
        RecordMicrophone = RecordMicrophone,
        SelectedMicrophone = SelectedMicrophone?.Id,
        SystemAudioVolume = SystemAudioVolume,
        MicrophoneVolume = MicrophoneVolume,
        OutputFolder = OutputFolder,
        LastSavedPath = SavedPath,
        Hotkeys = _effectiveHotkeys,
        MinimizeWhenRecording = MinimizeWhenRecording,
        ShowRecordingIndicator = ShowRecordingIndicator,
        ShowNotificationAfterRecording = ShowNotificationAfterRecording,
        StartMinimized = StartMinimized,
        IncludeCursor = IncludeCursor,
        CountdownSeconds = CountdownSeconds,
        CustomQuality = CreateCustomQuality()
    };

    private static string FormatDuration(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";

    private static int NormalizeFrameRate(QualityPresetKind preset, int requested) => preset switch
    {
        QualityPresetKind.Low => requested == 15 ? 15 : 30,
        QualityPresetKind.Medium => 30,
        QualityPresetKind.High => requested == 60 ? 60 : 30,
        QualityPresetKind.VeryHigh => requested is 15 or 30 or 60 ? requested : 30,
        _ => 30
    };
}
