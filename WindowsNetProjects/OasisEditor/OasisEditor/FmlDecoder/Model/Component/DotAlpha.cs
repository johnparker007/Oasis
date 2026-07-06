using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class DotAlpha : BaseComponent
    {
        public uint XSize {  get; set; }
        public uint YSize { get; set; }
        public uint DotSpacing { get; set; }
        public uint DigitSpacing { get; set; }

    }
}
