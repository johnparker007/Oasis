using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Decoder.Component.Helper;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class LampParser : ComponentParserBase<Lamp>
    {

        public Lamp Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Lamp, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);
            ApplyExtendedTags(component, parseResult, profile.Defaults);

            if (profile.Defaults?.ClearDuplicateLampShortcut2 == true
                && component.UInt32s.TryGetValue("Shortcut 1", out uint shortcut1)
                && component.UInt32s.TryGetValue("Shortcut 2", out uint shortcut2)
                && shortcut1 == shortcut2
                && component.Booleans.TryGetValue("Shortcut 2 Enabled", out bool shortcut2Enabled)
                && shortcut2Enabled)
            {
                component.UInt32s["Shortcut 2"] = 0;
                component.Booleans["Shortcut 2 Enabled"] = false;
            }

            return component;
        }

        private static void ApplyExtendedTags(
            Lamp component,
            ExtendedTagParser.ParseResult parseResult,
            DefaultsProfile defaults)
        {
            IReadOnlyDictionary<uint, object> valuesByTag = parseResult.ValuesByTag;
            if ((valuesByTag.TryGetValue(0x49, out var selectedCoinNoteId)) && (valuesByTag.TryGetValue(0x48, out var selectedEffectId)))
            {
                component.Strings.Add("SelectedCoinNote", CoinNoteHierarchy.ResolveCoinNote((int)selectedCoinNoteId, (uint)selectedEffectId));
                component.Strings.Add("SelectedEffect", CoinNoteHierarchy.ResolveEffect((int)selectedCoinNoteId, (uint)selectedEffectId));
            }
            component.Int32s.Remove("SelectedCoinNoteId");
            component.Int32s.Remove("LegacyCoinNoteId");
            component.UInt32s.Remove("SelectedEffectId");

            if (defaults?.LegacyCoinNoteFromBaseTag1A == true
                && parseResult.BaseSectionValuesByTag is not null
                && parseResult.BaseSectionExplicitTags?.Contains(0x1A) == true
                && parseResult.BaseSectionValuesByTag.TryGetValue(0x1A, out object legacyNoteValue)
                && TryGetInt32(legacyNoteValue, out int legacyNoteId))
            {
                bool isCanonical = IsCanonicalLegacyCoinNote(legacyNoteId);
                bool coinSelected = false;
                if (parseResult.BaseSectionValuesByTag.TryGetValue(0x19, out object coinSelectedValue)
                    && TryGetUInt32(coinSelectedValue, out uint selectedFlag))
                {
                    coinSelected = selectedFlag != 0;
                }

                if (coinSelected || isCanonical)
                {
                    uint legacyEffectId = ResolveLegacyEffectId(ref legacyNoteId);
                    if (legacyEffectId == 0)
                    {
                        legacyEffectId = defaults.LegacyTokenEffectDefaults
                            ? LegacyCoinNoteResolver.ResolveTokenDefaultEffect(legacyNoteId, defaults.LegacyBbTokenEffects)
                            : ResolveLegacyDefaultEffect(legacyNoteId);
                    }

                    component.Strings["SelectedCoinNote"] = CoinNoteHierarchy.ResolveCoinNote(legacyNoteId, legacyEffectId);
                    component.Strings["SelectedEffect"] = CoinNoteHierarchy.ResolveEffect(legacyNoteId, legacyEffectId);
                }
            }

            if (valuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
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

        private static bool TryGetUInt32(object value, out uint result)
        {
            switch (value)
            {
                case uint u: result = u; return true;
                case int i when i >= 0: result = (uint)i; return true;
                case ushort us: result = us; return true;
                case byte b: result = b; return true;
                case bool boolean: result = boolean ? 1u : 0u; return true;
                default: result = 0; return false;
            }
        }

        private static bool IsCanonicalLegacyCoinNote(int coinNoteId)
        {
            return coinNoteId == 0x43
                || coinNoteId == 0x44
                || coinNoteId == 0x45
                || coinNoteId == 0x49
                || coinNoteId == 0x4A;
        }

        private static uint ResolveLegacyEffectId(ref int coinNoteId)
        {
            switch (coinNoteId)
            {
                case 0x43: coinNoteId = 0x47; return 1;
                case 0x44: coinNoteId = 0x47; return 5;
                case 0x45: coinNoteId = 0x47; return 6;
                case 0x46:
                case 0x47:
                case 0x48:
                    coinNoteId -= 3;
                    return 0;
                case 0x49: coinNoteId = 0x47; return 3;
                case 0x4A: coinNoteId = 0x47; return 4;
                default: return 0;
            }
        }

        private static uint ResolveLegacyDefaultEffect(int coinNoteId)
        {
            CoinNoteOption option = CoinNoteHierarchy.Options
                .FirstOrDefault(o => o.Index == coinNoteId);
            if (option is null)
            {
                return 0;
            }

            EffectOption effect = option.Effects.FirstOrDefault(e => e.Index != 0)
                ?? option.Effects.FirstOrDefault();
            return effect is null ? 0 : (uint)effect.Index;
        }
    }
}
