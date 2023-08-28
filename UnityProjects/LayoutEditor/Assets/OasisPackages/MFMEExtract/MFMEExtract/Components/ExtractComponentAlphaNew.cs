using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;
using TempArcadeSimComponents;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentAlphaNew : ExtractComponentBase
    {
        public int Number;
        public ComponentSegmentAlpha.MFMECharacterSetType CharacterSet;
        public bool Reversed;
        public ColorJSON OnColor;
        public bool SixteenSegment;

        public ExtractComponentAlphaNew(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
