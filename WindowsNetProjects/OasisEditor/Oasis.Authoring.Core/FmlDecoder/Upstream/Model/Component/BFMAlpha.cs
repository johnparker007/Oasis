using MfmeFmlDecoder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MfmeFmlDecoder.src.Model.Component
{
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
    internal class BFMAlpha : BaseComponent
    {
        public uint OffLevel { get; set; }
        public uint DigitWidth { get; set; }

        public uint Columns { get; set; }
        public bool Reversed { get; set; }

        [JsonIgnore]
        public BitmapEntry[] Bitmaps { get; set; } = Array.Empty<BitmapEntry>();

    }
}

