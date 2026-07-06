using System;
using System.Globalization;
using System.Text;

namespace MfmeFmlDecoder.Model
{
    internal sealed class ComponentTagParseException : InvalidOperationException
    {
        public string ComponentContext { get; }
        public bool IsNestedScope { get; }
        public long PayloadIndex { get; }

        public ComponentTagParseException(
            string componentContext,
            bool isNestedScope,
            long payloadIndex,
            byte[] data,
            int parsedValueCount,
            int parsedFontCount,
            string additionalHint = null)
            : base(BuildMessage(
                componentContext,
                isNestedScope,
                payloadIndex,
                data,
                parsedValueCount,
                parsedFontCount,
                additionalHint))
        {
            ComponentContext = componentContext;
            IsNestedScope = isNestedScope;
            PayloadIndex = payloadIndex;
        }

        private static string BuildMessage(
            string componentContext,
            bool isNestedScope,
            long payloadIndex,
            byte[] data,
            int parsedValueCount,
            int parsedFontCount,
            string additionalHint)
        {
            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(componentContext))
            {
                message.Append('[');
                message.Append(componentContext);
                message.Append("] ");
            }

            message.Append(isNestedScope ? "Nested extended TLV" : "Extended TLV");
            message.Append(": cannot decode tag key at payload index ");
            message.Append(payloadIndex);
            message.Append(" (0x");
            message.Append(payloadIndex.ToString("X", CultureInfo.InvariantCulture));
            message.Append("). Already parsed ");
            message.Append(parsedValueCount);
            message.Append(" scalar/boolean/etc. value(s), ");
            message.Append(parsedFontCount);
            message.Append(" font role(s).");

            if (!string.IsNullOrWhiteSpace(additionalHint))
            {
                message.Append(' ');
                message.Append(additionalHint.Trim());
            }

            if (data is { Length: > 0 } && payloadIndex >= 0 && payloadIndex < data.Length)
            {
                message.Append(" Next bytes: ");
                message.Append(FormatBytes(data, payloadIndex, 10));
                message.Append('.');
            }

            message.Append(" Check the component tag map or extended-section offset.");
            return message.ToString();
        }

        private static string FormatBytes(byte[] data, long offset, int count)
        {
            int start = checked((int)offset);
            int length = Math.Min(count, data.Length - start);
            var preview = new StringBuilder(length * 3);
            for (int i = 0; i < length; i++)
            {
                if (i > 0)
                {
                    preview.Append(' ');
                }

                preview.Append(data[start + i].ToString("X2", CultureInfo.InvariantCulture));
            }

            if (data.Length - start > length)
            {
                preview.Append(" ...");
            }

            return preview.ToString();
        }
    }
}
