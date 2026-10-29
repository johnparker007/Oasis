using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8
{
    internal static class PrismLampProfileV19_8
    {
        public static readonly ComponentProfile Profile =
            (V20_1.PrismLampProfileV20_1.Profile with { Tags = BuildTags() })
                .WithVersion(MfmeVersion.V19_8)
                .AsLegacyTwoSection()
                .WithDefaults(new DefaultsProfile(
                    new Dictionary<string, object>(),
                    OrientationDefaultMode.Vertical,
                    MoveIsHorizontalToProperty: true));

        private static ComponentTagMap BuildTags()
        {
            ComponentTagMap tags = new(V20_1.PrismLampProfileV20_1.Profile.Tags);
            tags.Add(0x08, new TagInfo(0x01, "Unknown 0x08", new byte[] { 0x01 }, ValueRole.BYTE));
            return tags;
        }
    }
}
