using System.Linq;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.MFME
{
    internal static class MFMEProfiles
    {
        public static readonly ComponentProfile[] All =
            V5.V5Profiles.All
                .Select(profile => profile.WithVersion(MfmeVersion.MFME))
                .ToArray();
    }
}
