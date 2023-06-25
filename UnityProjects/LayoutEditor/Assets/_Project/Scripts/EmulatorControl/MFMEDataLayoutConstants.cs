using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MFMEDataLayoutConstants
{
    public static readonly int kDataLayoutReelY = 0;
    public static readonly int kDataLayoutReelXStart = (2 * 16) + 2; // rough - to get just past right side of mini lamp matrix
    public static readonly int kDataLayoutReelWidth = 2;

    public static readonly int kDataLayoutAlphaY = 4; // rough - to get just below reels
    public static readonly int kDataLayoutAlphaXStart = kDataLayoutReelXStart; // rough - to get just past right side of mini lamp matrix
    public static readonly int kDataLayoutAlphaWidth = 102;

    public static readonly int kDataLayoutSevenSegmentY = 99; 
    public static readonly int kDataLayoutSevenSegmentXStart = 0; 
    public static readonly int kDataLayoutSevenSegmentWidth = 8;

    public static readonly int kDataLayoutDummyLampsX = 224;

    // just putting checkboxes off the right side of the layout for now
    public static readonly int kDataLayoutCheckboxOffscreenX = 9999;
}
