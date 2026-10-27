using System;
using System.Globalization;
using System.Text;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.Model
{
    internal sealed class UnsupportedMfmeVersionException : InvalidOperationException
    {
        public long RecordOffset { get; }
        public string FoundVersion { get; }

        public UnsupportedMfmeVersionException(long recordOffset, byte[] versionBytes)
            : base(BuildMessage(recordOffset, versionBytes))
        {
            RecordOffset = recordOffset;
            FoundVersion = MfmeVersionReader.FormatVersion(versionBytes);
        }

        private static string BuildMessage(long recordOffset, byte[] versionBytes)
        {
            var message = new StringBuilder();
            message.Append("Unsupported MFME version at file offset 0x");
            message.Append(recordOffset.ToString("X8", CultureInfo.InvariantCulture));
            message.Append(": '");
            message.Append(MfmeVersionReader.FormatVersion(versionBytes));
            message.Append("'. Only MFME v20.1 is supported.");
            return message.ToString();
        }
    }
}
