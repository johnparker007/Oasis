using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal readonly struct SubLampNumberTablePayload
    {
        public uint? NumberOfDefinedLampNumbers { get; init; }

        public IReadOnlyList<LampSublampTableEntry> Entries { get; init; }
    }
}