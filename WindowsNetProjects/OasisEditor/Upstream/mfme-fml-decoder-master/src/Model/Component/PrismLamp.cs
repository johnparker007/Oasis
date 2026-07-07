using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class PrismLamp : BaseComponent
    {
        public uint HorizontalSpacing { get; set; }

        public uint VerticalSpacing { get; set; }

        public uint Tilt {  get; set; }

        public bool Style { get; set; }

        public bool IsHorizontal { get; set; }

        public bool CenterLine { get; set; }
        public uint SubLamp1Number { get; set; }
        public uint SubLamp2Number { get; set; }
    }
}

