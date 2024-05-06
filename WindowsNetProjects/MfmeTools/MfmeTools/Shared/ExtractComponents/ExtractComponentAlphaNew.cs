using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;
using static Oasis.MfmeTools.Shared.Mfme.MFMEConstants;

namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentAlphaNew : ExtractComponentBase
    {
        public int Number;
        public MFMECharacterSetType CharacterSet;
        public bool Reversed;
        public ColorJSON OnColor;
        public bool SixteenSegment;

        public ExtractComponentAlphaNew(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
