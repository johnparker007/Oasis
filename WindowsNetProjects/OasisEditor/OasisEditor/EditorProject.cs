namespace OasisEditor;

public sealed class EditorProject
{
    public required string Name { get; init; }
    public required string ProjectFilePath { get; init; }
    public required string ProjectDirectory { get; init; }
    public required string AssetsDirectory { get; init; }
    public required string MachinesDirectory { get; init; }
    public required string GeneratedDirectory { get; init; }
    public FruitMachinePlatformType FruitMachinePlatform { get; set; } = FruitMachinePlatformType.None;
    public string MameRomName { get; set; } = string.Empty;
    public bool AutomaticallyDownloadMissingRoms { get; set; } = true;
}
