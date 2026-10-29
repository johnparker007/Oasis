using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8
{
    internal static class EpochDotAlphaProfileV19_8
    {
        public static readonly ComponentProfile Profile =
            V20_1.EpochDotAlphaProfileV20_1.Profile
                .WithVersion(MfmeVersion.V19_8)
                .AsLegacyTwoSection()
                .WithOrientationRule(OrientationDefaultMode.Vertical);
    }
}
