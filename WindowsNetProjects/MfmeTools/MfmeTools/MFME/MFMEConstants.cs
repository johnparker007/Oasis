namespace MfmeTools.Mfme
{
    public static class MFMEConstants
    {
        public enum MFMEComponentType
        {
            None,
            Background,
            MatrixAlpha, // (nothing set up yet for these, these all get processed as Dot Alphas)
            SevenSegment,
            Reel,
            Lamp,
            Checkbox,
            Label,
            Button,
            Led,
            RgbLed,
            DotAlpha,
            AlphaNew,
            Alpha,  // (legacy font based one, also used for data layout 'binary font')
            Frame,
            BandReel,
            DiscReel,
            FlipReel,
            JpmBonusReel,
            BfmAlpha,
            ProconnMatrix,
            EpochAlpha,
            IgtVfd,
            Plasma,
            DotMatrix,
            BfmLed,
            BfmColourLed,
            AceMatrix,
            EpochMatrix,
            SevenSegmentBlock,
            BarcrestBwbVideo,
            BfmVideo,
            AceVideo,
            MaygayVideo,
            PrismLamp,
            Bitmap,
            Border
        }

        public enum MFMELampType
        {
            Off,
            On1,
            On2,
            On3,
            On4
        }

        public enum MFMELampShape
        {
            Rectangle,
            Square,
            RectRound,
            SquareRound,
            Ellipse,
            Circle,
            Diamond,
            Star,
            Polygon,
            TriangleLeft,
            TriangleRight,
            TriangleUp,
            TriangleDown,
            SemiCircleLeft,
            SemiCircleRight,
            SemiCircleUp,
            SemiCircleDown,
            Pie
        }

        public enum MFMECharacterSetType
        {
            OldCharset,
            OKI1937,
            BFMCharset
        }

        // Component BandReel
        public static readonly int kBandReelLampMaskCount = 3; // just scrape the initial page to get started
        public static readonly int kBandReelLampColumns = 2;
        public static readonly int kBandReelLampRows = 5;
        public static readonly int kBandReelLampCount = kBandReelLampColumns * kBandReelLampRows;

        // Component Reel
        public static readonly int kReelLampColumns = 3;
        public static readonly int kReelLampRows = 5;
        public static readonly int kReelLampCount = kReelLampColumns * kReelLampRows;

    }
}
