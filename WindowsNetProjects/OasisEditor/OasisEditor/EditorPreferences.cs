namespace OasisEditor;

public sealed class EditorPreferences
{
    public ThemePreference ThemePreference { get; init; } = ThemePreference.Dark;

    public MamePreferences Mame { get; init; } = new();

    public Dictionary<string, ProjectWindowState> ProjectWindowStates { get; init; } = new();
}

public sealed class MamePreferences
{
    public string Version { get; init; } = "0267";
    public string ExecutablePath { get; init; } = string.Empty;
    public string ReleaseSource { get; init; } = "https://github.com/mamedev/mame/releases";
    public string CommandLineOverrides { get; init; } = string.Empty;
}

public sealed class ProjectWindowState
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsMaximized { get; init; }
}
