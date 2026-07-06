using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class CheckboxParser : ComponentParserBase<Checkbox>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x01, "Checked", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x02, new TagInfo(0x00, "Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x27, new TagInfo(0x00, "PrimaryFont", Array.Empty<byte>(), ValueRole.FONT) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { }, ValueRole.UINT32) },
            { 0x3F, new TagInfo(0x00, "Label", new byte[] { }, ValueRole.TEXT) },
        };

        public Checkbox Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 7,
                    ValidAngleOffsetDelta: 7));

            DumpRemaining(componentOffset, parseData, offset);

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            return component;
        }
    }
}
