namespace OasisEditor.Features.MachineEditor.Models;

public sealed record MachineDocument(
    int Version,
    string Id,
    string Title,
    string? Summary,
    string? CabinetAssetPath,
    MachineSurfaceAssignment[] SurfaceAssignments,
    MachineReelAssignment[] ReelAssignments,
    MachineRuntimeDefinition Runtime,
    InputDefinitionModel[] InputDefinitions);

public sealed record MachineSurfaceAssignment(string TargetId, string FaceAssetPath)
{
    public MachineSurfaceAssignment Normalized() => new(TargetId.Trim(), FaceAssetPath.Trim().Replace('\\', '/'));
}

public sealed record MachineReelAssignment(MachineObjectReference MachineReelReference, string ReelSpecificationId)
{
    public MachineReelAssignment Normalized() => new(MachineReelReference, ReelSpecificationId.Trim());
}

public enum MachineRuntimeKind { Emulation }

public sealed record MachineRuntimeDefinition(
    MachineRuntimeKind Kind,
    FruitMachinePlatformType Platform,
    System6NativeRomSettings System6NativeRoms,
    Mpu5NativeRomSettings Mpu5NativeRoms,
    EpochNativeRomSettings EpochNativeRoms,
    Mpu3ProjectSettings Mpu3Settings,
    M1ProjectSettings M1Settings,
    Scorpion4ProjectSettings Scorpion4Settings)
{
    public static MachineRuntimeDefinition CreateDefault() => new(
        MachineRuntimeKind.Emulation,
        FruitMachinePlatformType.None,
        new System6NativeRomSettings(),
        new Mpu5NativeRomSettings(),
        new EpochNativeRomSettings(),
        new Mpu3ProjectSettings(),
        new M1ProjectSettings(),
        new Scorpion4ProjectSettings());
}

public static class MachineDocumentExtensions
{
    public static MachineDocument Empty(string? title = null) => new(
        1,
        Guid.NewGuid().ToString("N"),
        string.IsNullOrWhiteSpace(title) ? "Machine" : title.Trim(),
        null,
        null,
        [],
        [],
        MachineRuntimeDefinition.CreateDefault(),
        []);
}
