using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class FlipReel : BaseComponent
    {
        public uint Stops { get; set; }

        public uint HalfSteps { get; set; }

        public uint Offset {  get; set; }

        public uint BorderWidth { get; set; }

        public bool LampsEnabled { get; set; }

        public bool Inverted { get; set; }

        public uint SubLamp1Number { get; set; }
        public uint SubLamp2Number { get; set; }
        public uint SubLamp3Number { get; set; }

        // Only here for posterity - MFME does not properly export this.
        // public bool Reversed { get; set; }
    }
}

