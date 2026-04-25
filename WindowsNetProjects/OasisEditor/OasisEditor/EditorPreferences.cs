namespace OasisEditor;

public sealed class EditorPreferences
{
    public ThemePreference ThemePreference { get; init; } = ThemePreference.Dark;

    public Dictionary<string, ProjectWindowState> ProjectWindowStates { get; init; } = new();
}

public sealed class ProjectWindowState
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsMaximized { get; init; }
}
