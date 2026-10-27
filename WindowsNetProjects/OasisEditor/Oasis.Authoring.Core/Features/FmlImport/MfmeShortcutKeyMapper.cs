namespace OasisEditor;

/// <summary>Maps legacy MFME shortcut labels to portable key names without WPF input types.</summary>
internal static class MfmeShortcutNameMapper
{
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SPACE"] = "Space", ["1"] = "D1", ["2"] = "D2", ["3"] = "D3", ["4"] = "D4", ["5"] = "D5",
        ["6"] = "D6", ["7"] = "D7", ["8"] = "D8", ["9"] = "D9", ["0"] = "D0",
        ["`"] = "Oem3", ["-"] = "OemMinus", ["="] = "OemPlus", ["["] = "Oem4", ["]"] = "Oem6",
        [";"] = "Oem1", ["'"] = "OemQuotes", ["#"] = "Oem7", ["\\"] = "Oem5", [","] = "OemComma",
        ["."] = "OemPeriod", ["/"] = "Oem2", ["SHIFT"] = "LeftShift", ["CTRL"] = "LeftCtrl", ["ALT"] = "LeftAlt",
        ["UP"] = "Up", ["DOWN"] = "Down", ["LEFT"] = "Left", ["RIGHT"] = "Right",
        ["A"] = "A", ["B"] = "B", ["C"] = "C", ["D"] = "D", ["E"] = "E", ["F"] = "F", ["G"] = "G",
        ["H"] = "H", ["I"] = "I", ["J"] = "J", ["K"] = "K", ["L"] = "L", ["M"] = "M", ["N"] = "N",
        ["O"] = "O", ["P"] = "P", ["Q"] = "Q", ["R"] = "R", ["S"] = "S", ["T"] = "T", ["U"] = "U",
        ["V"] = "V", ["W"] = "W", ["X"] = "X", ["Y"] = "Y", ["Z"] = "Z"
    };

    public static bool TryMapShortcutName(string? value, out string keyName)
    {
        keyName = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Names.TryGetValue(value.TrimEnd(' '), out keyName!);
    }
}
