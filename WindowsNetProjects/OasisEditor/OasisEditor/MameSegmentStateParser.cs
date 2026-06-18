namespace OasisEditor;
using System.Text.RegularExpressions;

public interface IMameSegmentStateParser
{
    bool TryParse(string line, out int cellId, out int segmentMask, out MameSegmentOutputType outputType);
}

public enum MameSegmentOutputType
{
    Digit,
    Digiti,
    Vfd,
    NativeAlpha
}

public sealed class MameSegmentStateParser : IMameSegmentStateParser
{
    private static readonly Regex SegmentLineRegex = new(
        @"^(vfd|digiti|digit)\s*(\d+)\s*=\s*(-?\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool TryParse(string line, out int cellId, out int segmentMask, out MameSegmentOutputType outputType)
    {
        cellId = 0;
        segmentMask = 0;
        outputType = MameSegmentOutputType.Digit;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var match = SegmentLineRegex.Match(line.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups[2].Value, out cellId)
            || !int.TryParse(match.Groups[3].Value, out var rawMask))
        {
            return false;
        }

        var outputName = match.Groups[1].Value;
        if (outputName.Equals("digiti", StringComparison.OrdinalIgnoreCase))
        {
            outputType = MameSegmentOutputType.Digiti;
            segmentMask = (~rawMask) & 0xff;
            return true;
        }

        if (outputName.Equals("digit", StringComparison.OrdinalIgnoreCase))
        {
            outputType = MameSegmentOutputType.Digit;
            segmentMask = rawMask & 0xff;
            return true;
        }

        outputType = MameSegmentOutputType.Vfd;
        segmentMask = rawMask;
        return true;
    }
}
