using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    internal static class ComponentProfileFactory
    {
        public static ComponentProfile Create(
            MFMEComponentType kind,
            MfmeVersion version,
            ComponentTagMap tags,
            AngleProfile angle,
            ExtendedTagParser.Options options = null,
            BaseSectionProfile baseSection = null,
            IReadOnlyList<string> valueOrder = null,
            IReadOnlyList<string> imageOrder = null,
            DefaultsProfile defaults = null,
            ComponentTagMap baseTags = null,
            TagMapOverrides baseOverrides = null,
            TagMapOverrides classOverrides = null)
        {
            return new ComponentProfile(
                version,
                kind,
                tags ?? new ComponentTagMap(),
                baseSection ?? BaseSectionProfile.SingleStream,
                angle ?? AngleProfile.Default,
                options ?? ExtendedTagParser.Options.Default,
                valueOrder ?? ComponentValueOrder.Empty,
                imageOrder ?? ComponentValueOrder.Empty,
                defaults ?? DefaultsProfile.Empty,
                baseTags,
                baseOverrides,
                classOverrides);
        }
    }
}
