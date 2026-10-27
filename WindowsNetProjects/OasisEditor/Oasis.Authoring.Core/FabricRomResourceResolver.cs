namespace OasisEditor;

/// <summary>Resolves authored platform ROM settings into Fabric's stable, zero-based resources.</summary>
public static class FabricRomResourceResolver
{
    /// <summary>Returns a deep copy of platform settings with project-relative ROM paths made absolute.</summary>
    public static object ResolveSettingsPaths(EditorProject project, FruitMachinePlatformType platform, object settings)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(settings);
        return platform switch
        {
            FruitMachinePlatformType.Impact => ResolvePaths(project, Copy<System6NativeRomSettings>(settings), x => { x.ProgramRom1Path = ResolvePath(project, x.ProgramRom1Path); x.ProgramRom2Path = ResolvePath(project, x.ProgramRom2Path); x.ProgramRom3Path = ResolvePath(project, x.ProgramRom3Path); x.ProgramRom4Path = ResolvePath(project, x.ProgramRom4Path); x.SoundRom1Path = ResolvePath(project, x.SoundRom1Path); x.SoundRom2Path = ResolvePath(project, x.SoundRom2Path); x.SoundRom3Path = ResolvePath(project, x.SoundRom3Path); x.SoundRom4Path = ResolvePath(project, x.SoundRom4Path); }),
            FruitMachinePlatformType.MPU5 => ResolvePaths(project, Copy<Mpu5NativeRomSettings>(settings), x => { x.ProgramRom1Path = ResolvePath(project, x.ProgramRom1Path); x.ProgramRom2Path = ResolvePath(project, x.ProgramRom2Path); x.ProgramRom3Path = ResolvePath(project, x.ProgramRom3Path); x.ProgramRom4Path = ResolvePath(project, x.ProgramRom4Path); x.SoundRom1Path = ResolvePath(project, x.SoundRom1Path); x.SoundRom2Path = ResolvePath(project, x.SoundRom2Path); x.SoundRom3Path = ResolvePath(project, x.SoundRom3Path); x.SoundRom4Path = ResolvePath(project, x.SoundRom4Path); }),
            FruitMachinePlatformType.Epoch => ResolvePaths(project, Copy<EpochNativeRomSettings>(settings), x => { x.ProgramRom1Path = ResolvePath(project, x.ProgramRom1Path); x.ProgramRom2Path = ResolvePath(project, x.ProgramRom2Path); x.ProgramRom3Path = ResolvePath(project, x.ProgramRom3Path); x.ProgramRom4Path = ResolvePath(project, x.ProgramRom4Path); x.SoundRom1Path = ResolvePath(project, x.SoundRom1Path); x.SoundRom2Path = ResolvePath(project, x.SoundRom2Path); x.SoundRom3Path = ResolvePath(project, x.SoundRom3Path); x.SoundRom4Path = ResolvePath(project, x.SoundRom4Path); }),
            FruitMachinePlatformType.MPU3 => ResolvePaths(project, Copy<Mpu3ProjectSettings>(settings), x => { foreach (var item in x.ProgramRoms) item.Path = ResolvePath(project, item.Path); }),
            FruitMachinePlatformType.MaygayM1 => ResolvePaths(project, Copy<M1ProjectSettings>(settings), x => { foreach (var item in x.ProgramRoms.Concat(x.SoundRoms)) item.Path = ResolvePath(project, item.Path); }),
            FruitMachinePlatformType.Scorpion4 => ResolvePaths(project, Copy<Scorpion4ProjectSettings>(settings), x => { foreach (var item in x.ProgramRoms.Concat(x.SoundRoms)) item.Path = ResolvePath(project, item.Path); }),
            _ => throw new NotSupportedException($"Emulation platform '{platform}' does not have ROM path settings.")
        };
    }

    private static T Copy<T>(object settings) where T : class
    {
        if (settings.GetType() != typeof(T)) throw new InvalidOperationException($"Expected {typeof(T).Name} settings, received {settings.GetType().Name}.");
        return System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(settings))
            ?? throw new InvalidOperationException($"Could not copy {typeof(T).Name} settings.");
    }

    private static T ResolvePaths<T>(EditorProject project, T settings, Action<T> resolve) where T : class { resolve(settings); return settings; }

    private static string ResolvePath(EditorProject project, string path) => string.IsNullOrWhiteSpace(path) || Path.IsPathFullyQualified(path)
        ? path
        : Path.GetFullPath(Path.Combine(project.ProjectDirectory, path.Replace('/', Path.DirectorySeparatorChar)));

    public static IReadOnlyList<FabricRomResource> Build(System6NativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var resources = new List<FabricRomResource>(8);
        Add(resources, settings.ProgramRomPaths, FabricRomRole.Program);
        Add(resources, settings.SoundRomPaths, FabricRomRole.Sound);
        return resources;
    }

    public static IReadOnlyList<FabricRomResource> Build(Mpu5NativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.ProgramRom1Path)) throw new InvalidOperationException("Current project settings are missing required MPU5 Program ROM 1.");
        var resources = new List<FabricRomResource>(8);
        Add(resources, settings.ProgramRomPaths, FabricRomRole.Program);
        Add(resources, settings.SoundRomPaths, FabricRomRole.Sound);
        return resources;
    }

    public static IReadOnlyList<FabricRomResource> Build(EpochNativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.ProgramRom1Path)) throw new InvalidOperationException("Current project settings are missing required Epoch Program ROM 1.");
        var resources = new List<FabricRomResource>(8);
        Add(resources, settings.ProgramRomPaths, FabricRomRole.Program);
        Add(resources, settings.SoundRomPaths, FabricRomRole.Sound);
        return resources;
    }

    public static IReadOnlyList<FabricRomResource> Build(Mpu3ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ProgramRoms.Count != 4 || settings.ProgramRoms.Select(x => x.Slot).Distinct().Count() != 4)
            throw new InvalidOperationException("MPU3 requires exactly four uniquely slotted program ROM descriptors.");
        var resources = new List<FabricRomResource>(4);
        foreach (var rom in settings.ProgramRoms.OrderBy(x => x.Slot))
        {
            if (rom.Slot is < 0 or > 3) throw new InvalidOperationException("MPU3 program ROM slot must be 0..3.");
            if (!string.IsNullOrWhiteSpace(rom.Path)) resources.Add(new(FabricRomRole.Program, (uint)rom.Slot, rom.Path));
        }
        if (resources.Count == 0) throw new InvalidOperationException("Current project settings are missing required MPU3 program ROMs.");
        return resources;
    }

    public static IReadOnlyList<FabricRomResource> Build(M1ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        FabricAmberM1Configuration.Validate(settings);
        var resources = new List<FabricRomResource>(8);
        Add(resources, settings.ProgramRoms.OrderBy(x => x.Slot).Select(x => x.Path).ToArray(), FabricRomRole.Program);
        Add(resources, settings.SoundRoms.OrderBy(x => x.Slot).Select(x => x.Path).ToArray(), FabricRomRole.Sound);
        if (!resources.Any(x => x.Role == FabricRomRole.Program)) throw new InvalidOperationException("Current project settings are missing required M1 program ROM slot 0.");
        return resources;
    }

    public static IReadOnlyList<FabricRomResource> Build(Scorpion4ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        FabricAmberScorpion4Configuration.Validate(settings);
        var resources = new List<FabricRomResource>(8);
        Add(resources, settings.ProgramRoms.OrderBy(x => x.Slot).Select(x => x.Path).ToArray(), FabricRomRole.Program);
        Add(resources, settings.SoundRoms.OrderBy(x => x.Slot).Select(x => x.Path).ToArray(), FabricRomRole.Sound);
        if (!resources.Any(x => x.Role == FabricRomRole.Program)) throw new InvalidOperationException("Current project settings are missing required Scorpion 4 program ROM slot 0.");
        return resources;
    }

    private static void Add(List<FabricRomResource> resources, IReadOnlyList<string> paths, FabricRomRole role)
    {
        if (paths.Count > 4) throw new InvalidOperationException($"Fabric supports at most four {role} ROM slots.");
        var sawBlank = false;
        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            if (string.IsNullOrWhiteSpace(path)) { sawBlank = true; continue; }
            if (sawBlank) throw new InvalidOperationException($"Fabric {role} ROM slot {index} is configured after a blank slot; slots must be contiguous from zero.");
            resources.Add(new(role, checked((uint)index), path));
        }
    }
}
