using System.Collections.Generic;
using System.Linq;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5
{
    internal static class V5Profiles
    {
        public static readonly ComponentProfile[] All = Build();

        private static ComponentProfile[] Build()
        {
            List<ComponentProfile> profiles = V6_1.V6_1Profiles.All
                .Select(profile => profile.WithVersion(MfmeVersion.V5).WithOptions(profile.Options with { LegacyAnsiText = true }))
                .ToList();

            int lampIndex = profiles.FindIndex(profile => profile.Kind == MFMEComponentType.Lamp);
            profiles[lampIndex] = LampProfileV5.Profile;
            int labelIndex = profiles.FindIndex(profile => profile.Kind == MFMEComponentType.Label);
            profiles[labelIndex] = LabelProfileV5.Profile;
            int checkboxIndex = profiles.FindIndex(profile => profile.Kind == MFMEComponentType.Checkbox);
            profiles[checkboxIndex] = CheckboxProfileV5.Profile;
            int buttonIndex = profiles.FindIndex(profile => profile.Kind == MFMEComponentType.Button);
            profiles[buttonIndex] = ButtonProfileV5.Profile;
            int bfmLedIndex = profiles.FindIndex(profile => profile.Kind == MFMEComponentType.BFMLED);
            profiles[bfmLedIndex] = BFMLEDProfileV5.Profile;
            int bandReelIndex = profiles.FindIndex(profile => profile.Kind == MFMEComponentType.BandReel);
            profiles[bandReelIndex] = BandReelProfileV5.Profile;
            return profiles.ToArray();
        }
    }
}