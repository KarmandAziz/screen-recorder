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
    private readonly ILoggingService _logger;
    private readonly DispatcherTimer _timer;
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
    private int _customWidth;
    private int _customHeight;
    private int _customFrameRate;
    private int _customVideoBitrate;
    private int _customAudioBitrate;
    private bool _customHardwareEncoding;
    private RecordingState _state = RecordingState.Ready;
    private string _statusText = "Ready";
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
        _logger = logger;

        StartCommand = new AsyncRelayCommand(StartRecordingAsync, CanStart);
        PauseCommand = new AsyncRelayCommand(_recordingService.PauseAsync, () => State == RecordingState.Recording);
        ResumeCommand = new AsyncRelayCommand(_recordingService.ResumeAsync, () => State == RecordingState.Paused);
        StopCommand = new AsyncRelayCommand(StopRecordingAsync, CanStop);
        SelectAreaCommand = new AsyncRelayCommand(SelectAreaAsync, () => !IsRecordingActive);
        BrowseFolderCommand = new RelayCommand(BrowseFolder, () => !IsRecordingActive);
        OpenFileCommand = new RelayCommand(OpenFile, () => File.Exists(SavedPath));
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Directory.Exists(OutputFolder));
        ApplyShortcutsCommand = new AsyncRelayCommand(() => ApplyHotkeysAsync(true), () => !IsRecordingActive);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices, () => !IsRecordingActive);

        _recordingService.StateChanged += OnRecordingStateChanged;
        _hotkeys.Pressed += OnHotkeyPressed;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
        {
            DurationText = FormatDuration(_recordingService.Elapsed);
        }, Application.Current.Dispatcher);
        _timer.Start();
    }

    public ObservableCollection<DisplayInfo> Displays { get; } = [];
    public ObservableCollection<AudioDeviceInfo> Microphones { get; } = [];
    public IReadOnlyList<int> FrameRates { get; } = [15, 30, 60];

    public CaptureSourceKind SourceKind
    {
        get => _sourceKind;
        set
        {
            if (!SetProperty(ref _sourceKind, value)) return;
            OnPropertyChanged(nameof(IsMonitorSource));
            OnPropertyChanged(nameof(IsCustomAreaSource));
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

    public DisplayInfo? SelectedDisplay { get => _selectedDisplay; set => SetProperty(ref _selectedDisplay, value); }

    public PixelRect? SelectedRegion
    {
        get => _selectedRegion;
        private set
        {
            if (!SetProperty(ref _selectedRegion, value)) return;
            OnPropertyChanged(nameof(SelectedRegionText));
        }
    }

    public string SelectedRegionText => SelectedRegion?.ToString() ?? "No area selected";
    public bool IsMonitorSource => SourceKind == CaptureSourceKind.SelectedMonitor;
    public bool IsCustomAreaSource => SourceKind == CaptureSourceKind.CustomArea;

    public bool RecordSystemAudio { get => _recordSystemAudio; set => SetProperty(ref _recordSystemAudio, value); }
    public bool RecordMicrophone
    {
        get => _recordMicrophone;
        set
        {
            if (SetProperty(ref _recordMicrophone, value)) OnPropertyChanged(nameof(IsMicrophoneSelectionEnabled));
        }
    }
    public bool IsMicrophoneSelectionEnabled => RecordMicrophone && Microphones.Count > 0;
    public AudioDeviceInfo? SelectedMicrophone { get => _selectedMicrophone; set => SetProperty(ref _selectedMicrophone, value); }
    public double SystemAudioVolume { get => _systemAudioVolume; set => SetProperty(ref _systemAudioVolume, value); }
    public double MicrophoneVolume { get => _microphoneVolume; set => SetProperty(ref _microphoneVolume, value); }

    public QualityPresetKind QualityPreset
    {
        get => _qualityPreset;
        set
        {
            if (SetProperty(ref _qualityPreset, value)) OnPropertyChanged(nameof(IsCustomQuality));
        }
    }
    public bool IsCustomQuality => QualityPreset == QualityPresetKind.Custom;
    public int FrameRate { get => _frameRate; set => SetProperty(ref _frameRate, value); }
    public int CustomWidth { get => _customWidth; set => SetProperty(ref _customWidth, value); }
    public int CustomHeight { get => _customHeight; set => SetProperty(ref _customHeight, value); }
    public int CustomFrameRate { get => _customFrameRate; set => SetProperty(ref _customFrameRate, value); }
    public int CustomVideoBitrate { get => _customVideoBitrate; set => SetProperty(ref _customVideoBitrate, value); }
    public int CustomAudioBitrate { get => _customAudioBitrate; set => SetProperty(ref _customAudioBitrate, value); }
    public bool CustomHardwareEncoding { get => _customHardwareEncoding; set => SetProperty(ref _customHardwareEncoding, value); }

    public string OutputFolder
    {
        get => _outputFolder;
        set
        {
            if (SetProperty(ref _outputFolder, value)) OpenFolderCommand.RaiseCanExecuteChanged();
        }
    }
    public string StartStopShortcutText { get => _startStopShortcutText; set => SetProperty(ref _startStopShortcutText, value); }
    public string PauseResumeShortcutText { get => _pauseResumeShortcutText; set => SetProperty(ref _pauseResumeShortcutText, value); }
    public string SelectAreaShortcutText { get => _selectAreaShortcutText; set => SetProperty(ref _selectAreaShortcutText, value); }
    public bool MinimizeWhenRecording { get => _minimizeWhenRecording; set => SetProperty(ref _minimizeWhenRecording, value); }
    public bool ShowRecordingIndicator { get => _showRecordingIndicator; set => SetProperty(ref _showRecordingIndicator, value); }

    public RecordingState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsRecordingActive));
            RaiseCommandStates();
        }
    }
    public bool IsRecordingActive => State is RecordingState.Starting or RecordingState.Recording or RecordingState.Paused or RecordingState.Saving;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string DurationText { get => _durationText; private set => SetProperty(ref _durationText, value); }
    public string? SavedPath
    {
        get => _savedPath;
        private set
        {
            if (!SetProperty(ref _savedPath, value)) return;
            OpenFileCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand PauseCommand { get; }
    public AsyncRelayCommand ResumeCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand SelectAreaCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
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

    public Task StopForShutdownAsync() => StopRecordingAsync();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _recordingService.StateChanged -= OnRecordingStateChanged;
        _hotkeys.Pressed -= OnHotkeyPressed;
        _indicator.Hide();
        _regionSelection.HideSelectionBorder();
        GC.SuppressFinalize(this);
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            await SaveSettingsAsync();
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
                OutputFolder);
            await _recordingService.StartAsync(request);
        }
        catch (Exception exception)
        {
            _logger.Error("Start command failed.", exception);
            if (SourceKind == CaptureSourceKind.CustomArea && SelectedRegion is not null)
                _regionSelection.ShowSelectionBorder(SelectedRegion.Value);
            MessageBox.Show(exception.Message, "Unable to record", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopRecordingAsync()
    {
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
        await SaveSettingsAsync();
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
                    if (ShowRecordingIndicator || MinimizeWhenRecording) _indicator.Show();
                    _indicator.SetPaused(false);
                    break;
                case RecordingState.Paused:
                    _indicator.SetPaused(true);
                    break;
                case RecordingState.Saved:
                case RecordingState.Error:
                    _indicator.Hide();
                    if (SourceKind == CaptureSourceKind.CustomArea && SelectedRegion is not null)
                        _regionSelection.ShowSelectionBorder(SelectedRegion.Value);
                    break;
            }
        });
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

    private bool CanStart() => State is RecordingState.Ready or RecordingState.Saved or RecordingState.Error;
    private bool CanStop() => State is RecordingState.Starting or RecordingState.Recording or RecordingState.Paused;

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
        Hotkeys = _effectiveHotkeys,
        MinimizeWhenRecording = MinimizeWhenRecording,
        ShowRecordingIndicator = ShowRecordingIndicator,
        CustomQuality = CreateCustomQuality()
    };

    private static string FormatDuration(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
}
