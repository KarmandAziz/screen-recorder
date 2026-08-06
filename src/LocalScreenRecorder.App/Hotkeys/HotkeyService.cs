using System.ComponentModel;
using System.Windows.Interop;
using LocalScreenRecorder.App.Utilities;
using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.App.Services;

public sealed class HotkeyService(HotkeyValidator validator, ILoggingService logger) : IHotkeyService
{
    private const int StartStopId = 0x5101;
    private const int PauseResumeId = 0x5102;
    private const int SelectAreaId = 0x5103;
    private nint _windowHandle;
    private HwndSource? _source;
    private HotkeySettings? _registeredSettings;

    public event EventHandler<HotkeyAction>? Pressed;

    public void Initialize(nint windowHandle)
    {
        if (windowHandle == nint.Zero) throw new ArgumentException("A valid window handle is required.", nameof(windowHandle));
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);
        _source?.AddHook(WindowProc);
    }

    public bool TryRegister(HotkeySettings settings, out string error)
    {
        error = validator.ValidateSet(settings) ?? string.Empty;
        if (error.Length > 0) return false;
        if (_windowHandle == nint.Zero)
        {
            error = "The application window is not ready to register shortcuts.";
            return false;
        }

        var previous = _registeredSettings;
        UnregisterAll();
        if (TryRegisterGesture(StartStopId, settings.StartStop, out error) &&
            TryRegisterGesture(PauseResumeId, settings.PauseResume, out error) &&
            TryRegisterGesture(SelectAreaId, settings.SelectArea, out error))
        {
            _registeredSettings = settings;
            return true;
        }

        UnregisterAll();
        if (previous is not null)
        {
            _ = TryRegisterGesture(StartStopId, previous.StartStop, out _);
            _ = TryRegisterGesture(PauseResumeId, previous.PauseResume, out _);
            _ = TryRegisterGesture(SelectAreaId, previous.SelectArea, out _);
            _registeredSettings = previous;
        }
        return false;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WindowProc);
        _source = null;
        _windowHandle = nint.Zero;
        GC.SuppressFinalize(this);
    }

    private bool TryRegisterGesture(int id, HotkeyGesture gesture, out string error)
    {
        validator.TryGetVirtualKey(gesture.Key, out var key);
        if (NativeMethods.RegisterHotKey(_windowHandle, id, (uint)gesture.Modifiers | NativeMethods.ModNoRepeat, key))
        {
            error = string.Empty;
            return true;
        }

        var nativeError = new Win32Exception().Message;
        error = $"{gesture} is already in use by Windows or another application. Choose a different shortcut.";
        logger.Warn($"RegisterHotKey failed for {gesture}: {nativeError}");
        return false;
    }

    private void UnregisterAll()
    {
        if (_windowHandle == nint.Zero) return;
        NativeMethods.UnregisterHotKey(_windowHandle, StartStopId);
        NativeMethods.UnregisterHotKey(_windowHandle, PauseResumeId);
        NativeMethods.UnregisterHotKey(_windowHandle, SelectAreaId);
        _registeredSettings = null;
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != NativeMethods.WmHotkey) return nint.Zero;
        var action = wParam.ToInt32() switch
        {
            StartStopId => HotkeyAction.StartStop,
            PauseResumeId => HotkeyAction.PauseResume,
            SelectAreaId => HotkeyAction.SelectArea,
            _ => (HotkeyAction?)null
        };
        if (action is not null)
        {
            handled = true;
            Pressed?.Invoke(this, action.Value);
        }
        return nint.Zero;
    }
}
