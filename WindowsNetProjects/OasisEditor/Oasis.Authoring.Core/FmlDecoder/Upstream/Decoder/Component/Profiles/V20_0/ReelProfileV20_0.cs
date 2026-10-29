using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_0
{
    internal static class ReelProfileV20_0
    {
        public static readonly ComponentProfile Profile =
            (V20_1.ReelProfileV20_1.Profile with
            {
                Tags = BuildTags(),
                Options = ExtendedTagParser.Options.WithBitmapTags(
                    0x0E, 0x0F, 0x05, 0x10, 0x19, 0x28, 0x1C, 0x36),
            }).WithVersion(MfmeVersion.V20_0);

        private static ComponentTagMap BuildTags()
        {
            ComponentTagMap inner = new(V20_1.ReelProfileV20_1.Profile.Tags.NestedTagBlockMap);
            inner.Add(0x10, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP));

            ComponentTagMap outer = new(V20_1.ReelProfileV20_1.Profile.Tags);
            return outer.WithNestedTagBlock(inner);
        }
    }
}
