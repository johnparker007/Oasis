using System;
using System.IO;
using System.Linq;

namespace MfmeFmlDecoder.GameConfig
{
    internal static class SiblingGamResolver
    {
        /// <summary>
        /// Prefer same-stem .gam beside the layout; else the sole .gam in the directory.
        /// Returns null when none or ambiguous.
        /// </summary>
        public static string TryResolve(string layoutPath, out string skipReason)
        {
            skipReason = null;
            if (string.IsNullOrWhiteSpace(layoutPath))
            {
                skipReason = "Layout path is empty.";
                return null;
            }

            string dir = Path.GetDirectoryName(Path.GetFullPath(layoutPath));
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                skipReason = "Layout directory not found.";
                return null;
            }

            string sameStem = Path.ChangeExtension(layoutPath, ".gam");
            if (File.Exists(sameStem))
                return Path.GetFullPath(sameStem);

            string[] gams = Directory.GetFiles(dir, "*.gam");
            if (gams.Length == 1)
                return Path.GetFullPath(gams[0]);

            if (gams.Length == 0)
            {
                skipReason = "No sibling .gam found beside the layout.";
                return null;
            }

            skipReason =
                $"Multiple .gam files in '{dir}' and none matches layout stem; skipping GameConfig.";
            return null;
        }
    }
}
