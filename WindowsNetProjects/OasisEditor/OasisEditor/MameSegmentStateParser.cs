namespace OasisEditor;
using System.Text.RegularExpressions;

public interface IMameSegmentStateParser
{
    bool TryParse(string line, out int cellId, out int segmentMask);
}

public sealed class MameSegmentStateParser : IMameSegmentStateParser
{
    private static readonly Regex SegmentLineRegex = new(
        @"(?:vfd|digit)\s*(\d+)\s*=\s*(-?\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool TryParse(string line, out int cellId, out int segmentMask)
    {
        cellId = 0;
        segmentMask = 0;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var match = SegmentLineRegex.Match(line.Trim());
        return match.Success
            && int.TryParse(match.Groups[1].Value, out cellId)
            && int.TryParse(match.Groups[2].Value, out segmentMask);
    }
}
