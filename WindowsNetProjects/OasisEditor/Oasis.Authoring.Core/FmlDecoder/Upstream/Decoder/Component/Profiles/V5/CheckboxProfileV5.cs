using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5
{
    internal static class CheckboxProfileV5
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.CheckboxProfileV19_8.Profile.WithVersion(MfmeVersion.V5);
            ComponentTagMap first = new(legacy.Tags);
            first.Add(0x17, new TagInfo(0x01, "Unknown 0x17", new byte[] { 0x00 }, ValueRole.BOOLEAN));
            first.Add(0x18, new TagInfo(0x04, "Unknown 0x18", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32));
            first.Add(0x28, new TagInfo(0x00, "Label", System.Array.Empty<byte>(), ValueRole.TEXT));

            return legacy with
            {
                BaseTags = first,
                BaseSection = BaseSectionProfile.LegacyTwoSection(legacy.Tags),
                Options = legacy.Options with { LegacyAnsiText = true },
            };
        }
    }
}
