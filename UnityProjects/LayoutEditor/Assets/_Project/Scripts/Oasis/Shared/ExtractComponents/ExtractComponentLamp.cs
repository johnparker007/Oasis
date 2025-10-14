using Newtonsoft.Json;
using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentLamp : ExtractComponentBase
    {
        [Serializable]
        public class LampElement : ICloneable
        {
            public string NumberAsText;
            public ColorJSON OnColor;
            public string BmpImageFilename;
            public string BmpMaskImageFilename;

            public int? Number
            {
                get
                {
                    if(NumberAsText == null || NumberAsText.Length == 0)
                    {
                        return null;
                    }

                    return int.Parse(NumberAsText);
                }
            }

            public object Clone()
            {
                LampElement cloneCopy = new LampElement()
                {
                    NumberAsText = NumberAsText,
                    OnColor = OnColor,
                    BmpImageFilename = BmpImageFilename,
                    BmpMaskImageFilename = BmpMaskImageFilename
                };

                return cloneCopy;
            }
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
                return ButtonNumberAsString.Length > 0;
            }
        }

        [Newtonsoft.Json.JsonConstructor]
        public ExtractComponentLamp(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
            for(int lampElementIndex = 0; lampElementIndex < kLampElementCount; ++lampElementIndex)
            {
                LampElements[lampElementIndex] = new LampElement();
            }
        }

        // This is a special case method used to convert an MFME Button to an MFME Lamp
        // during the Import process, so from then on is treated as a Lamp with Input
        public ExtractComponentLamp(ExtractComponentButton sourceExtractComponentButton) : base(sourceExtractComponentButton)
        {
            for (int lampElementIndex = 0; lampElementIndex < kLampElementCount; ++lampElementIndex)
            {
                LampElements[lampElementIndex] = new LampElement();
            }

            for (int lampElementIndex = 0; lampElementIndex < sourceExtractComponentButton.LampElements.Length; ++lampElementIndex)
            {
                LampElements[lampElementIndex] = (LampElement)sourceExtractComponentButton.LampElements[lampElementIndex].Clone();
            }

            ButtonNumberAsString = sourceExtractComponentButton.ButtonNumberAsString; ;
            CoinNote = sourceExtractComponentButton.CoinNote;
            Effect = sourceExtractComponentButton.Effect;
            InhibitLampAsString = sourceExtractComponentButton.InhibitLampAsString;
            Shortcut1 = sourceExtractComponentButton.Shortcut1;
            Shortcut2 = sourceExtractComponentButton.Shortcut2;

            Graphic = sourceExtractComponentButton.Graphic;
            Inverted = sourceExtractComponentButton.Inverted;
            LockOut = sourceExtractComponentButton.LockOut;
            LED = sourceExtractComponentButton.LED;

            XOff = sourceExtractComponentButton.XOff;
            YOff = sourceExtractComponentButton.YOff;

            TextColor = sourceExtractComponentButton.TextColor;
            Shape = sourceExtractComponentButton.ShapeAsString;

            OffImageColor = sourceExtractComponentButton.OffImageColor;
        }
    }

}
