using System;

namespace MfmeFmlDecoder.Decoder
{
    internal enum ComponentOrderingMode
    {
        InFileOrder,
        BordersLastReversed,
    }

    /// <summary>
    /// File-level parsing state. The version is read once from TLV tag <c>0x2F</c>
    /// and flows through the walkers into every component parser. Component parsers
    /// never inspect version strings themselves.
    /// </summary>
    internal sealed class ParsingContext
    {
        public static readonly ParsingContext V20_1 =
            new ParsingContext(MfmeVersion.V20_1, sourcePath: null);

        public MfmeVersion Version { get; }

        public string SourcePath { get; }

        public ComponentOrderingMode Ordering { get; }

        public ParsingContext(
            MfmeVersion version,
            string sourcePath,
            ComponentOrderingMode ordering = ComponentOrderingMode.InFileOrder)
        {
            Version = version;
            SourcePath = sourcePath;
            Ordering = ordering;
        }

        public ParsingContext WithSourcePath(string sourcePath)
        {
            return new ParsingContext(Version, sourcePath, Ordering);
        }
    }
}
