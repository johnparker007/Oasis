using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8
{
    internal static class RGBLedProfileV19_8
    {
        public static readonly ComponentProfile Profile =
            V20_1.RGBLedProfileV20_1.Profile
                .WithVersion(MfmeVersion.V19_8)
                .AsLegacyTwoSection()
                .WithDefaults(new DefaultsProfile(
                    new Dictionary<string, object>(),
                    OrientationDefaultMode.Vertical,
                    DefaultWhiteLampNumberZero: true));
    }
}
