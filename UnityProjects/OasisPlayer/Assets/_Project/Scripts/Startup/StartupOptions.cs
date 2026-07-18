using System;

namespace OasisPlayer.Startup;

public enum StartupMode { MachinePreview, Arcade }

public sealed class StartupOptions
{
    public StartupOptions(StartupMode mode, string buildDirectory, bool fullscreen, int width, int height)
    {
        Mode = mode; BuildDirectory = buildDirectory ?? string.Empty; Fullscreen = fullscreen; Width = width; Height = height;
    }
    public StartupMode Mode { get; }
    public string BuildDirectory { get; }
    public bool Fullscreen { get; }
    public int Width { get; }
    public int Height { get; }
    public string SceneName => Mode switch { StartupMode.MachinePreview => "MachinePreview", StartupMode.Arcade => throw new NotSupportedException("Arcade mode is not implemented in Phase 1."), _ => throw new NotSupportedException($"Unsupported startup mode: {Mode}") };
}
