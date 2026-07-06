using System;

namespace MfmeFmlDecoder.Utilities
{
    public static class FixedPointUtility
    {
        // 32-bit signed fixed-point with 17 fractional bits (same layout as BitConverter.ToInt32).
        public static float ConvertFixedPoint17(byte[] data, int offset)
        {
            int rawValue = BitConverter.ToInt32(data, offset);
            return rawValue / 131072.0f;
        }
    }

    /// <summary>
    /// MFME wire encoding for geometry angle: six bytes after tag 0x07
    /// (01 08 + int16 half-degree steps LE + sign extension).
    /// </summary>
    public static class MfmeAngleWireCodec
    {
        public const int WireLength = 6;

        public static byte[] AngleToBytes(double angle)
        {
            if (angle < -360.0 || angle > 360.0)
            {
                throw new ArgumentOutOfRangeException(nameof(angle), "Angle must be between -360.0 and 360.0");
            }

            double halfIncrements = angle * 2.0;
            if (Math.Abs(halfIncrements - Math.Round(halfIncrements)) > 0.0001)
            {
                throw new ArgumentException("Angle must be in 0.5 degree increments", nameof(angle));
            }

            short value = (short)Math.Round(halfIncrements);

            byte[] result = new byte[WireLength];
            result[0] = 0x01;
            result[1] = 0x08;
            result[2] = (byte)(value & 0xFF);
            result[3] = (byte)((value >> 8) & 0xFF);
            if (value >= 0)
            {
                result[4] = 0x00;
                result[5] = 0x00;
            }
            else
            {
                result[4] = 0xFD;
                result[5] = 0xFF;
            }

            return result;
        }

        public static double BytesToAngle(ReadOnlySpan<byte> wire)
        {
            if (wire.Length != WireLength)
            {
                throw new ArgumentException($"Expected {WireLength} bytes", nameof(wire));
            }

            short value = (short)(wire[2] | (wire[3] << 8));
            return value / 2.0;
        }

        public static bool IsValidWireAngle(ReadOnlySpan<byte> wire)
        {
            if (wire.Length != WireLength)
            {
                return false;
            }

            try
            {
                double angle = BytesToAngle(wire);
                byte[] rebuilt = AngleToBytes(angle);
                byte leadByte = wire[0] == 0x00 ? (byte)0x01 : wire[0];
                if (leadByte != rebuilt[0])
                {
                    return false;
                }

                for (int i = 1; i < WireLength; i++)
                {
                    if (wire[i] != rebuilt[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static bool TryReadWireAngle(byte[] data, int offset, out double angle)
        {
            angle = 0;
            if (data is null || offset < 0 || offset + WireLength > data.Length)
            {
                return false;
            }

            ReadOnlySpan<byte> wire = data.AsSpan(offset, WireLength);
            if (!IsValidWireAngle(wire))
            {
                return false;
            }

            angle = BytesToAngle(wire);
            return true;
        }
    }
}
