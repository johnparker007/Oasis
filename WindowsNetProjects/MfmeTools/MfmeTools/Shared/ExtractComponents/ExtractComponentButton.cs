using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;

namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentButton : ExtractComponentBase
    {
        public static readonly int kLampElementCount = 2;

        public ExtractComponentLamp.LampElement[] LampElements = new ExtractComponentLamp.LampElement[kLampElementCount];

        public string ButtonNumberAsString;
        public string CoinNote;
        public string Effect;
        public string InhibitLampAsString;
        public string Shortcut1;
        public string Shortcut2;

        public bool Graphic;
        public bool Inverted;
        public bool Split;
        public bool LockOut;
        public bool LED;

        public int XOff;
        public int YOff;

        public ColorJSON TextColor;
        public string ShapeAsString;

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
                return ButtonNumberAsString.Length > 0;
            }
        }



        public ExtractComponentButton(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
            for (int lampElementIndex = 0; lampElementIndex < kLampElementCount; ++lampElementIndex)
            {
                LampElements[lampElementIndex] = new ExtractComponentLamp.LampElement();
            }
        }
    }

}
