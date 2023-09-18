using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentLamp : ExtractComponentBase
    {
        [Serializable]
        public class LampElement
        {
            public string NumberAsText;
            public ColorJSON OnColor;
            public string BmpImageFilename;
            public string BmpMaskImageFilename;
        }

        public static readonly int kLampElementCount = 12;

        public LampElement[] LampElements = new LampElement[kLampElementCount];

        public bool NoOutline;
        public bool Graphic;
        public bool Transparent;
        public bool Blend;
        public bool Inverted;
        public bool ClickAll;
        public bool LED;
        public bool LockOut;
        public bool RGB;
        public bool PreserveAspectRatio;

        public string ButtonNumberAsString;
        public string CoinNote;
        public string Effect;
        public string InhibitLampAsString;
        public string Shortcut1;
        public string Shortcut2;
        public ColorJSON TextColor;
        public ColorJSON OutlineColor;
        public int XOff;
        public int YOff;
        public string Shape;
        public string ShapeParameter1;
        public string ShapeParameter2;

        public ColorJSON OffImageColor;

        public bool HasCoinInput
        {
            get
            {
                return CoinNote.Length > 0;
            }
        }

        public bool HasButtonInput
        {
            get
            {
                // TOIMPROVE - should also do a safe TryParse check, to ensure 
                // ButtonNumberAsString parses to an int
                return ButtonNumberAsString.Length > 0;
            }
        }


        public ExtractComponentLamp(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
            for(int lampElementIndex = 0; lampElementIndex < kLampElementCount; ++lampElementIndex)
            {
                LampElements[lampElementIndex] = new LampElement();
            }
        }

        public int? GetLampNumber(int lampElementIndex)
        {
            LampElement lampElement = LampElements[lampElementIndex];

            return lampElement.NumberAsText.Length == 0 ? (int?)null : int.Parse(lampElement.NumberAsText);
        }
    }

}
