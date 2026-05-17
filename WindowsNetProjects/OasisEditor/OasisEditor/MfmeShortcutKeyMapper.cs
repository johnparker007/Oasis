using System.Windows.Input;

namespace OasisEditor;

public static class MfmeShortcutKeyMapper
{
    private static readonly Dictionary<Key, string> KeyToShortcut = new()
    {
        [Key.Space] = "SPACE",
        [Key.D1] = "1",
        [Key.D2] = "2",
        [Key.D3] = "3",
        [Key.D4] = "4",
        [Key.D5] = "5",
        [Key.D6] = "6",
        [Key.D7] = "7",
        [Key.D8] = "8",
        [Key.D9] = "9",
        [Key.D0] = "0",
        [Key.Oem3] = "`",
        [Key.Oem8] = "`",
        [Key.OemMinus] = "-",
        [Key.OemPlus] = "=",
        [Key.A] = "A",
        [Key.B] = "B",
        [Key.C] = "C",
        [Key.D] = "D",
        [Key.E] = "E",
        [Key.F] = "F",
        [Key.G] = "G",
        [Key.H] = "H",
        [Key.I] = "I",
        [Key.J] = "J",
        [Key.K] = "K",
        [Key.L] = "L",
        [Key.M] = "M",
        [Key.N] = "N",
        [Key.O] = "O",
        [Key.P] = "P",
        [Key.Q] = "Q",
        [Key.R] = "R",
        [Key.S] = "S",
        [Key.T] = "T",
        [Key.U] = "U",
        [Key.V] = "V",
        [Key.W] = "W",
        [Key.X] = "X",
        [Key.Y] = "Y",
        [Key.Z] = "Z",
        [Key.Oem4] = "[",
        [Key.Oem6] = "]",
        [Key.Oem1] = ";",
        [Key.OemQuotes] = "'",
        [Key.Oem7] = "#",
        [Key.LeftShift] = "SHIFT",
        [Key.RightShift] = "SHIFT",
        [Key.Oem5] = "\\",
        [Key.OemComma] = ",",
        [Key.OemPeriod] = ".",
        [Key.Oem2] = "/",
        [Key.LeftCtrl] = "CTRL",
        [Key.RightCtrl] = "CTRL",
        [Key.LeftAlt] = "ALT",
        [Key.RightAlt] = "ALT",
        [Key.Up] = "UP",
        [Key.Down] = "DOWN",
        [Key.Left] = "LEFT",
        [Key.Right] = "RIGHT"
    };

    public static bool TryMap(string? mfmeShortcut, out Key key)
    {
        key = Key.None;
        if (string.IsNullOrWhiteSpace(mfmeShortcut))
        {
            return false;
        }

        var value = mfmeShortcut.TrimEnd(' ').ToUpperInvariant();

        key = value switch
        {
            "SPACE" => Key.Space,
            "1" => Key.D1,
            "2" => Key.D2,
            "3" => Key.D3,
            "4" => Key.D4,
            "5" => Key.D5,
            "6" => Key.D6,
            "7" => Key.D7,
            "8" => Key.D8,
            "9" => Key.D9,
            "0" => Key.D0,
            "`" => Key.Oem3,
            "-" => Key.OemMinus,
            "=" => Key.OemPlus,
            "A" => Key.A,
            "B" => Key.B,
            "C" => Key.C,
            "D" => Key.D,
            "E" => Key.E,
            "F" => Key.F,
            "G" => Key.G,
            "H" => Key.H,
            "I" => Key.I,
            "J" => Key.J,
            "K" => Key.K,
            "L" => Key.L,
            "M" => Key.M,
            "N" => Key.N,
            "O" => Key.O,
            "P" => Key.P,
            "Q" => Key.Q,
            "R" => Key.R,
            "S" => Key.S,
            "T" => Key.T,
            "U" => Key.U,
            "V" => Key.V,
            "W" => Key.W,
            "X" => Key.X,
            "Y" => Key.Y,
            "Z" => Key.Z,
            "[" => Key.Oem4,
            "]" => Key.Oem6,
            ";" => Key.Oem1,
            "'" => Key.OemQuotes,
            "#" => Key.Oem7,
            "SHIFT" => Key.LeftShift,
            "\\" => Key.Oem5,
            "," => Key.OemComma,
            "." => Key.OemPeriod,
            "/" => Key.Oem2,
            "CTRL" => Key.LeftCtrl,
            "ALT" => Key.LeftAlt,
            "UP" => Key.Up,
            "DOWN" => Key.Down,
            "LEFT" => Key.Left,
            "RIGHT" => Key.Right,
            _ => Key.None
        };

        return key != Key.None;
    }

    public static bool TryMapKeyToMfmeShortcut(Key key, out string shortcut)
    {
        return KeyToShortcut.TryGetValue(key, out shortcut!);
    }

    public static string NormalizeShortcutForRouting(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return string.Empty;
        }

        var trimmed = shortcut.Trim();
        if (TryMap(trimmed, out var mappedKey) && TryMapKeyToMfmeShortcut(mappedKey, out var mappedShortcut))
        {
            return mappedShortcut;
        }

        if (Enum.TryParse<Key>(trimmed, ignoreCase: true, out var parsedKey)
            && TryMapKeyToMfmeShortcut(parsedKey, out var parsedShortcut))
        {
            return parsedShortcut;
        }

        return trimmed;
    }
}
