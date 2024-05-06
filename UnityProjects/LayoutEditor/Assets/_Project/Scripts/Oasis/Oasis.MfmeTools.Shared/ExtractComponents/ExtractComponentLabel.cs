using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentLabel : ExtractComponentBase
    {
        public string LampNumberAsText;
        public bool Transparent;
        public ColorJSON TextColor;
        public ColorJSON BackgroundColor;

        public ExtractComponentLabel(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
