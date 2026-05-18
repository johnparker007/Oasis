namespace OasisEditor.Rendering;

internal enum RuntimeTextHorizontalAlignment
{
    Left = 0,
    Center,
    Right
}

internal readonly record struct RuntimeTextLayoutLine(string Text, double X, double Y);

internal readonly record struct RuntimeTextLayoutResult(IReadOnlyList<RuntimeTextLayoutLine> Lines, double LineHeight);

internal static class RuntimeTextLayout
{
    public static RuntimeTextLayoutResult Layout(
        string? text,
        double maxWidth,
        double charWidth,
        double lineHeight,
        RuntimeTextHorizontalAlignment horizontalAlignment)
    {
        if (maxWidth <= 0d || charWidth <= 0d || lineHeight <= 0d)
        {
            return new RuntimeTextLayoutResult([], lineHeight <= 0d ? 0d : lineHeight);
        }

        var normalized = (text ?? string.Empty).Replace("\r\n", "\n");
        var paragraphs = normalized.Split('\n');
        var lines = new List<RuntimeTextLayoutLine>();
        var maxCharsPerLine = Math.Max(1, (int)Math.Floor(maxWidth / charWidth));

        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            var paragraph = paragraphs[paragraphIndex];
            var wrapped = WrapParagraph(paragraph, maxCharsPerLine);
            foreach (var wrappedLine in wrapped)
            {
                var trimmed = wrappedLine;
                var width = trimmed.Length * charWidth;
                var x = horizontalAlignment switch
                {
                    RuntimeTextHorizontalAlignment.Center => (maxWidth - width) / 2d,
                    RuntimeTextHorizontalAlignment.Right => maxWidth - width,
                    _ => 0d
                };

                x = Math.Max(0d, x);
                lines.Add(new RuntimeTextLayoutLine(trimmed, x, lines.Count * lineHeight));
            }

            if (wrapped.Count == 0)
            {
                lines.Add(new RuntimeTextLayoutLine(string.Empty, 0d, lines.Count * lineHeight));
            }
        }

        return new RuntimeTextLayoutResult(lines, lineHeight);
    }

    private static List<string> WrapParagraph(string text, int maxCharsPerLine)
    {
        var result = new List<string>();
        if (text.Length == 0)
        {
            return result;
        }

        var words = text.Split(' ', StringSplitOptions.None);
        var current = string.Empty;

        foreach (var rawWord in words)
        {
            var word = rawWord;
            if (word.Length > maxCharsPerLine)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    result.Add(current);
                    current = string.Empty;
                }

                var remaining = word;
                while (remaining.Length > maxCharsPerLine)
                {
                    result.Add(remaining[..maxCharsPerLine]);
                    remaining = remaining[maxCharsPerLine..];
                }

                current = remaining;
                continue;
            }

            var candidate = string.IsNullOrEmpty(current)
                ? word
                : $"{current} {word}";

            if (candidate.Length <= maxCharsPerLine)
            {
                current = candidate;
            }
            else
            {
                result.Add(current);
                current = word;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            result.Add(current);
        }

        return result;
    }
}
