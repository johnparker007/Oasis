using System;
using System.IO;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.Utilities;

namespace MfmeFmlDecoder.Decoder
{
    internal sealed class ComponentWalker
    {
        private ComponentParser _componentParser;
        public ComponentWalker(ComponentParser componentParser)
        {
            _componentParser = componentParser;
        }

        // Walk the components portion of the stream
        public void WalkComponents(Stream fileStream, BinaryReader reader, long offset)
        {
            RunLog.WriteLine($"ComponentWalker called");
            while (fileStream.Position < fileStream.Length)
            {
                try
                {
                    // Store the offset where this record starts
                    long recordStartOffset = fileStream.Position;

                    uint componentId = reader.ReadUInt32();
                    uint length = reader.ReadUInt32() - 8;
                    byte[] values = reader.ReadBytes((int)length);

                    // Pass the record start offset to OnRecordRead
                    OnRecordRead(componentId, recordStartOffset, length, values);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }
            
        }

        private void OnRecordRead(uint componentId, long offset, uint length, byte[] values)
        {
            RunLog.WriteLine($"Offset: 0x{offset:X8}, Component ID: 0x{componentId:X8}, Length: 0x{length + 8:X2}");
            _componentParser.ParseComponent(offset, componentId, length, values);
        }
    }
}