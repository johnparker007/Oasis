using System.Collections.Generic;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Model
{
    /// <summary>
    /// File-level TLV metadata from the layout prologue (before the <c>0xFFFFFFFF</c> terminator).
    /// </summary>
    internal sealed class LayoutFileHeader
    {
        internal const uint LayoutDescriptionTag = 0x0C;
        internal const uint TextNotesTag = 0x76;
        internal const uint SplashBitmapTag = 0x98;

        internal const int LayoutDescriptionLength = 101;

        public string Description { get; set; } = string.Empty;

        public string TextNotes { get; set; } = string.Empty;

        /// <summary>
        /// Splash bitmap bytes when tag <c>0x98</c> carries a BMP payload; otherwise null.
        /// </summary>
        public BitmapEntry SplashBitmap { get; set; }

        public bool HasSplash => SplashBitmap is not null;

        public IReadOnlyDictionary<string, BitmapEntry> Images
        {
            get
            {
                if (SplashBitmap is null)
                {
                    return EmptyImages;
                }

                return new Dictionary<string, BitmapEntry>(1)
                {
                    [SplashBitmapImageKey] = SplashBitmap
                };
            }
        }

        public const string SplashBitmapImageKey = "splash_bitmap";

        private static readonly IReadOnlyDictionary<string, BitmapEntry> EmptyImages =
            new Dictionary<string, BitmapEntry>(0);
    }
}
