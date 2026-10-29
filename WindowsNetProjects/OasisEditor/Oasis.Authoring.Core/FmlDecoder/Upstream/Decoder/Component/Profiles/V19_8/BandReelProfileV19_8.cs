using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8
{
    internal static class BandReelProfileV19_8
    {
        public static readonly ComponentProfile Profile =
            (V20_1.BandReelProfileV20_1.Profile with { Tags = BuildTags() })
                .WithVersion(MfmeVersion.V19_8)
                .AsLegacyTwoSection()
                .WithDefaults(new DefaultsProfile(
                    new Dictionary<string, object>(),
                    OrientationDefaultMode.Horizontal,
                    DefaultUndefinedMaskSlotsToMinusTwo: true));

        private static ComponentTagMap BuildTags()
        {
            ComponentTagMap tags = new(V20_1.BandReelProfileV20_1.Profile.Tags);
            tags.Add(0x0A, new TagInfo(0x01, "Unknown 0x0A", new byte[] { 0x01 }, ValueRole.BYTE));
            tags.Add(0x0B, new TagInfo(0x01, "Unknown 0x0B", new byte[] { 0x00 }, ValueRole.BYTE));
            tags.Add(0x0F, new TagInfo(0x01, "Unknown 0x0F", new byte[] { 0x00 }, ValueRole.BYTE));
            tags.Add(0x11, new TagInfo(0x01, "Unknown 0x11", new byte[] { 0x00 }, ValueRole.BYTE));
            tags.Add(0x14, new TagInfo(0x04, "Unknown 0x14", new byte[] { 0x00 }, ValueRole.UINT32));
            tags.Add(0x15, new TagInfo(0x01, "Unknown 0x15", new byte[] { 0x00 }, ValueRole.BYTE));
            return tags;
        }
    }
}
