using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MfmeFmlDecoder.GameConfig
{
    /// <summary>Parses an MFME .gam text file into keyed fields.</summary>
    internal sealed class GamFile
    {
        private static readonly Regex SystemLine = new Regex(
            @"^System\s+(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DipLine = new Regex(
            @"^DIP\s+(\d+)\s+(\S+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string Path { get; }
        public string System { get; }
        public IReadOnlyDictionary<string, string> Fields { get; }
        public IReadOnlyDictionary<string, string> DipBanks { get; }

        private GamFile(
            string path,
            string system,
            Dictionary<string, string> fields,
            Dictionary<string, string> dipBanks)
        {
            Path = path;
            System = system;
            Fields = fields;
            DipBanks = dipBanks;
        }

        public static GamFile Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("GAM path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("GAM file not found.", path);

            string text = File.ReadAllText(path, Encoding.UTF8);
            return Parse(path, text);
        }

        public static GamFile Parse(string path, string text)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string system = null;

            using var reader = new StringReader(text ?? string.Empty);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                Match sys = SystemLine.Match(line);
                if (sys.Success)
                {
                    system = sys.Groups[1].Value.Trim();
                    continue;
                }

                Match dip = DipLine.Match(line);
                if (dip.Success)
                {
                    string bankKey = "DIP " + dip.Groups[1].Value;
                    string pattern = dip.Groups[2].Value.Trim();
                    dips[bankKey] = pattern;
                    fields[bankKey] = pattern;
                    continue;
                }

                int space = line.IndexOf(' ');
                if (space <= 0)
                {
                    fields[line] = string.Empty;
                    continue;
                }

                string key = line.Substring(0, space).Trim();
                string value = line.Substring(space + 1).Trim();
                fields[key] = value;
            }

            return new GamFile(path, system, fields, dips);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            if (!Fields.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
                return false;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetUInt(string key, out uint value)
        {
            value = 0;
            if (!Fields.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
                return false;
            return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
