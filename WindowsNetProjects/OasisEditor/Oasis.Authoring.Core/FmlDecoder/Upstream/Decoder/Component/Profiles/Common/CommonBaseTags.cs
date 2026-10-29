using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    /// <summary>
    /// The shared MFME base loader tag map. Components override roles or names through
    /// their base profile, and versions apply deltas through <see cref="TagMapOverrides"/>.
    /// </summary>
    internal static class CommonBaseTags
    {
        public static ComponentTagMap For(ComponentTagMap classTags)
        {
            return BaseSectionTagMapBuilder.Build(classTags).Map;
        }
    }
}
