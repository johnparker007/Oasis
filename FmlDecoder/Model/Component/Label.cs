namespace MfmeFmlDecoder.src.Model.Component
{
    internal class Label : BaseComponent
    {
        /// <summary>Lamp index from extended TLV <c>0x39</c> (UInt32).</summary>
        public uint Lamp { get; set; }
    }
}
