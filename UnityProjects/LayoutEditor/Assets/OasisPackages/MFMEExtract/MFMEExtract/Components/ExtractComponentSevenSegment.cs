using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentSevenSegment : ExtractComponentBase
    {
		public int Number;
        public bool DPRight;
        public bool Alpha;
        public bool DPOff;
        public bool DPOn;
        public bool AutoDP;
        public bool SixteenSegment;
        public bool ZeroOn;
        public string TypeAsString;
        public ColorJSON SegmentOnColor;
        public ColorJSON SegmentOffColor;
        public ColorJSON SegmentBackgroundColor;

        public int Thickness;
        public int Spacing;
        public int HorzSpacing;
        public int VertSpacing;
        public int Offset;
        public int Angle;
        public int Slant;
        public int Chop;
        public int Centre;

        public bool LampsProgrammable;
        public string Lamps1AsString;
        public string Lamps2AsString;
        public string Lamps3AsString;
        public string Lamps4AsString;
        public string Lamps5AsString;
        public string Lamps6AsString;
        public string Lamps7AsString;
        public string Lamps8AsString;
        public string Lamps9AsString;
        public string Lamps10AsString;
        public string Lamps11AsString;
        public string Lamps12AsString;
        public string Lamps13AsString;
        public string Lamps14AsString;
        public string Lamps15AsString;
        public string Lamps16AsString;

        public ExtractComponentSevenSegment(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
