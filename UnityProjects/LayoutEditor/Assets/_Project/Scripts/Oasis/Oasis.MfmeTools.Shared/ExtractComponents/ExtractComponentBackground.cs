using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBackground : ExtractComponentBase
    {
        public string BmpImageFilename;
        public ColorJSON Color;

        public ExtractComponentBackground(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

    }

}
