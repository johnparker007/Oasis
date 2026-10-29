using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_0
{
    internal static class LampProfileV19_0
    {
        public static readonly ComponentProfile Profile =
            V19_8.LampProfileV19_8.Profile
                .WithVersion(MfmeVersion.V19_0)
                .WithBaseTags(CommonBaseTags.For(V19_8.LampProfileV19_8.Profile.Tags))
                .WithBaseOverrides(new TagMapOverrides(
                    new Dictionary<uint, TagInfo>
                    {
                        [0x1A] = new TagInfo(
                            0x04,
                            "LegacyCoinNoteId",
                            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF },
                            ValueRole.INT32),
                    }))
                .WithDefaults(new DefaultsProfile(
                    new Dictionary<string, object>(),
                    OrientationDefaultMode.Vertical,
                    LegacyCoinNoteFromBaseTag1A: true));
    }
}
