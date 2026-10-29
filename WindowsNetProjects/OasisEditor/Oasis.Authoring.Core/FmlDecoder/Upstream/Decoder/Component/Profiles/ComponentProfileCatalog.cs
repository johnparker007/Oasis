using System;
using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_6;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_5;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_4;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_3;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_2;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_1;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_0;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_0;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_0;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V5;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V5_2;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.MFME;
using MfmeFmlDecoder.src.Decoder.Component.Profiles.V18_0;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    /// <summary>
    /// Explicit catalogue of supported (component, version) profiles. No nearest-version
    /// fallback: a missing pair fails resolution.
    /// </summary>
    internal static class ComponentProfileCatalog
    {
        private static readonly Dictionary<(MFMEComponentType Kind, MfmeVersion Version), ComponentProfile> Profiles = Build();

        public static ComponentProfile Resolve(MFMEComponentType kind, MfmeVersion version)
        {
            if (Profiles.TryGetValue((kind, version), out ComponentProfile profile))
            {
                return profile;
            }

            throw new UnsupportedComponentProfileException(kind, version);
        }

        public static bool TryResolve(MFMEComponentType kind, MfmeVersion version, out ComponentProfile profile)
        {
            return Profiles.TryGetValue((kind, version), out profile);
        }

        public static ComponentOrderingMode GetComponentOrdering(MfmeVersion version)
        {
            return version == MfmeVersion.V20_1
                ? ComponentOrderingMode.InFileOrder
                : ComponentOrderingMode.BordersLastReversed;
        }

        public static IReadOnlyCollection<(MFMEComponentType Kind, MfmeVersion Version)> Registered => Profiles.Keys;

        private static Dictionary<(MFMEComponentType Kind, MfmeVersion Version), ComponentProfile> Build()
        {
            var profiles = new Dictionary<(MFMEComponentType Kind, MfmeVersion Version), ComponentProfile>();
            void Register(ComponentProfile profile) => profiles[(profile.Kind, profile.Version)] = profile;

            Register(AceMatrixProfileV20_1.Profile);
            Register(AceMatrixProfileV19_8.Profile);
            Register(AceVideoProfileV20_1.Profile);
            Register(AceVideoProfileV19_8.Profile);
            Register(AlphaNewProfileV20_1.Profile);
            Register(AlphaNewProfileV19_8.Profile);
            Register(AlphaProfileV20_1.Profile);
            Register(AlphaProfileV19_8.Profile);
            Register(AstraMatrixProfileV20_1.Profile);
            Register(AstraMatrixProfileV19_8.Profile);
            Register(BackgroundProfileV20_1.Profile);
            Register(BackgroundProfileV19_8.Profile);
            Register(BandReelProfileV20_1.Profile);
            Register(BandReelProfileV19_8.Profile);
            Register(BarcrestBWBVideoProfileV20_1.Profile);
            Register(BarcrestBWBVideoProfileV19_8.Profile);
            Register(BFMAlphaProfileV20_1.Profile);
            Register(BFMAlphaProfileV19_8.Profile);
            Register(BFMColourLedProfileV20_1.Profile);
            Register(BFMColourLedProfileV19_8.Profile);
            Register(BFMLEDProfileV20_1.Profile);
            Register(BFMLEDProfileV19_8.Profile);
            Register(BFMVideoProfileV20_1.Profile);
            Register(BFMVideoProfileV19_8.Profile);
            Register(BitmapProfileV20_1.Profile);
            Register(BitmapProfileV19_8.Profile);
            Register(BorderProfileV20_1.Profile);
            Register(BorderProfileV19_8.Profile);
            Register(ButtonProfileV20_1.Profile);
            Register(ButtonProfileV19_8.Profile);
            Register(CheckboxProfileV20_1.Profile);
            Register(CheckboxProfileV19_8.Profile);
            Register(CoinmasterVideoProfileV20_1.Profile);
            Register(CoinmasterVideoProfileV19_8.Profile);
            Register(DiscReelProfileV20_1.Profile);
            Register(DiscReelProfileV19_8.Profile);
            Register(DotAlphaProfileV20_1.Profile);
            Register(DotAlphaProfileV19_8.Profile);
            Register(DotMatrixProfileV20_1.Profile);
            Register(DotMatrixProfileV19_8.Profile);
            Register(EpochDotAlphaProfileV20_1.Profile);
            Register(EpochDotAlphaProfileV19_8.Profile);
            Register(EpochMatrixProfileV20_1.Profile);
            Register(EpochMatrixProfileV19_8.Profile);
            Register(FlipReelProfileV20_1.Profile);
            Register(FlipReelProfileV19_8.Profile);
            Register(FrameProfileV20_1.Profile);
            Register(FrameProfileV19_8.Profile);
            Register(IGTVfdProfileV20_1.Profile);
            Register(IGTVfdProfileV19_8.Profile);
            Register(LabelProfileV20_1.Profile);
            Register(LabelProfileV19_8.Profile);
            Register(LampProfileV20_1.Profile);
            Register(LampProfileV19_8.Profile);
            Register(LedProfileV20_1.Profile);
            Register(LedProfileV19_8.Profile);
            Register(MatrixAlphaProfileV20_1.Profile);
            Register(MatrixAlphaProfileV19_8.Profile);
            Register(MaygayMatrixProfileV20_1.Profile);
            Register(MaygayMatrixProfileV19_8.Profile);
            Register(MaygayVideoProfileV20_1.Profile);
            Register(MaygayVideoProfileV19_8.Profile);
            Register(PlasmaDisplayProfileV20_1.Profile);
            Register(PlasmaDisplayProfileV19_8.Profile);
            Register(PrismLampProfileV20_1.Profile);
            Register(PrismLampProfileV19_8.Profile);
            Register(ProconnMatrixProfileV20_1.Profile);
            Register(ProconnMatrixProfileV19_8.Profile);
            Register(ReelBonusReelProfileV20_1.Profile);
            Register(ReelBonusReelProfileV19_8.Profile);
            Register(ReelProfileV20_1.Profile);
            Register(ReelProfileV19_8.Profile);
            Register(RGBLedProfileV20_1.Profile);
            Register(RGBLedProfileV19_8.Profile);
            Register(SevenSegBlockProfileV20_1.Profile);
            Register(SevenSegBlockProfileV19_8.Profile);
            Register(SevenSegProfileV20_1.Profile);
            Register(SevenSegProfileV19_8.Profile);

            foreach (ComponentProfile profile in V19_6Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V19_5Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V19_4Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V19_3Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V19_2Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V19_1Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V19_0Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V20_0Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V6_1Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V6_0Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V5_2Profiles.All) Register(profile);
            foreach (ComponentProfile profile in V5Profiles.All) Register(profile);
            Register(LampProfileV5.Profile);
            foreach (ComponentProfile profile in MFMEProfiles.All) Register(profile);
            foreach (ComponentProfile profile in V18_0Profiles.All) Register(profile);
            return profiles;
        }
    }
}
