using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentLed : ExtractComponentBase
    {
        public string NumberAsString;
        public bool Led;
        public string DigitAsString;
        public string Segment;
        public ColorJSON OnColor;
        public ColorJSON OffColor;
        public bool NoOutline;
        public bool NoShadow;
        public string Style;

        public ExtractComponentLed(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

        public int? GetNumber()
        {
            return NumberAsString.Length == 0 ? (int?)null : int.Parse(NumberAsString);
        }

        public int? GetDigit()
        {
            return DigitAsString.Length == 0 ? (int?)null : int.Parse(DigitAsString);
        }
    }

}
