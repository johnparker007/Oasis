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
    public System6NativeRomSettings System6NativeRoms { get; set; } = new();
    public List<InputDefinitionModel> InputDefinitions { get; } = [];
}


public sealed class System6NativeRomSettings
{
    public string ProgramRom1Path { get; set; } = string.Empty;
    public string ProgramRom2Path { get; set; } = string.Empty;
    public string ProgramRom3Path { get; set; } = string.Empty;
    public string ProgramRom4Path { get; set; } = string.Empty;
    public string SoundRom1Path { get; set; } = string.Empty;
    public string SoundRom2Path { get; set; } = string.Empty;
    public string SoundRom3Path { get; set; } = string.Empty;
    public string SoundRom4Path { get; set; } = string.Empty;
    public bool FlashSwitch { get; set; }

    public IReadOnlyList<string> ProgramRomPaths => [ProgramRom1Path, ProgramRom2Path, ProgramRom3Path, ProgramRom4Path];
    public IReadOnlyList<string> SoundRomPaths => [SoundRom1Path, SoundRom2Path, SoundRom3Path, SoundRom4Path];
}
