using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class CoinmasterVideoParser : ComponentParserBase<CoinmasterVideo>
    {
        public CoinmasterVideo Parse(long componentOffset, uint componentId, byte[] data)
        {
            // MFME crashes if you try to edit or add overlay to this component
            // it has no other attributes, so the geometry parser is all we need.
            var (component, offset, _) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, data, offset);
            return component;
        }
    }
}
