using MfmeFmlDecoder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class BFMAlpha : BaseComponent
    {
        public uint OffLevel { get; set; }
        public uint DigitWidth { get; set; }

        public uint Columns { get; set; }
        public bool Reversed { get; set; }

        public BitmapEntry[] Bitmaps { get; set; } = Array.Empty<BitmapEntry>();

    }
}

