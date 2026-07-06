using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class BFMLED : BaseComponent
    {
        public uint XSize { get; set; }
        public uint YSize { get; set; }
        public uint DigitSpacing { get; set; }
        public uint LedSize { get; set; }
    }
}

