using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    /// <summary>
    /// Geometry-angle handling for a component version. The rule drives the existing
    /// MFME wire-angle normalisation; the trailing skip is the legacy post-angle run.
    /// </summary>
    internal sealed record AngleProfile(
        GeometryAngleNormalization.Rule NormalizationRule,
        int ValidAngleTrailingSkipBytes = 5,
        int FinalOffsetAdjustment = 0,
        bool PreListTag6 = false)
    {
        public static readonly AngleProfile Default =
            new(new GeometryAngleNormalization.Rule(), ValidAngleTrailingSkipBytes: 5);

        public static AngleProfile Rewrite(int rewriteTriggerOffsetDelta, int validAngleOffsetDelta = 0)
        {
            return new AngleProfile(
                new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: rewriteTriggerOffsetDelta,
                    ValidAngleOffsetDelta: validAngleOffsetDelta));
        }

        public static AngleProfile RewriteWithBytes(
            byte[] rewriteBytesAfterNormalizedAngle,
            int rewriteTriggerOffsetDelta,
            int validAngleOffsetDelta = 0)
        {
            return new AngleProfile(
                new GeometryAngleNormalization.Rule(
                    RewriteBytesAfterNormalizedAngle: rewriteBytesAfterNormalizedAngle,
                    RewriteTriggerOffsetDelta: rewriteTriggerOffsetDelta,
                    ValidAngleOffsetDelta: validAngleOffsetDelta));
        }
    }
}
