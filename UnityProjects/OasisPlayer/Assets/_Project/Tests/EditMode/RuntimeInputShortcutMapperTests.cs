#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeInputShortcutMapperTests
    {
        [TestCase("R", KeyCode.R)] [TestCase(" n ", KeyCode.N)]
        [TestCase("Space", KeyCode.Space)] [TestCase("SPACE", KeyCode.Space)]
        [TestCase("D0", KeyCode.Alpha0)] [TestCase("D9", KeyCode.Alpha9)]
        [TestCase("1", KeyCode.Alpha1)] [TestCase("Alpha2", KeyCode.Alpha2)]
        [TestCase("Left", KeyCode.LeftArrow)] [TestCase("RIGHT", KeyCode.RightArrow)]
        [TestCase("Up", KeyCode.UpArrow)] [TestCase("DownArrow", KeyCode.DownArrow)]
        [TestCase("Oem3", KeyCode.BackQuote)] [TestCase("Oem8", KeyCode.BackQuote)]
        [TestCase("OemMinus", KeyCode.Minus)] [TestCase("OemPlus", KeyCode.Equals)]
        [TestCase("Oem4", KeyCode.LeftBracket)] [TestCase("Oem6", KeyCode.RightBracket)]
        [TestCase("Oem1", KeyCode.Semicolon)] [TestCase("OemQuotes", KeyCode.Quote)]
        [TestCase("Oem7", KeyCode.Hash)] [TestCase("Oem5", KeyCode.Backslash)]
        [TestCase("OemComma", KeyCode.Comma)] [TestCase("OemPeriod", KeyCode.Period)]
        [TestCase("Oem2", KeyCode.Slash)] [TestCase("OemOpenBrackets", KeyCode.LeftBracket)]
        [TestCase("`", KeyCode.BackQuote)] [TestCase("-", KeyCode.Minus)]
        [TestCase("=", KeyCode.Equals)] [TestCase("[", KeyCode.LeftBracket)]
        [TestCase("]", KeyCode.RightBracket)] [TestCase(";", KeyCode.Semicolon)]
        [TestCase("'", KeyCode.Quote)] [TestCase("#", KeyCode.Hash)]
        [TestCase("\\", KeyCode.Backslash)] [TestCase(",", KeyCode.Comma)]
        [TestCase(".", KeyCode.Period)] [TestCase("/", KeyCode.Slash)]
        [TestCase("SHIFT", KeyCode.LeftShift)] [TestCase("RightShift", KeyCode.RightShift)]
        [TestCase("CTRL", KeyCode.LeftControl)] [TestCase("RightCtrl", KeyCode.RightControl)]
        [TestCase("ALT", KeyCode.LeftAlt)] [TestCase("RightAlt", KeyCode.RightAlt)]
        [TestCase("F1", KeyCode.F1)] [TestCase("F15", KeyCode.F15)]
        [TestCase("NumPad0", KeyCode.Keypad0)] [TestCase("Keypad9", KeyCode.Keypad9)]
        [TestCase("Return", KeyCode.Return)] [TestCase("Escape", KeyCode.Escape)]
        [TestCase("Back", KeyCode.Backspace)] [TestCase("PageUp", KeyCode.PageUp)]
        [TestCase("Add", KeyCode.KeypadPlus)] [TestCase("Decimal", KeyCode.KeypadPeriod)]
        public void SupportedEditorAndImporterNamesConvert(string shortcut, KeyCode expected)
        {
            Assert.True(RuntimeInputShortcutMapper.TryConvert(shortcut, out var key));
            Assert.AreEqual(expected, key);
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        [TestCase("Ctrl+R")] [TestCase("R,N")] [TestCase("unknown")]
        [TestCase("114")] [TestCase("-1")] [TestCase("Mouse0")]
        [TestCase("JoystickButton0")] [TestCase("None")] [TestCase("F16")]
        public void UnsupportedValuesNeverBecomeAnotherKey(string shortcut)
        {
            Assert.False(RuntimeInputShortcutMapper.TryConvert(shortcut, out var key));
            Assert.AreEqual(KeyCode.None, key);
        }

        [Test]
        public void AllDeclarationsRemainVisibleAndUnsupportedShortcutIdentifiesItsLogicalId()
        {
            var warnings = new List<string>();
            // Extra exported metadata is intentionally ignored; raw MFME is never a fallback key.
            var inputs = JsonUtility.FromJson<MachineRuntimeManifest>(
                "{\"inputs\":[{\"id\":\"rerack\",\"name\":\"Return ball\",\"keyboardShortcut\":\"R\"},"
                + "{\"id\":\"unassigned\",\"rawMfmeShortcut\":\"N\",\"keyboardShortcut\":\"\"},"
                + "{\"id\":\"unsupported\",\"keyboardShortcut\":\"Ctrl+R\"}]}").inputs;
            var bindings = RuntimeInputShortcutMapper.CreateBindings(inputs, warnings.Add);
            Assert.AreEqual(new[] { "rerack", "unassigned", "unsupported" }, bindings.Select(b => b.inputId));
            Assert.AreEqual(new[] { KeyCode.R, KeyCode.None, KeyCode.None }, bindings.Select(b => b.key));
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("unsupported", warnings[0]); StringAssert.Contains("Ctrl+R", warnings[0]);
            StringAssert.Contains("logical input remains available", warnings[0]);
        }
    }
}
#endif
