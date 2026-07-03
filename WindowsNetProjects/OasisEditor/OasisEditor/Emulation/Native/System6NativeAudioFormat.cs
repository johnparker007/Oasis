using System.Runtime.InteropServices;

namespace OasisEditor;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct System6NativeAudioFormat
{
    public const uint VersionValue = 1;
    public const uint PcmS16FormatValue = 1;

    public uint SizeBytes;
    public uint Version;
    public uint SampleRate;
    public uint Channels;
    public uint BitsPerSample;
    public uint Format;
}
