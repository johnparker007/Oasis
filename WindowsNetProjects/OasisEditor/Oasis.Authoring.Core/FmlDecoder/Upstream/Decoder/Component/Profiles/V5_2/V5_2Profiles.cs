using System.Linq;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5_2
{
    internal static class V5_2Profiles
    {
        public static readonly ComponentProfile[] All =
            V5.V5Profiles.All
                .Select(profile => profile.WithVersion(MfmeVersion.V5_2))
                .ToArray();
    }
}
