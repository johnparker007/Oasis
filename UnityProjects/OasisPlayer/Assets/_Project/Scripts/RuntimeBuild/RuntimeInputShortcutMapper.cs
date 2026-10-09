#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Converts Input Map/WPF key names without depending on Editor assemblies or raw MFME metadata.</summary>
    public static class RuntimeInputShortcutMapper
    {
        private static readonly Dictionary<string, KeyCode> Keys = CreateKeys();

        public static bool TryConvert(string shortcut, out KeyCode key)
        {
            key = KeyCode.None;
            return !string.IsNullOrWhiteSpace(shortcut) && Keys.TryGetValue(shortcut.Trim(), out key);
        }

        public static RuntimeInputDevelopmentControls.Binding[] CreateBindings(
            IEnumerable<MachineInputDefinition> inputs, Action<string> diagnostic)
        {
            var bindings = new List<RuntimeInputDevelopmentControls.Binding>();
            foreach (var input in inputs ?? Array.Empty<MachineInputDefinition>())
            {
                if (input == null) continue; // Logical declaration validation belongs to RuntimeMachine.
                var key = KeyCode.None;
                if (!string.IsNullOrWhiteSpace(input.keyboardShortcut) && !TryConvert(input.keyboardShortcut, out key))
                    diagnostic?.Invoke($"Machine input '{input.id}' has unsupported keyboard shortcut '{input.keyboardShortcut}'. "
                        + "Development binding uses None; logical input remains available. Assign a temporary key in Bindings or correct Input Map and rebuild.");
                bindings.Add(new RuntimeInputDevelopmentControls.Binding { key = key, inputId = input.id });
            }
            return bindings.ToArray();
        }

        private static Dictionary<string, KeyCode> CreateKeys()
        {
            // Mirror MfmeShortcutKeyMapper's normalized WPF names and single-key aliases.
            // Generic SHIFT/CTRL/ALT select the left key, as in Editor routing. No chord or numeric enum parsing.
            var keys = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < 26; i++) keys.Add(((char)('A' + i)).ToString(), (KeyCode)((int)KeyCode.A + i));
            for (var i = 0; i < 10; i++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha0 + i);
                Add(keys, key, i.ToString(), "D" + i, "Alpha" + i);
                Add(keys, (KeyCode)((int)KeyCode.Keypad0 + i), "NumPad" + i, "Keypad" + i);
            }
            for (var i = 1; i <= 15; i++) Add(keys, (KeyCode)((int)KeyCode.F1 + i - 1), "F" + i);
            Add(keys, KeyCode.Space, "Space");
            Add(keys, KeyCode.BackQuote, "Oem3", "OemTilde", "Oem8", "`");
            Add(keys, KeyCode.Minus, "OemMinus", "Minus", "-");
            Add(keys, KeyCode.Equals, "OemPlus", "Equals", "=");
            Add(keys, KeyCode.LeftBracket, "Oem4", "OemOpenBrackets", "OpenBrackets", "LeftBracket", "[");
            Add(keys, KeyCode.RightBracket, "Oem6", "OemCloseBrackets", "CloseBrackets", "RightBracket", "]");
            Add(keys, KeyCode.Semicolon, "Oem1", "OemSemicolon", "Semicolon", ";");
            Add(keys, KeyCode.Quote, "OemQuotes", "Quote", "'");
            Add(keys, KeyCode.Hash, "Oem7", "Hash", "#");
            Add(keys, KeyCode.Backslash, "Oem5", "OemPipe", "Oem102", "OemBackslash", "Backslash", "\\");
            Add(keys, KeyCode.Comma, "OemComma", "Comma", ",");
            Add(keys, KeyCode.Period, "OemPeriod", "Period", ".");
            Add(keys, KeyCode.Slash, "Oem2", "OemQuestion", "Slash", "/");
            Add(keys, KeyCode.LeftShift, "Shift", "LeftShift"); Add(keys, KeyCode.RightShift, "RightShift");
            Add(keys, KeyCode.LeftControl, "Ctrl", "LeftCtrl", "LeftControl"); Add(keys, KeyCode.RightControl, "RightCtrl", "RightControl");
            Add(keys, KeyCode.LeftAlt, "Alt", "LeftAlt"); Add(keys, KeyCode.RightAlt, "RightAlt");
            Add(keys, KeyCode.UpArrow, "Up", "UpArrow"); Add(keys, KeyCode.DownArrow, "Down", "DownArrow");
            Add(keys, KeyCode.LeftArrow, "Left", "LeftArrow"); Add(keys, KeyCode.RightArrow, "Right", "RightArrow");
            Add(keys, KeyCode.Return, "Return", "Enter"); Add(keys, KeyCode.Escape, "Escape", "Esc");
            Add(keys, KeyCode.Backspace, "Back", "Backspace"); Add(keys, KeyCode.Tab, "Tab");
            Add(keys, KeyCode.Delete, "Delete"); Add(keys, KeyCode.Insert, "Insert");
            Add(keys, KeyCode.Home, "Home"); Add(keys, KeyCode.End, "End");
            Add(keys, KeyCode.PageUp, "Prior", "PageUp"); Add(keys, KeyCode.PageDown, "Next", "PageDown");
            Add(keys, KeyCode.KeypadPlus, "Add", "KeypadPlus"); Add(keys, KeyCode.KeypadMinus, "Subtract", "KeypadMinus");
            Add(keys, KeyCode.KeypadMultiply, "Multiply", "KeypadMultiply"); Add(keys, KeyCode.KeypadDivide, "Divide", "KeypadDivide");
            Add(keys, KeyCode.KeypadPeriod, "Decimal", "KeypadPeriod"); Add(keys, KeyCode.KeypadEnter, "KeypadEnter");
            Add(keys, KeyCode.CapsLock, "Capital", "CapsLock"); Add(keys, KeyCode.Numlock, "NumLock");
            Add(keys, KeyCode.ScrollLock, "Scroll", "ScrollLock"); Add(keys, KeyCode.Pause, "Pause");
            return keys;
        }

        private static void Add(Dictionary<string, KeyCode> keys, KeyCode key, params string[] names)
        {
            foreach (var name in names) keys[name] = key;
        }
    }
}
#endif
