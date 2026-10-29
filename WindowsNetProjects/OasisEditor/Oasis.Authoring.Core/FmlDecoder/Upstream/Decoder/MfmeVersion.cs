using System;
using System.Text.RegularExpressions;

namespace MfmeFmlDecoder.Decoder
{
    /// <summary>
    /// Canonical MFME version read from TLV tag <c>0x2F</c>. The embedded strings are
    /// inconsistent across releases (<c>20.1</c>, <c>MFME v19.8</c>, <c>v5</c>,
    /// <c>20.0 Beta</c>, bare <c>MFME</c>), so all comparison happens on this value.
    /// </summary>
    internal readonly struct MfmeVersion : IEquatable<MfmeVersion>
    {
        public static readonly MfmeVersion V20_1 = new("20.1", 20, 1, hasMinor: true);
        public static readonly MfmeVersion V19_8 = new("19.8", 19, 8, hasMinor: true);
        public static readonly MfmeVersion V19_6 = new("19.6", 19, 6, hasMinor: true);
        public static readonly MfmeVersion V19_5 = new("19.5", 19, 5, hasMinor: true);
        public static readonly MfmeVersion V19_4 = new("19.4", 19, 4, hasMinor: true);
        public static readonly MfmeVersion V19_3 = new("19.3", 19, 3, hasMinor: true);
        public static readonly MfmeVersion V19_2 = new("19.2", 19, 2, hasMinor: true);
        public static readonly MfmeVersion V19_1 = new("19.1", 19, 1, hasMinor: true);
        public static readonly MfmeVersion V19_0 = new("19.0", 19, 0, hasMinor: true);
        public static readonly MfmeVersion V20_0 = new("20.0", 20, 0, hasMinor: true);
        public static readonly MfmeVersion V6_1 = new("6.1", 6, 1, hasMinor: true);
        public static readonly MfmeVersion V6_0 = new("6.0", 6, 0, hasMinor: true);
        public static readonly MfmeVersion V5_2 = new("5.2", 5, 2, hasMinor: true);
        public static readonly MfmeVersion V5 = new("5", 5, 0, hasMinor: false);
        public static readonly MfmeVersion V18_0 = new("18.0", 18, 0, hasMinor: true);
        public static readonly MfmeVersion MFME = new("MFME", 0, 0, hasMinor: false);

        public string Raw { get; }
        public int Major { get; }
        public int Minor { get; }
        public bool HasMinor { get; }

        public string Canonical => Major == 0 && !HasMinor
            ? "MFME"
            : HasMinor
                ? $"{Major}.{Minor}"
                : Major.ToString();

        private MfmeVersion(string raw, int major, int minor, bool hasMinor)
        {
            Raw = raw;
            Major = major;
            Minor = minor;
            HasMinor = hasMinor;
        }

        public static MfmeVersion Parse(string raw)
        {
            return TryParse(raw, out MfmeVersion version)
                ? version
                : new MfmeVersion(raw, 0, 0, hasMinor: false);
        }

        public static bool TryParse(string raw, out MfmeVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string text = Regex.Replace(raw.Replace("\0", string.Empty).Trim(), @"\s+", " ");
            if (string.Equals(text, "MFME", StringComparison.OrdinalIgnoreCase))
            {
                version = MFME;
                return true;
            }

            if (text.StartsWith("MFME", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(4).Trim();
            }

            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1).Trim();
            }

            Match match = Regex.Match(text, @"^(?<major>\d+)(?:\.(?<minor>\d+))?");
            if (!match.Success)
            {
                return false;
            }

            int major = int.Parse(match.Groups["major"].Value);
            bool hasMinor = match.Groups["minor"].Success;
            int minor = hasMinor ? int.Parse(match.Groups["minor"].Value) : 0;
            version = new MfmeVersion(raw, major, minor, hasMinor);
            return true;
        }

        public bool Equals(MfmeVersion other)
        {
            return Major == other.Major
                && Minor == other.Minor
                && HasMinor == other.HasMinor;
        }

        public override bool Equals(object obj) => obj is MfmeVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Major, Minor, HasMinor);

        public override string ToString() => HasMinor ? Canonical : Raw;

        public static bool operator ==(MfmeVersion left, MfmeVersion right) => left.Equals(right);

        public static bool operator !=(MfmeVersion left, MfmeVersion right) => !left.Equals(right);
    }
}
