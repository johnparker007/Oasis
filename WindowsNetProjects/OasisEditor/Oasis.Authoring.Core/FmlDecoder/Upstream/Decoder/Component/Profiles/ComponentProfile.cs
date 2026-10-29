using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    /// <summary>
    /// Declarative data for one (component, version) pair. Parsers hold behaviour only.
    /// There is no nearest-version fallback: every supported pair is registered explicitly.
    ///
    /// Tags are split by scope:
    ///   BaseTags  = the shared base loader map plus component/version overrides
    ///   Tags      = the component class map
    /// A tag id may exist in both scopes with different meanings.
    /// </summary>
    internal sealed record ComponentProfile(
        MfmeVersion Version,
        MFMEComponentType Kind,
        ComponentTagMap Tags,
        BaseSectionProfile BaseSection,
        AngleProfile Angle,
        ExtendedTagParser.Options Options,
        IReadOnlyList<string> ValueOrder,
        IReadOnlyList<string> ImageOrder,
        DefaultsProfile Defaults,
        ComponentTagMap BaseTags = null,
        TagMapOverrides BaseOverrides = null,
        TagMapOverrides ClassOverrides = null)
    {
        public ComponentTagMap NestedTags => Tags?.NestedTagBlockMap;

        public ComponentTagMap EffectiveClassTags =>
            ClassOverrides is null || ClassOverrides == TagMapOverrides.Empty
                ? Tags
                : ClassOverrides.Apply(Tags);

        public ComponentTagMap EffectiveBaseTags
        {
            get
            {
                ComponentTagMap source = BaseTags ?? Tags;
                return BaseOverrides is null || BaseOverrides == TagMapOverrides.Empty
                    ? source
                    : BaseOverrides.Apply(source);
            }
        }

        public ComponentProfile WithVersion(MfmeVersion version) => this with { Version = version };

        public ComponentProfile WithTags(ComponentTagMap tags) => this with { Tags = tags };

        public ComponentProfile WithBaseTags(ComponentTagMap baseTags) => this with { BaseTags = baseTags };

        public ComponentProfile WithBaseOverrides(TagMapOverrides overrides) => this with { BaseOverrides = overrides };

        public ComponentProfile WithClassOverrides(TagMapOverrides overrides) => this with { ClassOverrides = overrides };

        public ComponentProfile WithOptions(ExtendedTagParser.Options options) => this with { Options = options };

        public ComponentProfile WithAngle(AngleProfile angle) => this with { Angle = angle };

        public ComponentProfile WithOrientationRule(OrientationDefaultMode mode)
        {
            return this with
            {
                Defaults = new DefaultsProfile(new Dictionary<string, object>(), mode),
            };
        }

        public ComponentProfile WithDefaults(DefaultsProfile defaults)
        {
            return this with { Defaults = defaults };
        }

        public ComponentProfile AsLegacyTwoSection()
        {
            ComponentTagMap secondSectionTags = NestedTags;
            return this with
            {
                BaseSection = BaseSectionProfile.LegacyTwoSection(secondSectionTags),
            };
        }

        public ComponentProfile WithMode(BaseSectionMode mode)
        {
            return this with
            {
                BaseSection = mode == BaseSectionMode.V20Signature
                    ? BaseSectionProfile.V20Signature
                    : mode == BaseSectionMode.LegacyTwoSection
                        ? BaseSectionProfile.LegacyTwoSection(NestedTags)
                        : BaseSectionProfile.SingleStream,
            };
        }
    }
}
