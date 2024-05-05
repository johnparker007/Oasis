using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;
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

        public ExtractComponentAlphaNew(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
