using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentAlpha : ExtractComponentBase
    {
        public int Number;
        public bool Reversed;
        public ColorJSON Color;
        public int DigitWidth;
        public int Columns;
        public string BmpImageFilename;

        public ExtractComponentAlpha(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
