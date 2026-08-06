using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.Tests;

public sealed class HotkeyValidatorTests
{
    private readonly HotkeyValidator _validator = new();

    [Fact]
    public void TryParse_AcceptsDefaultShortcut()
    {
        var success = _validator.TryParse("Ctrl + Shift + R", out var gesture, out var error);

        Assert.True(success, error);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, gesture.Modifiers);
        Assert.Equal("R", gesture.Key);
        Assert.Equal("Ctrl+Shift+R", gesture.ToString());
    }

    [Theory]
    [InlineData("R")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+R+P")]
    public void TryParse_RejectsUnsafeOrAmbiguousShortcuts(string text)
    {
        Assert.False(_validator.TryParse(text, out _, out _));
    }

    [Fact]
    public void ValidateSet_RejectsDuplicateShortcuts()
    {
        var duplicate = new HotkeyGesture(HotkeyModifiers.Control, "F9");
        var settings = new HotkeySettings { StartStop = duplicate, PauseResume = duplicate };

        Assert.NotNull(_validator.ValidateSet(settings));
    }
}
