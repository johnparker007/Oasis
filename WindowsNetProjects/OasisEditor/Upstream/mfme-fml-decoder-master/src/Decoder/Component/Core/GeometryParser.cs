using MfmeFmlDecoder.src.Model.Component;
using MfmeFmlDecoder.Utilities;
using System;
using System.IO;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    internal class GeometryParser
    {
        internal readonly record struct GeometryParseResult(
            long Offset,
            bool IsRawAngleInRange
        );

        /// <param name="validAngleTrailingSkipBytes">
        /// After a valid in-range angle, MFME often leaves a short fixed run (commonly 5 bytes) before extended TLVs.
        /// Some components (e.g. Led) put extended tags immediately; use <c>0</c> for those.
        /// </param>
        public GeometryParseResult ParseInto(
            BaseComponent component,
            byte[] data,
            long offset = 5,
            int validAngleTrailingSkipBytes = 5
        )
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (validAngleTrailingSkipBytes < 0) throw new ArgumentOutOfRangeException(nameof(validAngleTrailingSkipBytes));

            long endOffset = data.Length;

            uint? x = null;
            uint? y = null;
            uint? width = null;
            uint? height = null;
            int? number = null;
            float? angle = null;
            bool isRawAngleInRange = false;

            while (offset < endOffset)
            {
                if (offset >= endOffset) break;
                byte tag = data[offset];
                offset++;

                // Geometry tag section ends on 0x00, or any unknown tag (> 0x07).
                if (tag == 0x00 || tag > 0x07)
                {
                    break;
                }

                if (offset + 3 >= endOffset)
                {
                    break;
                }

                switch (tag)
                {
                    case 0x01: // X
                        x = BitConverter.ToUInt32(data, (int)offset);
                        offset += 4;
                        break;

                    case 0x02: // Y
                        y = BitConverter.ToUInt32(data, (int)offset);
                        offset += 4;
                        break;

                    case 0x03: // Height
                        height = BitConverter.ToUInt32(data, (int)offset);
                        offset += 4;
                        break;

                    case 0x04: // Width
                        width = BitConverter.ToUInt32(data, (int)offset);
                        offset += 4;
                        break;

                    case 0x05: // Number (signed 32-bit)
                        number = BitConverter.ToInt32(data, (int)offset);
                        offset += 4;
                        break;

                    case 0x07: // Angle — six-byte MFME wire (01 08 + int16 half-steps + sign ext), else legacy fixed-point
                        if (MfmeAngleWireCodec.TryReadWireAngle(data, (int)offset, out double wireAngle))
                        {
                            isRawAngleInRange = wireAngle is >= -360.0 and <= 360.0;
                            angle = (float)wireAngle;
                            offset += MfmeAngleWireCodec.WireLength;
                        }
                        else
                        {
                            float rawAngle = FixedPointUtility.ConvertFixedPoint17(data, (int)offset);
                            isRawAngleInRange = rawAngle is >= 0 and <= 360;
                            angle = isRawAngleInRange ? rawAngle : 0.0f;
                            offset += 4;
                        }

                        break;

                    default:
                        offset += 4;
                        break;
                }
            }

            if (!x.HasValue) throw new InvalidDataException("Missing X coordinate (tag 0x01)");
            if (!y.HasValue) throw new InvalidDataException("Missing Y coordinate (tag 0x02)");
            if (!width.HasValue) throw new InvalidDataException("Missing Width (tag 0x03)");
            if (!height.HasValue) throw new InvalidDataException("Missing Height (tag 0x04)");
            if (!number.HasValue) throw new InvalidDataException("Missing Number (tag 0x05)");
            if (!angle.HasValue) throw new InvalidDataException("Missing Angle (tag 0x07)");

            component.X = x.Value;
            component.Y = y.Value;
            component.Width = width.Value;
            component.Height = height.Value;
            component.Number = number.Value;
            component.Angle = angle.Value;

            if (isRawAngleInRange && validAngleTrailingSkipBytes > 0)
            {
                // Only skip the post-angle run when a 0x00 geometry terminator is present.
                if (offset < endOffset && data[offset] == 0x00)
                {
                    offset++;
                    offset += validAngleTrailingSkipBytes - 1;
                }
            }

            return new GeometryParseResult(offset, isRawAngleInRange);
        }

        /// <summary>
        /// Locates a geometry-section tag value (tags <c>0x01</c>–<c>0x07</c> after the 4-byte component header)
        /// without running full <see cref="ParseInto"/>.
        /// </summary>
        internal static bool TryReadTagUInt32(byte[] data, byte geometryTag, out uint value, out int valueOffset)
        {
            if (!TryReadTagInt32(data, geometryTag, out int signed, out valueOffset))
            {
                value = 0;
                return false;
            }

            value = unchecked((uint)signed);
            return true;
        }

        internal static bool TryReadTagInt32(byte[] data, byte geometryTag, out int value, out int valueOffset)
        {
            value = 0;
            valueOffset = 0;
            if (data is null || geometryTag is 0 or > 0x07)
            {
                return false;
            }

            long offset = 5;
            while (offset < data.Length)
            {
                byte tag = data[offset++];
                if (tag == 0x00 || tag > 0x07)
                {
                    break;
                }

                if (offset + 3 >= data.Length)
                {
                    break;
                }

                if (tag == geometryTag)
                {
                    valueOffset = (int)offset;
                    value = BitConverter.ToInt32(data, valueOffset);
                    return true;
                }

                offset += 4;
            }

            return false;
        }
    }
}
