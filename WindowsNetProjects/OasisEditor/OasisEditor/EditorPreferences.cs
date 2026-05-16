namespace OasisEditor;

public sealed class EditorPreferences
{
    public ThemePreference ThemePreference { get; init; } = ThemePreference.Dark;

    public MamePreferences Mame { get; init; } = new();
    public OutputLogPreferences OutputLog { get; init; } = new();

    public Dictionary<string, ProjectWindowState> ProjectWindowStates { get; init; } = new();
}

public sealed class MamePreferences
{
    public string Version { get; init; } = "0267";
    public string ExecutablePath { get; init; } = string.Empty;
    public string ReleaseSource { get; init; } = "https://github.com/mamedev/mame/releases";
    public string CommandLineOverrides { get; init; } = string.Empty;
    public bool KeepMameUpToDateAutomatically { get; init; } = true;
    public bool DebugOutputLamps { get; init; }
    public bool DebugOutputStdIn { get; init; }
    public bool DebugOutputStdOut { get; init; }
    public string RomDownloadBaseUrl { get; init; } = "https://archive.org/download/MAME215RomsOnlyMerged/";
    public string RomArchiveExtension { get; init; } = ".zip";
    public string LocalRomSourceDirectory { get; init; } = string.Empty;
    public string LocalRomArchiveExtension { get; init; } = ".zip";
}

public sealed class ProjectWindowState
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsMaximized { get; init; }
}

public sealed class OutputLogPreferences
{
    public bool ShowInfoLogs { get; init; } = true;
    public bool ShowWarningLogs { get; init; } = true;
    public bool ShowErrorLogs { get; init; } = true;
    public bool AutoScroll { get; init; } = true;
}
