using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class ButtonProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.ButtonProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);

            ComponentTagMap tags = new(legacy.Tags);
            tags.Remove(0x49);
            tags.Add(0x1A, new TagInfo(0x04, "SelectedCoinNoteId", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, ValueRole.INT32));

            return legacy
                .WithTags(tags)
                .WithDefaults(new DefaultsProfile(
                    new Dictionary<string, object>
                    {
                        ["SelectedCoinNote"] = string.Empty,
                        ["SelectedEffect"] = string.Empty,
                    },
                    legacy.Defaults.Orientation,
                    LegacyTokenEffectDefaults: true));
        }
    }
}
