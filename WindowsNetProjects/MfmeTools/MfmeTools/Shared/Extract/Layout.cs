using Newtonsoft.Json;
using Oasis.MfmeTools.Shared.ExtractComponents;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oasis.MfmeTools.Shared.Extract
{
    [Serializable]
    public class Layout
    {
        public string ASName;

        public List<ExtractComponentBase> Components = new List<ExtractComponentBase>();

        [JsonIgnore]
        public ExtractComponentBackground Background
        {
            get
            {
                return (ExtractComponentBackground)Components.FirstOrDefault(x => x.GetType() == typeof(ExtractComponentBackground));
            }
        }

        public bool IsOutsideLayoutWindow(ExtractComponentBase extractComponentBase)
        {
            return extractComponentBase.Position.X > Background.Size.X
                || extractComponentBase.Position.Y > Background.Size.Y
                || (extractComponentBase.Position.X + extractComponentBase.Size.X) < 0
                || (extractComponentBase.Position.Y + extractComponentBase.Size.Y) < 0;
        }

// OASIS TODO - to fix where MPU4 lamps wrong in Mfme with 'matching' wrong Chr lamp values
        //public void RemapLamps(string[] mfmeLampTable, string[] mameLampTable)
        //{
        //    MAMELayoutMPU4ChrLampRemapper lampRemapper = new MAMELayoutMPU4ChrLampRemapper(mfmeLampTable, mameLampTable);

        //    foreach (ExtractComponentBase extractComponentBase in Components)
        //    {
        //        // TODO buttons, leds as lamps, any other lamp driven components
        //        if (extractComponentBase.GetType() != typeof(ExtractComponentLamp))
        //        {
        //            continue;
        //        }

        //        ExtractComponentLamp extractComponentLamp = (ExtractComponentLamp)extractComponentBase;

        //        // TODO I think only lamps 0-127 are scrambled
        //        // TODO just do zeroth for now in case of lamps, need to do all 12x lamp elements per mfme lamp component:
        //        if (extractComponentLamp.GetLampNumber(0) != null)
        //        {
        //            int originalLampNumber = (int)extractComponentLamp.GetLampNumber(0);
        //            int remappedLampNumber = lampRemapper.GetRemappedLampNumber(originalLampNumber);

        //            extractComponentLamp.LampElements[0].NumberAsText = remappedLampNumber.ToString();
        //        }

        //    }
        //}
    }
}
