namespace MfmeFmlDecoder.Model
{
    public enum ValueRole
    {
        RAW = 0,
        BOOLEAN,
        BYTE,
        FLOAT,
        ARGB_COLOR,
        BITMAP,
        CHECKBOX,
        DROPDOWN_CHOSEN_OPTION,
        UINT16,
        UINT32,
        INT32,
        UINT8_TUPLE,
        FONT,

        /// <summary>
        /// Number of sublamp mappings for this lamp. Pair with <see cref="LAMP_SUBLAMP_TABLE"/> in the same
        /// <see cref="ComponentTagMap"/> (typically tag <c>0x38</c>).
        /// </summary>
        SUBLAMP_COUNT,

        /// <summary>
        /// Variable-length table of <c>n</c> signed 32-bit sublamp numbers, one per sublamp index.
        /// Entry count comes from the <see cref="SUBLAMP_COUNT"/> tag in the same map (<c>n × 4</c> bytes).
        /// </summary>
        LAMP_SUBLAMP_TABLE,
        NESTED_TAG_BLOCK,

        /// <summary>
        /// MFME Unicode label / wide-string payloads used on several component tags.
        /// <para>Layout: <c>[preamble:4 bytes]</c>, <c>[0x3C tag:1]</c>, <c>[blockCount:1]</c>,
        /// then <c>blockCount</c> text blocks each encoded as
        /// <c>[subTag:1]</c>, <c>[charCount:uint32 LE]</c>, <c>UTF-16LE × charCount</c>.</para>
        /// </summary>
        TEXT,

        /// <summary>
        /// Repeated opaque TLV payloads in one extended tag value.
        /// <para>Layout: zero or more records of <c>[payloadLength:uint32 LE]</c>, <c>[payload:payloadLength bytes]</c>,
        /// terminated by a zero <c>payloadLength</c>, EOF, a <c>0x00</c> byte, or resumption of the outer TLV
        /// stream when the next byte is a tag id greater than this block's host tag key.</para>
        /// Parsed value is <see cref="List{T}"/> of <see cref="byte[]"/> — one entry per non-zero-length TLV payload
        /// (excluding each record's length prefix).
        /// </summary>
        TLVBLOCK,

        /// <summary>
        /// Fixed-length array of one-byte booleans (<c>0x00</c>/<c>0x01</c>), one flag per programmable "digit"
        /// (typically 48 entries on a SevenSegBlock, tag <c>0x0B</c>). Pair with <see cref="PROGRAMMABLE_LAMP_TABLE"/>
        /// in the same <see cref="ComponentTagMap"/>. Parsed value is the raw <see cref="byte[]"/> mask.
        /// </summary>
        PROGRAMMABLE_DIGIT_MASK,

        /// <summary>
        /// Fixed-length per-digit boolean flag array (<c>0x00</c>/<c>0x01</c> per digit, typically 48 entries),
        /// e.g. SevenSegBlock display flags such as DP-on/zero-on/auto-DP/visible/DP-off. Parsed value is a
        /// <see cref="bool[]"/>, one entry per digit.
        /// </summary>
        DIGIT_FLAG_ARRAY,

        /// <summary>
        /// Programmable-digit lamp table (SevenSegBlock tag <c>0x1B</c>): one block of eight little-endian
        /// <see cref="uint"/> lamp numbers per digit. The block count comes from the <see cref="PROGRAMMABLE_DIGIT_MASK"/>
        /// entry length in the same map (<c>digits × 8 × 4</c> bytes). Non-programmable digits are still present as
        /// eight <c>0xFFFFFFFF</c> sentinels. Parsed value is <see cref="IReadOnlyList{T}"/> of <see cref="uint[]"/>,
        /// one eight-element array per digit (in digit order, including sentinel blocks).
        /// </summary>
        PROGRAMMABLE_LAMP_TABLE,
    }
}