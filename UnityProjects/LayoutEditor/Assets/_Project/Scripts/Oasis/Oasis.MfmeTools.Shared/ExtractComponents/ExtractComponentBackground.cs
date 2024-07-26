using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBackground : ExtractComponentBase
    {
        public string BmpImageFilename;

        // TODO there's a bunch of stuff missing to be implemented here!
        // color etc

        public ExtractComponentBackground(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

    }

}
