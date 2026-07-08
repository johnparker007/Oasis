namespace OasisEditor;

public sealed class EditorPreferences
{
    public ThemePreference ThemePreference { get; init; } = ThemePreference.Dark;

    public MamePreferences Mame { get; init; } = new();
    public NativeEmulationPreferences NativeEmulation { get; init; } = new();
    public OutputLogPreferences OutputLog { get; init; } = new();
    public FaceGenerationPreferences FaceGeneration { get; init; } = new();
    public string LastMfmeFmlImportDirectory { get; init; } = string.Empty;

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


public sealed class NativeEmulationPreferences
{
    public const int DefaultAudioBufferLengthMilliseconds = 50;

    public string System6LibraryPath { get; init; } = string.Empty;
    public int AudioBufferLengthMilliseconds { get; init; } = DefaultAudioBufferLengthMilliseconds;
}

public sealed class FaceGenerationPreferences
{
    public byte DefaultMaskExtractionThreshold { get; init; } = FaceGenerationSettingsModel.DefaultMaskExtractionThreshold;
    public double DefaultTrayBoundsInflationPercent { get; init; } = FaceGenerationSettingsModel.DefaultTrayBoundsInflationPercent;
    public double DefaultTrayBoundsPaddingPixels { get; init; } = FaceGenerationSettingsModel.DefaultTrayBoundsPaddingPixels;
    public bool DefaultClampTrayBoundsToLampWindow { get; init; } = FaceGenerationSettingsModel.DefaultClampTrayBoundsToLampWindow;
    public bool ShowFaceGenerationSettingsBeforeRegenerate { get; init; } = true;

    public FaceGenerationSettingsModel ToSettings()
    {
        return new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = DefaultMaskExtractionThreshold,
            TrayBoundsInflationPercent = DefaultTrayBoundsInflationPercent,
            TrayBoundsPaddingPixels = DefaultTrayBoundsPaddingPixels,
            ClampTrayBoundsToLampWindow = DefaultClampTrayBoundsToLampWindow
        }.Normalize();
    }

    public static FaceGenerationPreferences FromSettings(
        FaceGenerationSettingsModel settings,
        bool showBeforeRegenerate)
    {
        var normalized = (settings ?? FaceGenerationSettingsModel.Default).Normalize();
        return new FaceGenerationPreferences
        {
            DefaultMaskExtractionThreshold = normalized.MaskExtractionThreshold,
            DefaultTrayBoundsInflationPercent = normalized.TrayBoundsInflationPercent,
            DefaultTrayBoundsPaddingPixels = normalized.TrayBoundsPaddingPixels,
            DefaultClampTrayBoundsToLampWindow = normalized.ClampTrayBoundsToLampWindow,
            ShowFaceGenerationSettingsBeforeRegenerate = showBeforeRegenerate
        };
    }
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
    public string SearchText { get; init; } = string.Empty;
}
