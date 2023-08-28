using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentRgbLed : ExtractComponentBase
    {
        public int Number;
        public string RedLedNumberAsText;
        public string GreenLedNumberAsText;
        public string BlueLedNumberAsText;
        public string WhiteLedNumberAsText;
        public bool MuxLED;
        public bool NoOutline;
        public bool NoShadow;
        public string Style;
        public ColorJSON AdjustedColorOff;
        public ColorJSON AdjustedColorRed;
        public ColorJSON AdjustedColorGreen;
        public ColorJSON AdjustedColorRedGreen;
        public ColorJSON AdjustedColorBlue;
        public ColorJSON AdjustedColorRedBlue;
        public ColorJSON AdjustedColorGreenBlue; // incorrectly labelled as RedGreen in MFME
        public ColorJSON AdjustedColorRedGreenBlue;

        public ExtractComponentRgbLed(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
