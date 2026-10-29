using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    internal sealed class UnsupportedComponentProfileException : InvalidOperationException
    {
        public MFMEComponentType Kind { get; }
        public MfmeVersion Version { get; }

        public UnsupportedComponentProfileException(MFMEComponentType kind, MfmeVersion version)
            : base(
                $"No component profile registered for {kind} at MFME version '{version}'. " +
                "Add an explicit profile for this (component, version) pair; nearest-version fallback is not allowed.")
        {
            Kind = kind;
            Version = version;
        }
    }
}
