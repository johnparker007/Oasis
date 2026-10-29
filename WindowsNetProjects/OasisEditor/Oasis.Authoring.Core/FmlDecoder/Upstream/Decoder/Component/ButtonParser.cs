using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using System.Linq;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Decoder.Component.Helper;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class ButtonParser : ComponentParserBase<Button>
    {
        private Dictionary<uint, string> shapeOptions = new Dictionary<uint, string>
        {
            { 0x00, "Rectangle" },
            { 0x01, "Triangle Up" },
            { 0x02, "Triangle Down" },
            { 0x03, "Triangle Left" },
            { 0x04, "Triangle Right" },
            { 0x05, "Ellipse" },
        };


        public Button Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Button, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult.ValuesByTag, shapeOptions, profile.Defaults);

            if (profile.Defaults?.ClearDuplicateButtonShortcut2 == true
                && component.UInt32s.TryGetValue("Shortcut 1", out uint shortcut1)
                && component.UInt32s.TryGetValue("Shortcut 2", out uint shortcut2)
                && shortcut1 == shortcut2
                && component.Booleans.TryGetValue("Shortcut 2 Enabled", out bool shortcut2Enabled)
                && shortcut2Enabled)
            {
                component.UInt32s["Shortcut 2"] = 0;
                component.Booleans["Shortcut 2 Enabled"] = false;
            }

            if (profile.Defaults?.SynthesizeButtonLamp1 == true
                && !component.Images.ContainsKey("lamp_1_image")
                && component.Images.ContainsKey("lamp_2_image")
                && component.Booleans.TryGetValue("Graphic", out bool graphic)
                && graphic)
            {
                int width = checked((int)component.Width);
                int height = checked((int)component.Height);
                component.Images["lamp_1_image"] = new BitmapEntry(
                    width,
                    height,
                    32,
                    new byte[checked(width * height * 4)])
                {
                    Purpose = "lamp_1_image",
                };
            }

            if (profile.Defaults?.SynthesizeButtonLamp1 == true
                && !component.Images.ContainsKey("off_image")
                && component.Images.ContainsKey("lamp_1_image")
                && component.Images.ContainsKey("lamp_2_image")
                && component.Booleans.TryGetValue("Graphic", out bool graphicOff)
                && graphicOff)
            {
                int width = checked((int)component.Width);
                int height = checked((int)component.Height);
                component.Images["off_image"] = new BitmapEntry(
                    width,
                    height,
                    32,
                    new byte[checked(width * height * 4)])
                {
                    Purpose = "off_image",
                };
            }

            return component;
        }

        private static void ApplyExtendedTags(
            Button component,
            System.Collections.Generic.IReadOnlyDictionary<uint, object> valuesByTag,
            Dictionary<uint, string> shapeOptions,
            DefaultsProfile defaults)
        {
            if ((valuesByTag.TryGetValue(0x49, out var selectedCoinNoteId)) && (valuesByTag.TryGetValue(0x48, out var selectedEffectId)))
            {
                component.Strings["SelectedCoinNote"] = CoinNoteHierarchy.ResolveCoinNote((int)selectedCoinNoteId, (uint)selectedEffectId);
                component.Strings["SelectedEffect"] = CoinNoteHierarchy.ResolveEffect((int)selectedCoinNoteId, (uint)selectedEffectId);
            }
            else if (valuesByTag.TryGetValue(0x1A, out var legacyCoinNoteId)
                && TryGetInt32(legacyCoinNoteId, out int legacyNote)
                && (ComponentHasSelectedCoin(valuesByTag) || LegacyCoinNoteResolver.IsCanonical(legacyNote)))
            {
                uint effectId = LegacyCoinNoteResolver.ResolveEffectId(ref legacyNote);
                if (effectId == 0)
                {
                    effectId = defaults?.LegacyTokenEffectDefaults == true
                        ? LegacyCoinNoteResolver.ResolveTokenDefaultEffect(legacyNote, defaults?.LegacyBbTokenEffects == true)
                        : LegacyCoinNoteResolver.ResolveDefaultEffect(legacyNote);
                }

                component.Strings["SelectedCoinNote"] = CoinNoteHierarchy.ResolveCoinNote(legacyNote, effectId);
                component.Strings["SelectedEffect"] = CoinNoteHierarchy.ResolveEffect(legacyNote, effectId);
            }
            component.Int32s.Remove("SelectedCoinNoteId");
            component.UInt32s.Remove("SelectedEffectId");
            if (valuesByTag.TryGetValue(0x01, out var selectedShapeId)) component.Strings.Add("SelectedShape", shapeOptions[(uint)selectedShapeId]);

            if (valuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
            }
        }

        private static bool ComponentHasSelectedCoin(IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (!valuesByTag.TryGetValue(0x19, out object selected))
            {
                return false;
            }

            switch (selected)
            {
                case bool b: return b;
                case byte by: return by != 0;
                case uint u: return u != 0;
                case int i: return i != 0;
                default: return false;
            }
        }

        private static bool TryGetInt32(object value, out int result)
        {
            switch (value)
            {
                case int i: result = i; return true;
                case uint u when u <= int.MaxValue: result = (int)u; return true;
                case ushort us: result = us; return true;
                case byte b: result = b; return true;
                default: result = 0; return false;
            }
        }
    }
}