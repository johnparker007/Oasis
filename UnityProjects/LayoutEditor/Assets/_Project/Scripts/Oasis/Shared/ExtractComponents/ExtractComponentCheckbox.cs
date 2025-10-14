using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentCheckbox : ExtractComponentBase
    {
        public int Number;
        public ColorJSON TextColor;
        public string Text;
        public bool State;

        public ExtractComponentCheckbox(ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
