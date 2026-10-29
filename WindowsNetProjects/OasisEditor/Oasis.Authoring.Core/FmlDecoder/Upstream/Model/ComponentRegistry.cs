using System.Collections.Generic;

namespace MfmeFmlDecoder.Model  // Or whatever namespace you want the registry in
{
    public class ComponentRegistry
    {
        public Dictionary<MFMEComponentType, ComponentTagMap> ComponentTags =
            new Dictionary<MFMEComponentType, ComponentTagMap>
            {
                // Background component (0x01)
                [MFMEComponentType.Background] = new ComponentTagMap
                {

                    //// The Background component just follows the 'extended tagmap' foirmat
                    //// Tags are not in T L V format, just T V
                    //// L is determined by the tag number

                    //{ 0x3B, new TagInfo(0x04, "ComponentDataClass", [], ValueRole.UINT32) }, // I think this is some kind of struct identifier, doesn't seem to map to any data 
                    
                    //{ 0x01, new TagInfo(0x04, "Colour", [0xF0, 0xF0, 0xF0, 0xFF], TagType.Extended, ValueRole.ARGB_Colour) },
                    //{ 0x07, new TagInfo(0x04, "BorderColour", [0x00, 0x00, 0x00, 0xFF], TagType.Extended, ValueRole.ARGB_Colour) },
                    //{ 0x09, new TagInfo(0x04, "TransparentColour", [0x00, 0x00, 0x00, 0xFF], TagType.Extended, ValueRole.ARGB_Colour) },
                    
                    //// TODO: Cater for Transparency_None
                    ////  - come up with a good model for packaging options of this nature (target dropdown, maps to chosen option)
                    //{ 0x08, new TagInfo(0x01, "Transparency_UseColour", [0x00], TagType.Extended, ValueRole.DROPDOWN_CHOSEN_OPTION) },
                    //{ 0x0F, new TagInfo(0x01, "Transparency_UseAlphaChannel", [0x00], TagType.Extended, ValueRole.DROPDOWN_CHOSEN_OPTION) },
                    
                    //{ 0x0D, new TagInfo(0x01, "Tiled", [0x00], TagType.Extended, ValueRole.CHECKBOX) },
                    //{ 0x0E, new TagInfo(0x01, "RandomTile", [0x00], TagType.Extended, ValueRole.CHECKBOX) },
                    //{ 0x4B, new TagInfo(0x01, "NoOverLayInFullscreenMode", [0x00], TagType.Extended, ValueRole.CHECKBOX) },
                    
                    //{ 0x4C, new TagInfo(0x02, "OffsetXY", [0x00, 0x00], TagType.Extended, ValueRole.UINT8_TUPLE) },
                    //{ 0x0C, new TagInfo(0x36000000, "Bitmap", [], TagType.Extended, ValueRole.BITMAP) }, // Because more size information is found in the header, this will need to update the tag size as it goes
                },

                // Checkbox component (0x14)
                [MFMEComponentType.Checkbox] = new ComponentTagMap
                {
                    /**
                     Very odd stuff happens when we set Angle to a valid nonzero value and also set number

                        01 71 00 00 00     - X
                        02 62 00 00 00     - Y
                        03 19 00 00 00     - Width
                        04 49 00 00 00     - Height
                        05 08 00 00 00     - Number of the checkbox
                        07 00 08 5A 00     - Angle (32-bit signed fixed-point number with 17 fractional bits)
                        00                 - We actually get a terminator in this case, if number 0x05 is zero then this is absent.

                        00 17 01 18        - This is the old 'default' value previously seen in 0x07'
                        08                 - This is the number as seen in 0x05

                        I think the best way to proceed is to update the terminating conditions for the component tag loop to include number in 0x05 as a stop condition, as well as zero. Previous tag < 0x07 == don't stop.

                        We can perhaps find the correct 'conditional byte skips' for this scenario.

                        Number = 0x00 && Angle <= 360 -- skip 4 bytes after tag block.
                        Number > 0x00 && Angle > 360 (angle is undefined) -- no bytes to skip.
                        Number > 0x00 && Angle <= 360 -- skip 5 bytes after the tag block.
                     */

                    /*
                        Followed by a block with this structure, we should seek the start marker 00 00 00 27 from the end of the font block.

                        00 00 00 27 06 00 00 00 54 61 68 6F 6D 61 - Font name "Tahoma" 0x06 chars long.
                        0E -- 14 point font
                        00 00 00 
                        BA -- Script dropdown number (see table)
                        FF 80 00 00 -- Text colour RGBA
                        02 -- Font style (dropdown value 0x00 -> 0xFF)

                        -- Seems to be a fixed size set of flags, not sure the purpose.
                        3B 30 C7 6F 0D 3F 

                        -- The actual text itself
                        09 00 00 00 54 00 65 00 78 00 74 00 54 00 65 00 78 00 74 00 41 00 4C 00 00 

                        -- Optional, we may have the following two byte sequence.
                        01 01 - This indicates 'checked' == true, if we do not see this before the closing 0x00 then 'checked' == false

                        -- Close
                        00

                        Script dropdown index lookup table for checkbox
                        0xFF = 01
                        0xBA = 02
                        0xB2 = 03
                        0xA1 = 04
                        0xA2 = 05
                        0xB1 = 06
                        0xEE = 07
                        0xCC = 08
                        0xDE = 09
                        0x0E = 0A
                     */

                    //{ 0x01, new TagInfo(0x01, "Checked", [0x00], TagType.Extended, ValueRole.CHECKBOX) },
                },

                // Reel component (0x03)
                [MFMEComponentType.Reel] = new ComponentTagMap
                {
                },

                // Lamp component (0x04)
                [MFMEComponentType.Lamp] = new ComponentTagMap
                {
                },

                // Button component (0x05)
                [MFMEComponentType.Button] = new ComponentTagMap
                {
                },

                // DiscReel component (0x06)
                [MFMEComponentType.DiscReel] = new ComponentTagMap
                {
                },

                // Alpha component (0x07)
                [MFMEComponentType.Alpha] = new ComponentTagMap
                {
                },

                // BandReel component (0x08)
                [MFMEComponentType.BandReel] = new ComponentTagMap
                {
                },

                // Frame component (0x09)
                [MFMEComponentType.Frame] = new ComponentTagMap
                {
                },

                // Label component (0x0A)
                [MFMEComponentType.Label] = new ComponentTagMap
                {
                },

                // Bitmap component (0x0B)
                [MFMEComponentType.Bitmap] = new ComponentTagMap
                {
                },

                // BFMAlpha component (0x0C)
                [MFMEComponentType.BFMAlpha] = new ComponentTagMap
                {
                },

                // DotMatrix component (0x0D)
                [MFMEComponentType.DotMatrix] = new ComponentTagMap
                {
                },

                // SevenSeg component (0x0E)
                [MFMEComponentType.SevenSeg] = new ComponentTagMap
                {
                },

                // BarcrestBWBVideo component (0x0F)
                [MFMEComponentType.BarcrestBWBVideo] = new ComponentTagMap
                {
                },

                // AceMatrix component (0x10)
                [MFMEComponentType.AceMatrix] = new ComponentTagMap
                {
                },

                // ProconnMatrix component (0x11)
                [MFMEComponentType.ProconnMatrix] = new ComponentTagMap
                {
                },

                // Led component (0x12)
                [MFMEComponentType.Led] = new ComponentTagMap
                {
                },

                // DotAlpha component (0x13)
                [MFMEComponentType.DotAlpha] = new ComponentTagMap
                {
                },

                // BFMLED component (0x15)
                [MFMEComponentType.BFMLED] = new ComponentTagMap
                {
                },

                // EpochDotAlpha component (0x16)
                [MFMEComponentType.EpochDotAlpha] = new ComponentTagMap
                {
                },

                // AlphaNew component (0x19)
                [MFMEComponentType.AlphaNew] = new ComponentTagMap
                {
                },

                // MatrixAlpha component (0x1A)
                [MFMEComponentType.MatrixAlpha] = new ComponentTagMap
                {
                },

                // SevenSegBlock component (0x1C)
                [MFMEComponentType.SevenSegBlock] = new ComponentTagMap
                {
                },

                // BFMVideo component (0x1F)
                [MFMEComponentType.BFMVideo] = new ComponentTagMap
                {
                },

                // IGTVfd component (0x20)
                [MFMEComponentType.IGTVfd] = new ComponentTagMap
                {
                },

                // AceVideo component (0x21)
                [MFMEComponentType.AceVideo] = new ComponentTagMap
                {
                },

                // EpochMatrix component (0x22)
                [MFMEComponentType.EpochMatrix] = new ComponentTagMap
                {
                },

                // PlasmaDisplay component (0x23)
                [MFMEComponentType.PlasmaDisplay] = new ComponentTagMap
                {
                },

                // BFMColourLed component (0x24)
                [MFMEComponentType.BFMColourLed] = new ComponentTagMap
                {
                },

                // RGBLed component (0x26)
                [MFMEComponentType.RGBLed] = new ComponentTagMap
                {
                },

                // ReelBonusReel component (0x27)
                [MFMEComponentType.ReelBonusReel] = new ComponentTagMap
                {
                },

                // MaygayVideo component (0x28)
                [MFMEComponentType.MaygayVideo] = new ComponentTagMap
                {
                },

                // PrismLamp component (0x29)
                [MFMEComponentType.PrismLamp] = new ComponentTagMap
                {
                },

                // AstraMatrix component (0x2C)
                [MFMEComponentType.AstraMatrix] = new ComponentTagMap
                {
                },

                // FlipReel component (0x2D)
                [MFMEComponentType.FlipReel] = new ComponentTagMap
                {
                },

                // MaygayMatrix component (0x2E)
                [MFMEComponentType.MaygayMatrix] = new ComponentTagMap
                {
                },

                // CoinmasterVideo component (0x2F)
                [MFMEComponentType.CoinmasterVideo] = new ComponentTagMap
                {
                },
                [MFMEComponentType.BaseClass] = new ComponentTagMap
                {
                    //{ 0x01, new TagInfo(4, "X", [], TagType.Component) },
                    //{ 0x02, new TagInfo(4, "Y", [], TagType.Component) },
                    //{ 0x03, new TagInfo(4, "Width", [], TagType.Component) },
                    //{ 0x04, new TagInfo(4, "Height", [], TagType.Component) },
                    //{ 0x05, new TagInfo(4, "Angle", [0x00, 0x00, 0x00, 0x00], TagType.Component) },
                    //{ 0x06, new TagInfo(4, "Unknow 0x06", [], TagType.Component) },
                    //{ 0x07, new TagInfo(4, "Unknown 0x07", [], TagType.Component) }, { 0x08, new TagInfo(4, "Unknown 0x08", [], TagType.Component) }, { 0x09, new TagInfo(4, "Unknown 0x09", [], TagType.Component) }, { 0x0A, new TagInfo(4, "Unknown 0x0A", [], TagType.Component) },
                    //{ 0x0B, new TagInfo(4, "Unknown 0x0B", [], TagType.Component) }, { 0x0C, new TagInfo(4, "Unknown 0x0C", [], TagType.Component) }, { 0x0D, new TagInfo(4, "Unknown 0x0D", [], TagType.Component) }, { 0x0E, new TagInfo(4, "Unknown 0x0E", [], TagType.Component) },
                    //{ 0x0F, new TagInfo(4, "Unknown 0x0F", [], TagType.Component) }, { 0x10, new TagInfo(4, "Unknown 0x10", [], TagType.Component) }, { 0x11, new TagInfo(4, "Unknown 0x11", [], TagType.Component) }, { 0x12, new TagInfo(4, "Unknown 0x12", [], TagType.Component) },
                    //{ 0x13, new TagInfo(4, "Unknown 0x13", [], TagType.Component) }, { 0x14, new TagInfo(4, "Unknown 0x14", [], TagType.Component) }, { 0x15, new TagInfo(4, "Unknown 0x15", [], TagType.Component) }, { 0x16, new TagInfo(4, "Unknown 0x16", [], TagType.Component) },
                    //{ 0x17, new TagInfo(4, "Unknown 0x17", [], TagType.Component) }, { 0x18, new TagInfo(4, "Unknown 0x18", [], TagType.Component) }, { 0x19, new TagInfo(4, "Unknown 0x19", [], TagType.Component) }, { 0x1A, new TagInfo(4, "Unknown 0x1A", [], TagType.Component) },
                    //{ 0x1B, new TagInfo(4, "Unknown 0x1B", [], TagType.Component) }, { 0x1C, new TagInfo(4, "Unknown 0x1C", [], TagType.Component) }, { 0x1D, new TagInfo(4, "Unknown 0x1D", [], TagType.Component) }, { 0x1E, new TagInfo(4, "Unknown 0x1E", [], TagType.Component) },
                    //{ 0x1F, new TagInfo(4, "Unknown 0x1F", [], TagType.Component) }, { 0x20, new TagInfo(4, "Unknown 0x20", [], TagType.Component) }, { 0x21, new TagInfo(4, "Unknown 0x21", [], TagType.Component) }, { 0x22, new TagInfo(4, "Unknown 0x22", [], TagType.Component) },
                    //{ 0x23, new TagInfo(4, "Unknown 0x23", [], TagType.Component) }, { 0x24, new TagInfo(4, "Unknown 0x24", [], TagType.Component) }, { 0x25, new TagInfo(4, "Unknown 0x25", [], TagType.Component) }, { 0x26, new TagInfo(4, "Unknown 0x26", [], TagType.Component) },
                    //{ 0x27, new TagInfo(4, "Unknown 0x27", [], TagType.Component) }, { 0x28, new TagInfo(4, "Unknown 0x28", [], TagType.Component) }, { 0x29, new TagInfo(4, "Unknown 0x29", [], TagType.Component) }, { 0x2A, new TagInfo(4, "Unknown 0x2A", [], TagType.Component) },
                    //{ 0x2B, new TagInfo(4, "Unknown 0x2B", [], TagType.Component) }, { 0x2C, new TagInfo(4, "Unknown 0x2C", [], TagType.Component) }, { 0x2D, new TagInfo(4, "Unknown 0x2D", [], TagType.Component) }, { 0x2E, new TagInfo(4, "Unknown 0x2E", [], TagType.Component) },
                    //{ 0x2F, new TagInfo(4, "Unknown 0x2F", [], TagType.Component) }, { 0x30, new TagInfo(4, "Unknown 0x30", [], TagType.Component) }, { 0x31, new TagInfo(4, "Unknown 0x31", [], TagType.Component) }, { 0x32, new TagInfo(4, "Unknown 0x32", [], TagType.Component) },
                    //{ 0x33, new TagInfo(4, "Unknown 0x33", [], TagType.Component) }, { 0x34, new TagInfo(4, "Unknown 0x34", [], TagType.Component) }, { 0x35, new TagInfo(4, "Unknown 0x35", [], TagType.Component) }, { 0x36, new TagInfo(4, "Unknown 0x36", [], TagType.Component) },
                    //{ 0x37, new TagInfo(4, "Unknown 0x37", [], TagType.Component) }, { 0x38, new TagInfo(4, "Unknown 0x38", [], TagType.Component) }, { 0x39, new TagInfo(4, "Unknown 0x39", [], TagType.Component) }, { 0x3A, new TagInfo(4, "Unknown 0x3A", [], TagType.Component) },
                    //{ 0x3B, new TagInfo(4, "Unknown 0x3B", [], TagType.Component) }, { 0x3C, new TagInfo(4, "Unknown 0x3C", [], TagType.Component) }, { 0x3D, new TagInfo(4, "Unknown 0x3D", [], TagType.Component) }, { 0x3E, new TagInfo(4, "Unknown 0x3E", [], TagType.Component) },
                    //{ 0x3F, new TagInfo(4, "Unknown 0x3F", [], TagType.Component) }, { 0x40, new TagInfo(4, "Unknown 0x40", [], TagType.Component) }, { 0x41, new TagInfo(4, "Unknown 0x41", [], TagType.Component) }, { 0x42, new TagInfo(4, "Unknown 0x42", [], TagType.Component) },
                    //{ 0x43, new TagInfo(4, "Unknown 0x43", [], TagType.Component) }, { 0x44, new TagInfo(4, "Unknown 0x44", [], TagType.Component) }, { 0x45, new TagInfo(4, "Unknown 0x45", [], TagType.Component) }, { 0x46, new TagInfo(4, "Unknown 0x46", [], TagType.Component) },
                    //{ 0x47, new TagInfo(4, "Unknown 0x47", [], TagType.Component) }, { 0x48, new TagInfo(4, "Unknown 0x48", [], TagType.Component) }, { 0x49, new TagInfo(4, "Unknown 0x49", [], TagType.Component) }, { 0x4A, new TagInfo(4, "Unknown 0x4A", [], TagType.Component) },
                    //{ 0x4B, new TagInfo(4, "Unknown 0x4B", [], TagType.Component) }, { 0x4C, new TagInfo(4, "Unknown 0x4C", [], TagType.Component) }, { 0x4D, new TagInfo(4, "Unknown 0x4D", [], TagType.Component) }, { 0x4E, new TagInfo(4, "Unknown 0x4E", [], TagType.Component) },
                    //{ 0x4F, new TagInfo(4, "Unknown 0x4F", [], TagType.Component) }, { 0x50, new TagInfo(4, "Unknown 0x50", [], TagType.Component) }, { 0x51, new TagInfo(4, "Unknown 0x51", [], TagType.Component) }, { 0x52, new TagInfo(4, "Unknown 0x52", [], TagType.Component) },
                    //{ 0x53, new TagInfo(4, "Unknown 0x53", [], TagType.Component) }, { 0x54, new TagInfo(4, "Unknown 0x54", [], TagType.Component) }, { 0x55, new TagInfo(4, "Unknown 0x55", [], TagType.Component) }, { 0x56, new TagInfo(4, "Unknown 0x56", [], TagType.Component) },
                    //{ 0x57, new TagInfo(4, "Unknown 0x57", [], TagType.Component) }, { 0x58, new TagInfo(4, "Unknown 0x58", [], TagType.Component) }, { 0x59, new TagInfo(4, "Unknown 0x59", [], TagType.Component) }, { 0x5A, new TagInfo(4, "Unknown 0x5A", [], TagType.Component) },
                    //{ 0x5B, new TagInfo(4, "Unknown 0x5B", [], TagType.Component) }, { 0x5C, new TagInfo(4, "Unknown 0x5C", [], TagType.Component) }, { 0x5D, new TagInfo(4, "Unknown 0x5D", [], TagType.Component) }, { 0x5E, new TagInfo(4, "Unknown 0x5E", [], TagType.Component) },
                    //{ 0x5F, new TagInfo(4, "Unknown 0x5F", [], TagType.Component) }, { 0x60, new TagInfo(4, "Unknown 0x60", [], TagType.Component) }, { 0x61, new TagInfo(4, "Unknown 0x61", [], TagType.Component) }, { 0x62, new TagInfo(4, "Unknown 0x62", [], TagType.Component) },
                    //{ 0x63, new TagInfo(4, "Unknown 0x63", [], TagType.Component) }, { 0x64, new TagInfo(4, "Unknown 0x64", [], TagType.Component) }, { 0x65, new TagInfo(4, "Unknown 0x65", [], TagType.Component) }, { 0x66, new TagInfo(4, "Unknown 0x66", [], TagType.Component) },
                    //{ 0x67, new TagInfo(4, "Unknown 0x67", [], TagType.Component) }, { 0x68, new TagInfo(4, "Unknown 0x68", [], TagType.Component) }, { 0x69, new TagInfo(4, "Unknown 0x69", [], TagType.Component) }, { 0x6A, new TagInfo(4, "Unknown 0x6A", [], TagType.Component) },
                    //{ 0x6B, new TagInfo(4, "Unknown 0x6B", [], TagType.Component) }, { 0x6C, new TagInfo(4, "Unknown 0x6C", [], TagType.Component) }, { 0x6D, new TagInfo(4, "Unknown 0x6D", [], TagType.Component) }, { 0x6E, new TagInfo(4, "Unknown 0x6E", [], TagType.Component) },
                    //{ 0x6F, new TagInfo(4, "Unknown 0x6F", [], TagType.Component) }, { 0x70, new TagInfo(4, "Unknown 0x70", [], TagType.Component) }, { 0x71, new TagInfo(4, "Unknown 0x71", [], TagType.Component) }, { 0x72, new TagInfo(4, "Unknown 0x72", [], TagType.Component) },
                    //{ 0x73, new TagInfo(4, "Unknown 0x73", [], TagType.Component) }, { 0x74, new TagInfo(4, "Unknown 0x74", [], TagType.Component) }, { 0x75, new TagInfo(4, "Unknown 0x75", [], TagType.Component) }, { 0x76, new TagInfo(4, "Unknown 0x76", [], TagType.Component) },
                    //{ 0x77, new TagInfo(4, "Unknown 0x77", [], TagType.Component) }, { 0x78, new TagInfo(4, "Unknown 0x78", [], TagType.Component) }, { 0x79, new TagInfo(4, "Unknown 0x79", [], TagType.Component) }, { 0x7A, new TagInfo(4, "Unknown 0x7A", [], TagType.Component) },
                    //{ 0x7B, new TagInfo(4, "Unknown 0x7B", [], TagType.Component) }, { 0x7C, new TagInfo(4, "Unknown 0x7C", [], TagType.Component) }, { 0x7D, new TagInfo(4, "Unknown 0x7D", [], TagType.Component) }, { 0x7E, new TagInfo(4, "Unknown 0x7E", [], TagType.Component) },
                    //{ 0x7F, new TagInfo(4, "Unknown 0x7F", [], TagType.Component) }, { 0x80, new TagInfo(4, "Unknown 0x80", [], TagType.Component) }, { 0x81, new TagInfo(4, "Unknown 0x81", [], TagType.Component) }, { 0x82, new TagInfo(4, "Unknown 0x82", [], TagType.Component) },
                    //{ 0x83, new TagInfo(4, "Unknown 0x83", [], TagType.Component) }, { 0x84, new TagInfo(4, "Unknown 0x84", [], TagType.Component) }, { 0x85, new TagInfo(4, "Unknown 0x85", [], TagType.Component) }, { 0x86, new TagInfo(4, "Unknown 0x86", [], TagType.Component) },
                    //{ 0x87, new TagInfo(4, "Unknown 0x87", [], TagType.Component) }, { 0x88, new TagInfo(4, "Unknown 0x88", [], TagType.Component) }, { 0x89, new TagInfo(4, "Unknown 0x89", [], TagType.Component) }, { 0x8A, new TagInfo(4, "Unknown 0x8A", [], TagType.Component) },
                    //{ 0x8B, new TagInfo(4, "Unknown 0x8B", [], TagType.Component) }, { 0x8C, new TagInfo(4, "Unknown 0x8C", [], TagType.Component) }, { 0x8D, new TagInfo(4, "Unknown 0x8D", [], TagType.Component) }, { 0x8E, new TagInfo(4, "Unknown 0x8E", [], TagType.Component) },
                    //{ 0x8F, new TagInfo(4, "Unknown 0x8F", [], TagType.Component) }, { 0x90, new TagInfo(4, "Unknown 0x90", [], TagType.Component) }, { 0x91, new TagInfo(4, "Unknown 0x91", [], TagType.Component) }, { 0x92, new TagInfo(4, "Unknown 0x92", [], TagType.Component) },
                    //{ 0x93, new TagInfo(4, "Unknown 0x93", [], TagType.Component) }, { 0x94, new TagInfo(4, "Unknown 0x94", [], TagType.Component) }, { 0x95, new TagInfo(4, "Unknown 0x95", [], TagType.Component) }, { 0x96, new TagInfo(4, "Unknown 0x96", [], TagType.Component) },
                    //{ 0x97, new TagInfo(4, "Unknown 0x97", [], TagType.Component) }, { 0x98, new TagInfo(4, "Unknown 0x98", [], TagType.Component) }, { 0x99, new TagInfo(4, "Unknown 0x99", [], TagType.Component) }, { 0x9A, new TagInfo(4, "Unknown 0x9A", [], TagType.Component) },
                    //{ 0x9B, new TagInfo(4, "Unknown 0x9B", [], TagType.Component) }, { 0x9C, new TagInfo(4, "Unknown 0x9C", [], TagType.Component) }, { 0x9D, new TagInfo(4, "Unknown 0x9D", [], TagType.Component) }, { 0x9E, new TagInfo(4, "Unknown 0x9E", [], TagType.Component) },
                    //{ 0x9F, new TagInfo(4, "Unknown 0x9F", [], TagType.Component) }, { 0xA0, new TagInfo(4, "Unknown 0xA0", [], TagType.Component) }, { 0xA1, new TagInfo(4, "Unknown 0xA1", [], TagType.Component) }, { 0xA2, new TagInfo(4, "Unknown 0xA2", [], TagType.Component) },
                    //{ 0xA3, new TagInfo(4, "Unknown 0xA3", [], TagType.Component) }, { 0xA4, new TagInfo(4, "Unknown 0xA4", [], TagType.Component) }, { 0xA5, new TagInfo(4, "Unknown 0xA5", [], TagType.Component) }, { 0xA6, new TagInfo(4, "Unknown 0xA6", [], TagType.Component) },
                    //{ 0xA7, new TagInfo(4, "Unknown 0xA7", [], TagType.Component) }, { 0xA8, new TagInfo(4, "Unknown 0xA8", [], TagType.Component) }, { 0xA9, new TagInfo(4, "Unknown 0xA9", [], TagType.Component) }, { 0xAA, new TagInfo(4, "Unknown 0xAA", [], TagType.Component) },
                    //{ 0xAB, new TagInfo(4, "Unknown 0xAB", [], TagType.Component) }, { 0xAC, new TagInfo(4, "Unknown 0xAC", [], TagType.Component) }, { 0xAD, new TagInfo(4, "Unknown 0xAD", [], TagType.Component) }, { 0xAE, new TagInfo(4, "Unknown 0xAE", [], TagType.Component) },
                    //{ 0xAF, new TagInfo(4, "Unknown 0xAF", [], TagType.Component) }, { 0xB0, new TagInfo(4, "Unknown 0xB0", [], TagType.Component) }, { 0xB1, new TagInfo(4, "Unknown 0xB1", [], TagType.Component) }, { 0xB2, new TagInfo(4, "Unknown 0xB2", [], TagType.Component) },
                    //{ 0xB3, new TagInfo(4, "Unknown 0xB3", [], TagType.Component) }, { 0xB4, new TagInfo(4, "Unknown 0xB4", [], TagType.Component) }, { 0xB5, new TagInfo(4, "Unknown 0xB5", [], TagType.Component) }, { 0xB6, new TagInfo(4, "Unknown 0xB6", [], TagType.Component) },
                    //{ 0xB7, new TagInfo(4, "Unknown 0xB7", [], TagType.Component) }, { 0xB8, new TagInfo(4, "Unknown 0xB8", [], TagType.Component) }, { 0xB9, new TagInfo(4, "Unknown 0xB9", [], TagType.Component) }, { 0xBA, new TagInfo(4, "Unknown 0xBA", [], TagType.Component) },
                    //{ 0xBB, new TagInfo(4, "Unknown 0xBB", [], TagType.Component) }, { 0xBC, new TagInfo(4, "Unknown 0xBC", [], TagType.Component) }, { 0xBD, new TagInfo(4, "Unknown 0xBD", [], TagType.Component) }, { 0xBE, new TagInfo(4, "Unknown 0xBE", [], TagType.Component) },
                    //{ 0xBF, new TagInfo(4, "Unknown 0xBF", [], TagType.Component) }, { 0xC0, new TagInfo(4, "Unknown 0xC0", [], TagType.Component) }, { 0xC1, new TagInfo(4, "Unknown 0xC1", [], TagType.Component) }, { 0xC2, new TagInfo(4, "Unknown 0xC2", [], TagType.Component) },
                    //{ 0xC3, new TagInfo(4, "Unknown 0xC3", [], TagType.Component) }, { 0xC4, new TagInfo(4, "Unknown 0xC4", [], TagType.Component) }, { 0xC5, new TagInfo(4, "Unknown 0xC5", [], TagType.Component) }, { 0xC6, new TagInfo(4, "Unknown 0xC6", [], TagType.Component) },
                    //{ 0xC7, new TagInfo(4, "Unknown 0xC7", [], TagType.Component) }, { 0xC8, new TagInfo(4, "Unknown 0xC8", [], TagType.Component) }, { 0xC9, new TagInfo(4, "Unknown 0xC9", [], TagType.Component) }, { 0xCA, new TagInfo(4, "Unknown 0xCA", [], TagType.Component) },
                    //{ 0xCB, new TagInfo(4, "Unknown 0xCB", [], TagType.Component) }, { 0xCC, new TagInfo(4, "Unknown 0xCC", [], TagType.Component) }, { 0xCD, new TagInfo(4, "Unknown 0xCD", [], TagType.Component) }, { 0xCE, new TagInfo(4, "Unknown 0xCE", [], TagType.Component) },
                    //{ 0xCF, new TagInfo(4, "Unknown 0xCF", [], TagType.Component) }, { 0xD0, new TagInfo(4, "Unknown 0xD0", [], TagType.Component) }, { 0xD1, new TagInfo(4, "Unknown 0xD1", [], TagType.Component) }, { 0xD2, new TagInfo(4, "Unknown 0xD2", [], TagType.Component) },
                    //{ 0xD3, new TagInfo(4, "Unknown 0xD3", [], TagType.Component) }, { 0xD4, new TagInfo(4, "Unknown 0xD4", [], TagType.Component) }, { 0xD5, new TagInfo(4, "Unknown 0xD5", [], TagType.Component) }, { 0xD6, new TagInfo(4, "Unknown 0xD6", [], TagType.Component) },
                    //{ 0xD7, new TagInfo(4, "Unknown 0xD7", [], TagType.Component) }, { 0xD8, new TagInfo(4, "Unknown 0xD8", [], TagType.Component) }, { 0xD9, new TagInfo(4, "Unknown 0xD9", [], TagType.Component) }, { 0xDA, new TagInfo(4, "Unknown 0xDA", [], TagType.Component) },
                    //{ 0xDB, new TagInfo(4, "Unknown 0xDB", [], TagType.Component) }, { 0xDC, new TagInfo(4, "Unknown 0xDC", [], TagType.Component) }, { 0xDD, new TagInfo(4, "Unknown 0xDD", [], TagType.Component) }, { 0xDE, new TagInfo(4, "Unknown 0xDE", [], TagType.Component) },
                    //{ 0xDF, new TagInfo(4, "Unknown 0xDF", [], TagType.Component) }, { 0xE0, new TagInfo(4, "Unknown 0xE0", [], TagType.Component) }, { 0xE1, new TagInfo(4, "Unknown 0xE1", [], TagType.Component) }, { 0xE2, new TagInfo(4, "Unknown 0xE2", [], TagType.Component) },
                    //{ 0xE3, new TagInfo(4, "Unknown 0xE3", [], TagType.Component) }, { 0xE4, new TagInfo(4, "Unknown 0xE4", [], TagType.Component) }, { 0xE5, new TagInfo(4, "Unknown 0xE5", [], TagType.Component) }, { 0xE6, new TagInfo(4, "Unknown 0xE6", [], TagType.Component) },
                    //{ 0xE7, new TagInfo(4, "Unknown 0xE7", [], TagType.Component) }, { 0xE8, new TagInfo(4, "Unknown 0xE8", [], TagType.Component) }, { 0xE9, new TagInfo(4, "Unknown 0xE9", [], TagType.Component) }, { 0xEA, new TagInfo(4, "Unknown 0xEA", [], TagType.Component) },
                    //{ 0xEB, new TagInfo(4, "Unknown 0xEB", [], TagType.Component) }, { 0xEC, new TagInfo(4, "Unknown 0xEC", [], TagType.Component) }, { 0xED, new TagInfo(4, "Unknown 0xED", [], TagType.Component) }, { 0xEE, new TagInfo(4, "Unknown 0xEE", [], TagType.Component) },
                    //{ 0xEF, new TagInfo(4, "Unknown 0xEF", [], TagType.Component) }, { 0xF0, new TagInfo(4, "Unknown 0xF0", [], TagType.Component) }, { 0xF1, new TagInfo(4, "Unknown 0xF1", [], TagType.Component) }, { 0xF2, new TagInfo(4, "Unknown 0xF2", [], TagType.Component) },
                    //{ 0xF3, new TagInfo(4, "Unknown 0xF3", [], TagType.Component) }, { 0xF4, new TagInfo(4, "Unknown 0xF4", [], TagType.Component) }, { 0xF5, new TagInfo(4, "Unknown 0xF5", [], TagType.Component) }, { 0xF6, new TagInfo(4, "Unknown 0xF6", [], TagType.Component) },
                    //{ 0xF7, new TagInfo(4, "Unknown 0xF7", [], TagType.Component) }, { 0xF8, new TagInfo(4, "Unknown 0xF8", [], TagType.Component) }, { 0xF9, new TagInfo(4, "Unknown 0xF9", [], TagType.Component) }, { 0xFA, new TagInfo(4, "Unknown 0xFA", [], TagType.Component) },
                    //{ 0xFB, new TagInfo(4, "Unknown 0xFB", [], TagType.Component) }, { 0xFC, new TagInfo(4, "Unknown 0xFC", [], TagType.Component) }, { 0xFD, new TagInfo(4, "Unknown 0xFD", [], TagType.Component) }, { 0xFE, new TagInfo(4, "Unknown 0xFE", [], TagType.Component) },
                    //{ 0xFF, new TagInfo(4, "Unknown 0xFF", [], TagType.Component) },
                },
            };
    }
}