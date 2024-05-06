using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBackground : ExtractComponentBase
    {
        public string BmpImageFilename;

        public ExtractComponentBackground(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

    }

}
