using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    internal sealed class FontTagParser<T> where T : BaseComponent
    {
        private readonly T component;

        private static readonly IReadOnlyDictionary<uint, string> ScriptStyleMap = new Dictionary<uint, string>
        {
            [0x00] = "Western",
            [0x80] = "Japanese",
            [0xA1] = "Greek",
            [0xA2] = "Turkish",
            [0xBA] = "Baltic",
            [0xEE] = "Central European",
            [0xCC] = "Cyrillic",
        };

        public FontTagParser(T component)
        {
            this.component = component ?? throw new ArgumentNullException(nameof(component));
        }

        public (T component, long offset) ParseFont(
            long offset,
            IReadOnlyDictionary<byte, string> validFontTagMap,
            byte[] data
        )
        {
            if (validFontTagMap is null) throw new ArgumentNullException(nameof(validFontTagMap));
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            byte tag = data[offset];
            if (!validFontTagMap.TryGetValue(tag, out string role))
            {
                throw new InvalidOperationException($"Unexpected font tag 0x{tag:X2} at payload offset 0x{offset:X}.");
            }

            int valueOffset = checked((int)offset + 1);
            int valueLength = checked(data.Length - valueOffset);
            var font = ParseFontBlob(tag, role, data, valueOffset, valueLength);
            component.Fonts[role] = font;

            int cursor = valueOffset + GetFontValueLength(data, valueOffset);

            return (component, cursor);
        }

        public static FontTagEntry ParseFontBlob(uint tag, string role, byte[] data, int offset, int length)
        {
            if (role is null) throw new ArgumentNullException(nameof(role));
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || offset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(length));

            int cursor = offset;
            int end = offset + length;

            EnsureAvailable(data, cursor, 4, "font name length", end);
            uint fontNameLength = BitConverter.ToUInt32(data, cursor);
            cursor += 4;

            if (fontNameLength > int.MaxValue)
            {
                throw new InvalidOperationException($"Font name length {fontNameLength} at offset 0x{offset:X} exceeds supported range.");
            }

            int nameLength = checked((int)fontNameLength);
            EnsureAvailable(data, cursor, nameLength, "font name bytes", end);
            string fontName = Encoding.ASCII.GetString(data, cursor, nameLength);
            cursor += nameLength;

            EnsureAvailable(data, cursor, 4, "font size", end);
            uint fontSize = BitConverter.ToUInt32(data, cursor);
            cursor += 4;

            EnsureAvailable(data, cursor, 4, "script style", end);
            uint scriptStyleDword = BitConverter.ToUInt32(data, cursor);
            cursor += 4;

            EnsureAvailable(data, cursor, 1, "padding", end);
            cursor += 1;

            EnsureAvailable(data, cursor, 1, "font style", end);
            byte fontStyle = data[cursor];

            byte scriptStyleRaw = (byte)(scriptStyleDword & 0xFFu);
            string textColour = FormatTextColourFromScriptStyleDword(scriptStyleDword);

            return new FontTagEntry(
                Tag: tag,
                Role: role,
                FontName: fontName,
                FontSize: fontSize,
                ScriptStyleRaw: scriptStyleRaw,
                ScriptStyleName: ResolveScriptStyleDisplayName(scriptStyleRaw),
                TextColour: textColour,
                FontStyle: fontStyle
            );
        }

        public static string ResolveScriptStyleDisplayName(byte scriptStyleRaw)
        {
            return ScriptStyleMap.TryGetValue(scriptStyleRaw, out string scriptStyle)
                ? scriptStyle
                : "Western";
        }

        /// <summary>
        /// High 24 bits of the script-style dword are B,G,R (COLORREF-style); output is <c>#RRGGBBFF</c>.
        /// </summary>
        public static string FormatTextColourFromScriptStyleDword(uint scriptStyleDword)
        {
            uint bgr24 = scriptStyleDword >> 8 & 0xFFFFFFu;
            return FormatBgr888ToArgbHexFf(bgr24);
        }

        /// <summary>
        /// Triplet is stored as B,G,R from high byte to low (COLORREF-style); output remains #RRGGBBFF.
        /// </summary>
        private static string FormatBgr888ToArgbHexFf(uint bgr24)
        {
            bgr24 &= 0xFFFFFFu;
            byte b = (byte)(bgr24 >> 16 & 0xFF);
            byte g = (byte)(bgr24 >> 8 & 0xFF);
            byte r = (byte)(bgr24 & 0xFF);
            return string.Create(
                9,
                (r, g, b),
                static (span, c) =>
                {
                    span[0] = '#';
                    WriteHexByte(span.Slice(1, 2), c.r);
                    WriteHexByte(span.Slice(3, 2), c.g);
                    WriteHexByte(span.Slice(5, 2), c.b);
                    span[7] = 'F';
                    span[8] = 'F';
                });
        }

        private static void WriteHexByte(Span<char> dest, byte value)
        {
            string s = value.ToString("X2", CultureInfo.InvariantCulture);
            dest[0] = s[0];
            dest[1] = s[1];
        }

        public static int GetFontValueLength(byte[] data, int offset)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            int cursor = offset;
            EnsureAvailable(data, cursor, 4, "font name length");
            uint fontNameLength = BitConverter.ToUInt32(data, cursor);
            cursor += 4;
            if (fontNameLength > int.MaxValue)
            {
                throw new InvalidOperationException($"Font name length {fontNameLength} at offset 0x{offset:X} exceeds supported range.");
            }

            int nameLength = checked((int)fontNameLength);
            EnsureAvailable(data, cursor, nameLength + 4 + 4 + 1 + 1, "font value");
            cursor += nameLength + 4 + 4 + 1 + 1;
            return cursor - offset;
        }

        private static void EnsureAvailable(byte[] data, int offset, int requiredLength, string fieldName, int? endExclusive = null)
        {
            int end = endExclusive ?? data.Length;
            if (offset < 0 || requiredLength < 0 || offset + requiredLength > end)
            {
                throw new InvalidOperationException(
                    $"Not enough bytes to parse {fieldName}. Need {requiredLength} bytes at 0x{offset:X}, " +
                    $"but only {end - offset} remain.");
            }
        }

    }
}
