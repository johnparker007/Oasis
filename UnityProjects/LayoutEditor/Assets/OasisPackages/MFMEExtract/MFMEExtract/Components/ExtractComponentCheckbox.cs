using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentCheckbox : ExtractComponentBase
    {
        public int Number;
        public ColorJSON TextColor;
        public string Text;
        public bool State;

        public ExtractComponentCheckbox(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
