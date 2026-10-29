using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class CoinmasterVideoParser : ComponentParserBase<CoinmasterVideo>
    {
        public CoinmasterVideo Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.CoinmasterVideo, context.Version);
            // MFME crashes if you try to edit or add overlay to this component
            // it has no other attributes, so the geometry parser is all we need.
            var (component, offset, _) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, data, offset);
            return component;
        }
    }
}
