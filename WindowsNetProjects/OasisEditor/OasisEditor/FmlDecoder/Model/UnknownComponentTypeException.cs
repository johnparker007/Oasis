using System;
using System.Globalization;
using System.Text;

namespace MfmeFmlDecoder.Model
{
    internal sealed class UnknownComponentTypeException : InvalidOperationException
    {
        public uint ComponentId { get; }
        public long ComponentOffset { get; }
        public uint PayloadLength { get; }

        public UnknownComponentTypeException(
            uint componentId,
            long componentOffset,
            uint payloadLength,
            byte[] payload)
            : base(BuildMessage(componentId, componentOffset, payloadLength, payload))
        {
            ComponentId = componentId;
            ComponentOffset = componentOffset;
            PayloadLength = payloadLength;
        }

        private static string BuildMessage(
            uint componentId,
            long componentOffset,
            uint payloadLength,
            byte[] payload)
        {
            var message = new StringBuilder();
            message.Append("Unknown MFME component type at file offset 0x");
            message.Append(componentOffset.ToString("X8", CultureInfo.InvariantCulture));
            message.Append(": component ID 0x");
            message.Append(componentId.ToString("X8", CultureInfo.InvariantCulture));
            message.Append(" (");
            message.Append(componentId);
            message.Append("). Payload length ");
            message.Append(payloadLength);
            message.Append(" bytes (wire record length ");
            message.Append(checked(payloadLength + 8U));
            message.Append(" bytes including id and length fields).");

            if (payload is { Length: > 0 })
            {
                message.Append(" Payload preview: ");
                message.Append(FormatPayloadPreview(payload));
                message.Append('.');
            }

            message.Append(" Register the type in MFMEComponentType and add a parser in ComponentParser.");
            return message.ToString();
        }

        private static string FormatPayloadPreview(byte[] payload)
        {
            int previewLength = Math.Min(payload.Length, 16);
            var preview = new StringBuilder(previewLength * 3);
            for (int i = 0; i < previewLength; i++)
            {
                if (i > 0)
                {
                    preview.Append(' ');
                }

                preview.Append(payload[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            if (payload.Length > previewLength)
            {
                preview.Append(" ...");
            }

            return preview.ToString();
        }
    }
}
