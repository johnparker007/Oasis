using MfmeFmlDecoder.Model;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.Utilities
{
    internal static class BitmapEntryUtility
    {
        public static BitmapEntry[] BuildEntries(IReadOnlyList<byte[]> bitmaps)
        {
            if (bitmaps is null || bitmaps.Count == 0) return Array.Empty<BitmapEntry>();

            BitmapEntry[] entries = new BitmapEntry[bitmaps.Count];
            for (int i = 0; i < bitmaps.Count; i++)
            {
                byte[] bytes = bitmaps[i] ?? Array.Empty<byte>();
                var info = BmpUtility.ReadInfo(bytes);
                entries[i] = new BitmapEntry(info.Width, info.Height, info.BitsPerPixel, bytes);
            }
            return entries;
        }

        public static BitmapEntry BuildEntry(IReadOnlyList<byte[]> bitmaps)
        {
            if (bitmaps is null || bitmaps.Count == 0) return null;

            byte[] bytes = bitmaps[0] ?? Array.Empty<byte>();
            var info = BmpUtility.ReadInfo(bytes);
            return new BitmapEntry(info.Width, info.Height, info.BitsPerPixel, bytes);
        }

        public static Dictionary<uint, BitmapEntry> BuildEntryByTag(IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> bitmapsByTag)
        {
            Dictionary<uint, BitmapEntry> result = new();
            if (bitmapsByTag is null || bitmapsByTag.Count == 0) return result;

            foreach (var kvp in bitmapsByTag)
            {
                BitmapEntry entry = BuildEntry(kvp.Value);
                if (entry is not null)
                {
                    result[kvp.Key] = entry;
                }
            }

            return result;
        }

        public static Dictionary<uint, BitmapEntry[]> BuildEntriesByTag(IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> bitmapsByTag)
        {
            Dictionary<uint, BitmapEntry[]> result = new();
            if (bitmapsByTag is null || bitmapsByTag.Count == 0) return result;

            foreach (var kvp in bitmapsByTag)
            {
                result[kvp.Key] = BuildEntries(kvp.Value);
            }

            return result;
        }
    }
}

