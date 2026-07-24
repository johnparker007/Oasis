using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class Label : BaseComponent
    {
        /// <summary>
        /// Lamp index from extended TLV <c>0x39</c> (INT32).
        /// Null when MFME's undefined sentinel (<c>-2</c> / <c>0xFFFFFFFE</c>) was present;
        /// undefined lamps are omitted from JSON Values.
        /// </summary>
        [JsonIgnore]
        public int? Lamp { get; set; }
    }
}

