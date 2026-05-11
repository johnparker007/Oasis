namespace OasisEditor;

public interface IMameLampStateParser
{
    bool TryParse(string line, out int lampId, out int lampValue);
}

public sealed class MameLampStateParser : IMameLampStateParser
{
    public bool TryParse(string line, out int lampId, out int lampValue)
    {
        lampId = 0;
        lampValue = 0;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (!trimmed.StartsWith("lamp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var firstSpaceIndex = trimmed.IndexOf(' ');
        if (firstSpaceIndex <= "lamp".Length)
        {
            return false;
        }

        var lastSpaceIndex = trimmed.LastIndexOf(' ');
        if (lastSpaceIndex <= firstSpaceIndex || lastSpaceIndex >= trimmed.Length - 1)
        {
            return false;
        }

        var lampToken = trimmed.Substring("lamp".Length, firstSpaceIndex - "lamp".Length);
        var valueToken = trimmed.Substring(lastSpaceIndex + 1);

        if (!int.TryParse(lampToken, out lampId))
        {
            return false;
        }

        if (!int.TryParse(valueToken, out lampValue))
        {
            return false;
        }

        return true;
    }
}
