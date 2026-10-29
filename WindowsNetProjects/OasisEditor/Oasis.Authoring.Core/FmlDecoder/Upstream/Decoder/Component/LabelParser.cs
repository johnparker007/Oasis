using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class LabelParser : ComponentParserBase<Label>
    {
        public Label Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Label, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

            offset = ExtendedTagParser.AlignExtendedSectionStart(profile.Tags, parseData, offset);

            var parseResult = ParseExtendedTags(component, profile, parseData, offset);
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
