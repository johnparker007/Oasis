using System;

namespace OasisPlayer.Startup
{
    public enum StartupMode
    {
        MachinePreview,
        Arcade
    }

    public sealed class StartupOptions
    {
        public StartupOptions(StartupMode mode, string buildDirectory, bool fullscreen, int width, int height, string interaction = "", string arcadeManifest = "", string fabricRuntime = "", string amberImpact = "")
        {
            Mode = mode;
            BuildDirectory = buildDirectory ?? string.Empty;
            Fullscreen = fullscreen;
            Width = width;
            Height = height;
            Interaction = interaction;
            ArcadeManifest = arcadeManifest;
            FabricRuntime = fabricRuntime; AmberImpact = amberImpact;
        }

        public StartupMode Mode { get; }
        public string BuildDirectory { get; }
        public bool Fullscreen { get; }
        public int Width { get; }
        public int Height { get; }
        public string Interaction { get; }
        public string ArcadeManifest { get; }
        public string FabricRuntime { get; }
        public string AmberImpact { get; }

        public string SceneName => Mode switch
        {
            StartupMode.MachinePreview => "MachinePreview",
            StartupMode.Arcade => "MachinePreview",
            _ => throw new NotSupportedException($"Unsupported startup mode: {Mode}")
        };
    }
}
