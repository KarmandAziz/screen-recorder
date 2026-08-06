namespace LocalScreenRecorder.Core.Models;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(Key.ToUpperInvariant());
        return string.Join("+", parts);
    }
}

public sealed record HotkeySettings
{
    public HotkeyGesture StartStop { get; init; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "R");
    public HotkeyGesture PauseResume { get; init; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "P");
    public HotkeyGesture SelectArea { get; init; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "A");
}
