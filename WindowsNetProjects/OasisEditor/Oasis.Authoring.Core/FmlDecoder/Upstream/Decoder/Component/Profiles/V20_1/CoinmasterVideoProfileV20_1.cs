using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class CoinmasterVideoProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.CoinmasterVideo,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap(),
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options.Default,
            baseSection: BaseSectionProfile.SingleStream);
    }
}
