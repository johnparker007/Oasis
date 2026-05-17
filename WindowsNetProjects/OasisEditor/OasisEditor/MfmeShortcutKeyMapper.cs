using System.Windows.Input;

namespace OasisEditor;

public static class MfmeShortcutKeyMapper
{
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
}
