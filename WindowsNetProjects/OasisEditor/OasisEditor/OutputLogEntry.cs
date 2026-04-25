using System.Windows.Media;

namespace OasisEditor;

public enum OutputLogStatus
{
    Info,
    Warning,
    Error
}

public sealed record OutputLogEntry(DateTime Timestamp, string Message, OutputLogStatus Status)
{
    private static readonly Brush InfoBrush = Brushes.White;
    private static readonly Brush WarningBrush = Brushes.Gold;
    private static readonly Brush ErrorBrush = Brushes.IndianRed;

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");

    public string IconGlyph => Status switch
    {
        OutputLogStatus.Warning => "\uE7BA",
        OutputLogStatus.Error => "\uEA39",
        _ => "\uE946"
    };

    public Brush StatusBrush => Status switch
    {
        OutputLogStatus.Warning => WarningBrush,
        OutputLogStatus.Error => ErrorBrush,
        _ => InfoBrush
    };
}
