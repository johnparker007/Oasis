namespace MfmeFmlDecoder.Model
{
    public enum TagType
    {
        Component,  // A mapping for a tag in the component section of a component descriptor
        XTLV, // A small TLV that appears after component and before extended
        Extended,    // A mapping for a tag in the extended section of a component descriptor
    }
}
