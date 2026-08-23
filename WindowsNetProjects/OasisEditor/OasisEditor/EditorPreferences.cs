namespace OasisEditor;

public sealed class EditorPreferences
{
    public ThemePreference ThemePreference { get; init; } = ThemePreference.Dark;

    public NativeEmulationPreferences NativeEmulation { get; init; } = new();
    public OutputLogPreferences OutputLog { get; init; } = new();
    public FaceGenerationPreferences FaceGeneration { get; init; } = new();
    public OasisPlayerPreferences Player { get; init; } = new();
    public string LastMfmeFmlImportDirectory { get; init; } = string.Empty;

    public Dictionary<string, ProjectWindowState> ProjectWindowStates { get; init; } = new();
}

public sealed class OasisPlayerPreferences
{
    public string ExecutablePath { get; init; } = string.Empty;
    public bool Fullscreen { get; init; }
    public int PreviewWidth { get; init; } = OasisPlayerLaunchService.DefaultPreviewWidth;
    public int PreviewHeight { get; init; } = OasisPlayerLaunchService.DefaultPreviewHeight;
}

public sealed class NativeEmulationPreferences
{
    public const int DefaultAudioBufferLengthMilliseconds = 50;

    public string FabricRuntimeLibraryPath { get; init; } = string.Empty;
    public string ProductionAmberLibraryPath { get; init; } = string.Empty;
    public string Mpu5AmberLibraryPath { get; init; } = string.Empty;
    public string EpochAmberLibraryPath { get; init; } = string.Empty;
    public string Mpu3AmberLibraryPath { get; init; } = string.Empty;
    public string M1AmberLibraryPath { get; init; } = string.Empty;
    public string Scorpion4AmberLibraryPath { get; init; } = string.Empty;
    public int AudioBufferLengthMilliseconds { get; init; } = DefaultAudioBufferLengthMilliseconds;
}

public sealed class FaceGenerationPreferences
{
    public bool DefaultPostWarpSharpeningEnabled { get; init; } = FaceGenerationSettingsModel.DefaultPostWarpSharpeningEnabled;
    public double DefaultPostWarpSharpeningAmount { get; init; } = FaceGenerationSettingsModel.DefaultPostWarpSharpeningAmount;
    public double DefaultPostWarpSharpeningRadiusPixels { get; init; } = FaceGenerationSettingsModel.DefaultPostWarpSharpeningRadiusPixels;
    public int DefaultPostWarpSharpeningThreshold { get; init; } = FaceGenerationSettingsModel.DefaultPostWarpSharpeningThreshold;
    public byte DefaultMaskExtractionThreshold { get; init; } = FaceGenerationSettingsModel.DefaultMaskExtractionThreshold;
    public double DefaultTrayBoundsInflationPercent { get; init; } = FaceGenerationSettingsModel.DefaultTrayBoundsInflationPercent;
    public double DefaultTrayBoundsPaddingPixels { get; init; } = FaceGenerationSettingsModel.DefaultTrayBoundsPaddingPixels;
    public bool DefaultClampTrayBoundsToLampWindow { get; init; } = FaceGenerationSettingsModel.DefaultClampTrayBoundsToLampWindow;

    public FaceGenerationSettingsModel ToSettings()
    {
        return new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = DefaultMaskExtractionThreshold,
            TrayBoundsInflationPercent = DefaultTrayBoundsInflationPercent,
            TrayBoundsPaddingPixels = DefaultTrayBoundsPaddingPixels,
            ClampTrayBoundsToLampWindow = DefaultClampTrayBoundsToLampWindow,
            PostWarpSharpeningEnabled = DefaultPostWarpSharpeningEnabled,
            PostWarpSharpeningAmount = DefaultPostWarpSharpeningAmount,
            PostWarpSharpeningRadiusPixels = DefaultPostWarpSharpeningRadiusPixels,
            PostWarpSharpeningThreshold = DefaultPostWarpSharpeningThreshold
        }.Normalize();
    }

    public static FaceGenerationPreferences FromSettings(FaceGenerationSettingsModel settings)
    {
        var normalized = (settings ?? FaceGenerationSettingsModel.Default).Normalize();
        return new FaceGenerationPreferences
        {
            DefaultMaskExtractionThreshold = normalized.MaskExtractionThreshold,
            DefaultTrayBoundsInflationPercent = normalized.TrayBoundsInflationPercent,
            DefaultTrayBoundsPaddingPixels = normalized.TrayBoundsPaddingPixels,
            DefaultClampTrayBoundsToLampWindow = normalized.ClampTrayBoundsToLampWindow,
            DefaultPostWarpSharpeningEnabled = normalized.PostWarpSharpeningEnabled,
            DefaultPostWarpSharpeningAmount = normalized.PostWarpSharpeningAmount,
            DefaultPostWarpSharpeningRadiusPixels = normalized.PostWarpSharpeningRadiusPixels,
            DefaultPostWarpSharpeningThreshold = normalized.PostWarpSharpeningThreshold
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
