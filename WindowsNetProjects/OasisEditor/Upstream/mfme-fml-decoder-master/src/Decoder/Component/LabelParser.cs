using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class LabelParser : ComponentParserBase<Label>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { }, ValueRole.UINT32) },
            { 0x3F, new TagInfo(0x00, "Label", new byte[] { }, ValueRole.TEXT) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x39, new TagInfo(0x04, "Lamp", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
            { 0x01, new TagInfo(0x04, "BackgroundColour", new byte[] { 0xF0, 0xF0, 0xF0, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x01, "Unknown 0x02", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x27, new TagInfo(0x00, "PrimaryFont", Array.Empty<byte>(), ValueRole.FONT) },
            { 0x38, new TagInfo(0x04, "Unknown 0x38", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1C, new TagInfo(0x04, "Unknown 0x1C", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        };

        public Label Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteBytesAfterNormalizedAngle: new byte[] { 0x00, 0x01 },
                    RewriteTriggerOffsetDelta: 0,
                    // 0: extended TLVs may follow the angle wire immediately (e.g. PrimaryFont 0x27).
                    // A non-zero delta skips into the middle of variable-length tags such as fonts.
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, parseData, offset);

            offset = ExtendedTagParser.AlignExtendedSectionStart(componentTagMap, parseData, offset);

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);
            ApplyLamp(component);

            return component;
        }

        /// <summary>MFME uses <c>0xFFFFFFFE</c> (-2) when a label lamp number is not defined.</summary>
        private const int UndefinedLampNumber = SublampTableJsonConverter.UndefinedSublampNumber;

        private static void ApplyLamp(Label component)
        {
            if (!component.Int32s.TryGetValue("Lamp", out int lamp))
            {
                return;
            }

            if (lamp == UndefinedLampNumber)
            {
                // Undefined lamps must not appear in JSON Values.
                component.Int32s.Remove("Lamp");
                component.Lamp = null;
                return;
            }

            component.Lamp = lamp;
        }
    }
}
