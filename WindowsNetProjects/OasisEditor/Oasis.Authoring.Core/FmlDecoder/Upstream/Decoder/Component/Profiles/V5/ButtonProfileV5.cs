using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5
{
    internal static class ButtonProfileV5
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V6_1.ButtonProfileV6_1.Profile.WithVersion(MfmeVersion.V5);
            ComponentTagMap tags = new(legacy.Tags);
            tags.Add(0x28, new TagInfo(0x00, "Label (UTF-16)", System.Array.Empty<byte>(), ValueRole.TEXT));

            return legacy
                .WithTags(tags)
                .WithOptions(legacy.Options with { LegacyAnsiText = true })
                .WithDefaults(legacy.Defaults with { SynthesizeButtonLamp1 = true, ClearDuplicateButtonShortcut2 = true, LegacyBbTokenEffects = true });
        }
    }
}
