using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentSevenSegmentBlock : ExtractComponentBase
    {
        [Serializable]
        public class DigitElement
        {
            public string NumberAsString;
            public bool Programmable;
            public bool Visible;
            public bool DPOn;
            public bool DPOff;
            public bool AutoDP;
            public bool ZeroOn;
            public string ProgrammableSegment1LampNumberAsString;
            public string ProgrammableSegment2LampNumberAsString;
            public string ProgrammableSegment3LampNumberAsString;
            public string ProgrammableSegment4LampNumberAsString;
            public string ProgrammableSegment5LampNumberAsString;
            public string ProgrammableSegment6LampNumberAsString;
            public string ProgrammableSegment7LampNumberAsString;
            public string ProgrammableSegment8LampNumberAsString;
        }

        public const int kMaximumColumns = 8;
        public const int kMaximumRows = 6;
        public const int kDigitElementCount = kMaximumColumns * kMaximumRows;

        public int Width;
        public int Height;
        public int Columns;
        public int Rows;
        public int RowSpacing;
        public int ColumnSpacing;
        public ColorJSON OnColour;
        public ColorJSON OffColour;
        public ColorJSON BackColour;
        public string TypeAsString;
        public bool DPRight;
        public bool FourteenSegment;
        public int Thickness;
        public int Spacing;
        public int HorzSize;
        public int VertSize;
        public int Offset;
        public int Angle;
        public int Slant;
        public int Chop;
        public int Centre;

        public DigitElement[] DigitElements = new DigitElement[kDigitElementCount];


        public ExtractComponentSevenSegmentBlock(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
            for (int digitElementIndex = 0; digitElementIndex < kDigitElementCount; ++digitElementIndex)
            {
                DigitElements[digitElementIndex] = new DigitElement();
            }
        }
    }

}
