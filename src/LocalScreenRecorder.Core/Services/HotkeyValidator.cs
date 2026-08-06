using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.Core.Services;

public sealed class HotkeyValidator
{
    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Escape"] = 0x1B,
        ["Esc"] = 0x1B,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Left"] = 0x25,
        ["Right"] = 0x27
    };

    public bool TryParse(string? text, out HotkeyGesture gesture, out string error)
    {
        gesture = new HotkeyGesture(HotkeyModifiers.None, string.Empty);
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter a shortcut such as Ctrl+Shift+R.";
            return false;
        }

        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var token in tokens)
        {
            switch (token.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL": modifiers |= HotkeyModifiers.Control; break;
                case "SHIFT": modifiers |= HotkeyModifiers.Shift; break;
                case "ALT": modifiers |= HotkeyModifiers.Alt; break;
                case "WIN":
                case "WINDOWS": modifiers |= HotkeyModifiers.Windows; break;
                default:
                    if (key is not null)
                    {
                        error = "A shortcut must contain exactly one non-modifier key.";
                        return false;
                    }
                    key = token;
                    break;
            }
        }

        if (modifiers == HotkeyModifiers.None)
        {
            error = "Use at least one modifier: Ctrl, Shift, Alt, or Win.";
            return false;
        }

        if (key is null || !TryGetVirtualKey(key, out _))
        {
            error = "Use a letter, digit, F1–F24, or a supported navigation key.";
            return false;
        }

        gesture = new HotkeyGesture(modifiers, NormalizeKey(key));
        return true;
    }

    public bool TryGetVirtualKey(string key, out uint virtualKey)
    {
        virtualKey = 0;
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        {
            virtualKey = char.ToUpperInvariant(key[0]);
            return true;
        }

        if (key.Length is 2 or 3 && key[0] is 'F' or 'f' &&
            int.TryParse(key[1..], out var functionNumber) && functionNumber is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionNumber - 1);
            return true;
        }

        return NamedKeys.TryGetValue(key, out virtualKey);
    }

    public string? ValidateSet(HotkeySettings settings)
    {
        var gestures = new[] { settings.StartStop, settings.PauseResume, settings.SelectArea };
        foreach (var gesture in gestures)
        {
            if (gesture.Modifiers == HotkeyModifiers.None || !TryGetVirtualKey(gesture.Key, out _))
            {
                return $"{gesture} is not a valid global shortcut.";
            }
        }

        return gestures.Distinct().Count() == gestures.Length
            ? null
            : "Each action must use a different global shortcut.";
    }

    private static string NormalizeKey(string key)
    {
        if (key.Equals("Esc", StringComparison.OrdinalIgnoreCase)) return "Escape";
        return key.Length == 1 ? key.ToUpperInvariant() : key;
    }
}
