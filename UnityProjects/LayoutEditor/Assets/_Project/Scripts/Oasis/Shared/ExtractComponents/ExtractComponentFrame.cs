using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentFrame : ExtractComponentBase
    {
        public string ShapeAsString;
        public string BevelAsString;

        public ExtractComponentFrame(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
