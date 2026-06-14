namespace OasisEditor.Progress;

public sealed record EditorProgressRequest(
    string Title,
    string InitialMessage = "",
    EditorProgressMode InitialMode = EditorProgressMode.Indeterminate,
    bool CanCancel = false,
    TimeSpan? ShowDelay = null,
    TimeSpan? MinimumDisplayDuration = null)
{
    public static readonly TimeSpan DefaultShowDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan DefaultMinimumDisplayDuration = TimeSpan.FromMilliseconds(300);

    public EditorProgressRequest Normalize()
    {
        var title = string.IsNullOrWhiteSpace(Title) ? "Working" : Title.Trim();
        var initialMessage = InitialMessage?.Trim() ?? string.Empty;
        var showDelay = ShowDelay < TimeSpan.Zero ? TimeSpan.Zero : ShowDelay;
        var minimumDisplayDuration = MinimumDisplayDuration < TimeSpan.Zero ? TimeSpan.Zero : MinimumDisplayDuration;

        return this with
        {
            Title = title,
            InitialMessage = initialMessage,
            ShowDelay = showDelay ?? DefaultShowDelay,
            MinimumDisplayDuration = minimumDisplayDuration ?? DefaultMinimumDisplayDuration
        };
    }
}
