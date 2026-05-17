namespace OasisEditor;
using System.Text.RegularExpressions;

public interface IMameSegmentStateParser
{
    bool TryParse(string line, out int cellId, out int segmentMask);
}

public sealed class MameSegmentStateParser : IMameSegmentStateParser
{
    private static readonly Regex SegmentLineRegex = new(
        @"^(vfd|digiti|digit)\s*(\d+)\s*=\s*(-?\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool TryParse(string line, out int cellId, out int segmentMask)
    {
        cellId = 0;
        segmentMask = 0;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var match = SegmentLineRegex.Match(line.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups[2].Value, out cellId)
            || !int.TryParse(match.Groups[3].Value, out var rawMask))
        {
            return false;
        }

        var outputType = match.Groups[1].Value;
        if (outputType.Equals("digiti", StringComparison.OrdinalIgnoreCase))
        {
            segmentMask = (~rawMask) & 0xff;
            return true;
        }

        if (outputType.Equals("digit", StringComparison.OrdinalIgnoreCase))
        {
            segmentMask = rawMask & 0xff;
            return true;
        }

        segmentMask = rawMask;
        return true;
    }
}
