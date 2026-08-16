using System.Diagnostics;
using LocalScreenRecorder.App.Models;
using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;
using ScreenRecorderLib;

namespace LocalScreenRecorder.App.Services;

public sealed class RecordingService(
    IScreenCaptureService screenCapture,
    IAudioCaptureService audioCapture,
    IEncoderService encoder,
    QualityPresetService qualityPresets,
    FilenameService filenames,
    ILoggingService logger) : IRecordingService
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Stopwatch _activeSegment = new();
    private readonly RecordingStateMachine _stateMachine = new();
    private Recorder? _recorder;
    private RecordingPaths? _paths;
    private TaskCompletionSource<bool>? _started;
    private TaskCompletionSource<string>? _completed;
    private TimeSpan _accumulated;
    private bool _disposed;

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    public RecordingState State => _stateMachine.State;

    public TimeSpan Elapsed
    {
        get { lock (_sync) return _accumulated + _activeSegment.Elapsed; }
    }

    public string? LastSavedPath { get; private set; }

    public async Task StartAsync(RecordingRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Task<bool>? started = null;
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (State is RecordingState.Starting or RecordingState.Recording or RecordingState.Paused
                or RecordingState.Stopping or RecordingState.Finalizing)
            {
                throw new InvalidOperationException("A recording is already active.");
            }

            ValidateEnvironment(request);
            SetState(RecordingState.Starting, "Starting capture…");
            var plan = screenCapture.CreatePlan(request);
            var quality = qualityPresets.Resolve(
                request.QualityPreset,
                plan.SourceBounds.Width,
                plan.SourceBounds.Height,
                request.FrameRate,
                request.CustomQuality);

            _paths = filenames.CreatePaths(request.OutputFolder, DateTimeOffset.Now);
            CleanupOldPartialFiles(request.OutputFolder);
            EnsureDiskSpace(_paths.TemporaryPath);

            var options = RecorderOptions.Default;
            options.SourceOptions = plan.SourceOptions;
            options.OutputOptions = encoder.CreateOutputOptions(quality);
            options.VideoEncoderOptions = encoder.CreateVideoOptions(quality);
            options.AudioOptions = audioCapture.CreateOptions(request, quality.AudioBitrateKbps);
            options.LogOptions.IsLogEnabled = true;
            options.LogOptions.LogFilePath = Path.Combine(Path.GetDirectoryName(logger.LogFilePath)!, "native-recorder.log");
            options.LogOptions.LogSeverityLevel = ScreenRecorderLib.LogLevel.Info;

            ResetTiming();
            LastSavedPath = null;
            _started = NewCompletionSource<bool>();
            _completed = NewCompletionSource<string>();
            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnRecordingComplete += RecorderOnRecordingComplete;
            _recorder.OnRecordingFailed += RecorderOnRecordingFailed;
            _recorder.OnStatusChanged += RecorderOnStatusChanged;

            logger.Info(
                $"Starting {request.SourceKind} recording to temporary file '{_paths.TemporaryPath}' at " +
                $"{quality.Width}x{quality.Height}, {quality.FrameRate} FPS; desktop audio={request.RecordSystemAudio}, " +
                $"microphone={request.RecordMicrophone}, cursor={request.IncludeCursor}, hardware={quality.UseHardwareEncoding}.");
            await Task.Run(() => _recorder.Record(_paths.TemporaryPath), cancellationToken);
            started = _started.Task;
        }
        catch (Exception exception)
        {
            throw HandleStartFailure(exception);
        }
        finally
        {
            _operationGate.Release();
        }

        try
        {
            await started!.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        catch (Exception exception) when (State is RecordingState.Stopping or RecordingState.Finalizing or RecordingState.Saved)
        {
            logger.Info($"Start wait ended after a concurrent stop: {exception.GetType().Name}.");
        }
        catch (Exception exception)
        {
            await _operationGate.WaitAsync(CancellationToken.None);
            try { throw HandleStartFailure(exception); }
            finally { _operationGate.Release(); }
        }
    }

    public async Task PauseAsync()
    {
        await _operationGate.WaitAsync();
        try
        {
            if (State != RecordingState.Recording || _recorder is null) return;
            logger.Info("Pausing recording.");
            await Task.Run(_recorder.Pause);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task ResumeAsync()
    {
        await _operationGate.WaitAsync();
        try
        {
            if (State != RecordingState.Paused || _recorder is null) return;
            logger.Info("Resuming recording.");
            await Task.Run(_recorder.Resume);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (State is RecordingState.Stopping or RecordingState.Finalizing && _completed is not null)
            {
                await _completed.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
                return;
            }

            if (State is not (RecordingState.Starting or RecordingState.Recording or RecordingState.Paused) || _recorder is null)
            {
                return;
            }

            SetState(RecordingState.Stopping, "Stopping capture safely…");
            logger.Info("Stopping recording and waiting for MP4 finalization.");
            StopTiming();
            var completion = _completed ?? throw new InvalidOperationException("The recording session is not initialized.");
            await Task.Run(_recorder.Stop, cancellationToken);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        }
        catch (TimeoutException exception)
        {
            logger.Error("MP4 finalization timed out.", exception);
            SetState(RecordingState.Error, "The MP4 file could not be finalized in time. Check the log for details.");
            throw;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_recorder is not null && State is RecordingState.Starting or RecordingState.Recording or RecordingState.Paused
                or RecordingState.Stopping or RecordingState.Finalizing)
            {
                _recorder.Stop();
                _completed?.Task.Wait(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception exception)
        {
            logger.Error("The recorder did not shut down cleanly.", exception);
        }
        finally
        {
            ReleaseRecorder();
            _operationGate.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private void RecorderOnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        if (!IsCurrentRecorder(sender)) return;
        switch (e.Status)
        {
            case RecorderStatus.Recording:
                lock (_sync)
                {
                    if (!_activeSegment.IsRunning) _activeSegment.Start();
                }
                SetState(RecordingState.Recording, "Recording");
                _started?.TrySetResult(true);
                break;
            case RecorderStatus.Paused:
                StopTiming();
                SetState(RecordingState.Paused, "Paused");
                break;
            case RecorderStatus.Finishing:
                StopTiming();
                SetState(RecordingState.Finalizing, "Finalizing MP4…");
                break;
        }
    }

    private void RecorderOnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        if (!IsCurrentRecorder(sender)) return;
        StopTiming();
        try
        {
            var paths = _paths ?? throw new InvalidOperationException("Recording paths are unavailable.");
            var finalPath = EnsureUniqueFinalPath(paths.FinalPath);
            if (!File.Exists(paths.TemporaryPath))
            {
                throw new IOException("The encoder completed without creating its temporary MP4 file.");
            }

            File.Move(paths.TemporaryPath, finalPath, false);
            LastSavedPath = finalPath;
            logger.Info($"Recording saved to '{finalPath}'.");
            SetState(RecordingState.Saved, "Recording saved", finalPath);
            _started?.TrySetCanceled();
            _completed?.TrySetResult(finalPath);
        }
        catch (Exception exception)
        {
            logger.Error("MP4 finalization failed.", exception);
            var recoveryPath = _paths?.TemporaryPath;
            if (recoveryPath is not null && File.Exists(recoveryPath)) LastSavedPath = recoveryPath;
            var wrapped = new IOException("The MP4 was encoded but could not be moved to its final filename.", exception);
            var recoveryMessage = recoveryPath is not null && File.Exists(recoveryPath)
                ? "The recording was encoded but could not be renamed. The recoverable partial MP4 remains in the output folder."
                : "The MP4 could not be finalized. Check the output folder and available disk space.";
            SetState(RecordingState.Error, recoveryMessage, LastSavedPath);
            _started?.TrySetCanceled();
            _completed?.TrySetException(wrapped);
        }
        finally
        {
            ReleaseRecorder(sender as Recorder, deferDisposal: true);
        }
    }

    private void RecorderOnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        if (!IsCurrentRecorder(sender)) return;
        StopTiming();
        logger.Error($"Native recorder failure: {e.Error}");
        CleanupTemporaryFile();
        var message = ToFriendlyMessage(e.Error, "Recording stopped unexpectedly.");
        SetState(RecordingState.Error, message);
        var exception = new InvalidOperationException(message);
        _started?.TrySetException(exception);
        _completed?.TrySetException(exception);
        ReleaseRecorder(sender as Recorder, deferDisposal: true);
    }

    private void ValidateEnvironment(RecordingRequest request)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134))
        {
            throw new PlatformNotSupportedException("Windows Graphics Capture requires Windows 10 version 1803 or newer.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputFolder))
        {
            throw new IOException("Choose an output folder before recording.");
        }

        Directory.CreateDirectory(request.OutputFolder);
        var probe = Path.Combine(request.OutputFolder, $".write-test-{Guid.NewGuid():N}.tmp");
        using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }

        if (request.RecordSystemAudio && audioCapture.GetPlaybackDevices().Count == 0)
        {
            throw new InvalidOperationException("No active playback device is available for system-audio capture.");
        }

        if (request.RecordMicrophone && audioCapture.GetMicrophones().Count == 0)
        {
            throw new InvalidOperationException("No microphone is available. Disable microphone recording or connect a device.");
        }
    }

    private static void EnsureDiskSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (root is null || root.StartsWith("\\\\", StringComparison.Ordinal)) return;
            var drive = new DriveInfo(root);
            if (drive.IsReady && drive.AvailableFreeSpace < 250L * 1024 * 1024)
            {
                throw new IOException("The output drive has less than 250 MB of free space. Free space or choose another folder.");
            }
        }
        catch (ArgumentException)
        {
            // Some virtual or network-backed folders do not expose DriveInfo reliably.
        }
    }

    private void ResetTiming()
    {
        lock (_sync)
        {
            _activeSegment.Reset();
            _accumulated = TimeSpan.Zero;
        }
    }

    private void StopTiming()
    {
        lock (_sync)
        {
            if (!_activeSegment.IsRunning) return;
            _activeSegment.Stop();
            _accumulated += _activeSegment.Elapsed;
            _activeSegment.Reset();
        }
    }

    private void SetState(RecordingState state, string message, string? path = null)
    {
        if (!_stateMachine.TryTransitionTo(state) && _stateMachine.State != state)
        {
            logger.Warn($"Ignored invalid recording transition {_stateMachine.State} -> {state}: {message}");
            return;
        }
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(state, message, path));
    }

    private Exception HandleStartFailure(Exception exception)
    {
        logger.Error("Recording could not be started.", exception);
        CleanupFailedSession();
        var message = ToFriendlyMessage(exception.Message, "The recording could not be started.");
        SetState(RecordingState.Error, message);
        return new InvalidOperationException(message, exception);
    }

    private bool IsCurrentRecorder(object? sender)
    {
        lock (_sync) return sender is not null && ReferenceEquals(sender, _recorder);
    }

    private void CleanupFailedSession()
    {
        _started?.TrySetCanceled();
        _completed?.TrySetCanceled();
        CleanupTemporaryFile();
        ReleaseRecorder();
    }

    private void CleanupTemporaryFile()
    {
        try
        {
            if (_paths is not null && File.Exists(_paths.TemporaryPath)) File.Delete(_paths.TemporaryPath);
        }
        catch (Exception exception)
        {
            logger.Warn($"Could not delete incomplete recording '{_paths?.TemporaryPath}': {exception.Message}");
        }
    }

    private void ReleaseRecorder(Recorder? target = null, bool deferDisposal = false)
    {
        Recorder? recorder;
        lock (_sync)
        {
            recorder = target ?? _recorder;
            if (ReferenceEquals(_recorder, recorder)) _recorder = null;
        }
        if (recorder is null) return;
        recorder.OnRecordingComplete -= RecorderOnRecordingComplete;
        recorder.OnRecordingFailed -= RecorderOnRecordingFailed;
        recorder.OnStatusChanged -= RecorderOnStatusChanged;

        void DisposeRecorder()
        {
            try { recorder.Dispose(); }
            catch (Exception exception) { logger.Error("Recorder resource cleanup failed.", exception); }
        }

        if (deferDisposal)
            ThreadPool.QueueUserWorkItem(_ => DisposeRecorder());
        else
            DisposeRecorder();
    }

    private static string EnsureUniqueFinalPath(string desiredPath)
    {
        if (!File.Exists(desiredPath)) return desiredPath;
        var directory = Path.GetDirectoryName(desiredPath)!;
        var stem = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);
        for (var suffix = 2; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{suffix}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static void CleanupOldPartialFiles(string folder)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, ".*.partial.mp4"))
            {
                var file = new FileInfo(path);
                if (file.Length == 0 && file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7)) file.Delete();
            }
        }
        catch
        {
            // Stale cleanup is best-effort and should not prevent a new recording.
        }
    }

    private static string ToFriendlyMessage(string? technicalMessage, string fallback)
    {
        var text = technicalMessage ?? string.Empty;
        if (text.Contains("access", StringComparison.OrdinalIgnoreCase) || text.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return "Screen capture or the output folder was denied. Check Windows privacy permissions and folder access.";
        if (text.Contains("audio", StringComparison.OrdinalIgnoreCase) || text.Contains("device", StringComparison.OrdinalIgnoreCase))
            return "An audio device became unavailable. Reconnect it or change the audio settings and try again.";
        if (text.Contains("encoder", StringComparison.OrdinalIgnoreCase) || text.Contains("media foundation", StringComparison.OrdinalIgnoreCase))
            return "The H.264 encoder could not start. Update the graphics driver or disable hardware encoding in Custom quality.";
        if (text.Contains("display", StringComparison.OrdinalIgnoreCase) || text.Contains("monitor", StringComparison.OrdinalIgnoreCase))
            return "The selected monitor is no longer available. Refresh the source and try again.";
        if (text.Contains("disk", StringComparison.OrdinalIgnoreCase) || text.Contains("space", StringComparison.OrdinalIgnoreCase))
            return "There is not enough free disk space to finish the recording.";
        return fallback + " See the local log for technical details.";
    }

    private static TaskCompletionSource<T> NewCompletionSource<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
