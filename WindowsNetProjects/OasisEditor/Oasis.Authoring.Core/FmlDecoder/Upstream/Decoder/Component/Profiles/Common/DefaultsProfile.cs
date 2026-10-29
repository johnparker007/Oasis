using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    internal enum OrientationDefaultMode
    {
        None,
        Vertical,
        Horizontal,
        ReelAspect,
    }

    /// <summary>
    /// Model-level defaults that are not carried by a TLV tag default. They are
    /// applied by the parser after the tag maps have been resolved.
    /// </summary>
    internal sealed record DefaultsProfile(
        IReadOnlyDictionary<string, object> Values,
        OrientationDefaultMode Orientation = OrientationDefaultMode.None,
        bool DefaultWhiteLampNumberZero = false,
        bool OverrideOrientation = false,
        bool DefaultUndefinedMaskSlotsToMinusTwo = false,
        bool MoveIsHorizontalToProperty = false,
        bool LegacyCoinNoteFromBaseTag1A = false,
        bool LegacyTokenEffectDefaults = false,
        bool LegacyScaledSkew = false,
        bool UseNormalizedExtendedData = false,
        bool SynthesizeOffImage = false,
        bool DiscReelLampCountFromStops = false,
        bool DotAlphaAutoSize = false,
        bool DotMatrixAutoSize = false,
        bool SynthesizeButtonLamp1 = false,
        bool BfmLedSizeFromXSize = false,
        bool ClearDuplicateButtonShortcut2 = false,
        bool ClearDuplicateLampShortcut2 = false,
        bool LegacyBbTokenEffects = false,
        bool SequentialBandReelMaskSlots = false)
    {
        public static readonly DefaultsProfile Empty =
            new(new Dictionary<string, object>());

        public bool TryGetValue(string key, out object value)
        {
            if (Values is not null && Values.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }
    }
}
