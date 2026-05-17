using System.Windows.Input;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeShortcutKeyMapperTests
{
    [Theory]
    [InlineData("SPACE", Key.Space)]
    [InlineData("1", Key.D1)]
    [InlineData("A", Key.A)]
    [InlineData("RIGHT", Key.Right)]
    [InlineData("ctrl", Key.LeftCtrl)]
    [InlineData("ALT ", Key.LeftAlt)]
    [InlineData("`", Key.Oem3)]
    public void TryMap_WithSupportedShortcuts_ReturnsMappedKey(string raw, Key expected)
    {
        var ok = MfmeShortcutKeyMapper.TryMap(raw, out var key);

        Assert.True(ok);
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("F1")]
    [InlineData("Shift+3")]
    public void TryMap_WithUnsupportedShortcuts_ReturnsFalse(string? raw)
    {
        var ok = MfmeShortcutKeyMapper.TryMap(raw, out var key);

        Assert.False(ok);
        Assert.Equal(Key.None, key);
    }

    [Fact]
    public void TryMapKeyToMfmeShortcut_WithOem3_ReturnsBacktick()
    {
        var ok = MfmeShortcutKeyMapper.TryMapKeyToMfmeShortcut(Key.Oem3, out var shortcut);

        Assert.True(ok);
        Assert.Equal("`", shortcut);
    }

    [Fact]
    public void TryMapKeyToMfmeShortcut_WithOem8_ReturnsBacktick()
    {
        var ok = MfmeShortcutKeyMapper.TryMapKeyToMfmeShortcut(Key.Oem8, out var shortcut);

        Assert.True(ok);
        Assert.Equal("`", shortcut);
    }

    [Theory]
    [InlineData("D1", "1")]
    [InlineData("D2", "2")]
    [InlineData("Oem3", "`")]
    [InlineData("Space", "SPACE")]
    [InlineData("RIGHT", "RIGHT")]
    public void NormalizeShortcutForRouting_WithMappedShortcut_ReturnsCanonicalToken(string raw, string expected)
    {
        var normalized = MfmeShortcutKeyMapper.NormalizeShortcutForRouting(raw);

        Assert.Equal(expected, normalized);
    }
}
