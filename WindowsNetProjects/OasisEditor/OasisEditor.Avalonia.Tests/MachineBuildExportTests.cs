using System.Text;
using System.Text.Json;
using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Progress;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class MachineBuildExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oasis-avalonia-build-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ProductionBuilderAndPackageExporterCreatePortableGeneratedOutputAndRefuseOverwrite()
    {
        var project = CreateProject();
        var machinePath = CreateMachine(project);
        var builder = new MachineRuntimeBuildService();
        var built = builder.BuildFromMachineDocument(project, machinePath, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.True(built.Success, built.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(built.BuildRoot!, MachineRuntimeBuildService.MachineManifestFileName)));
        using (var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(built.BuildRoot!, MachineRuntimeBuildService.MachineManifestFileName))))
            Assert.Equal(MachineRuntimeBuildService.MachineSchemaVersion, json.RootElement.GetProperty("schemaVersion").GetInt32());

        var exports = Path.Combine(project.GeneratedDirectory, "Exports");
        var exporter = new MachineRuntimePackageExportService(builder);
        var package = exporter.Export(project, machinePath, exports, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.True(package.Success, package.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(package.PackageDirectory!, MachineRuntimeBuildService.MachineManifestFileName)));
        var marker = Path.Combine(package.PackageDirectory!, "keep.txt");
        File.WriteAllText(marker, "preserve");
        var repeated = exporter.Export(project, machinePath, exports, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.False(repeated.Success);
        Assert.Equal("preserve", File.ReadAllText(marker));
    }

    [Fact]
    public void ProductionBuilderRejectsMachineSourceOutsideProject()
    {
        var project = CreateProject();
        var outside = Path.Combine(Path.GetTempPath(), "oasis-external-machine-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(outside, "{}");
            var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, outside, NoOpEditorProgressReporter.Instance, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Contains("safe", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (File.Exists(outside)) File.Delete(outside); }
    }

    [Fact]
    public void PackageExporterRejectsDestinationInsideGeneratedBuildAndDoesNotCreateIt()
    {
        var project = CreateProject();
        var machinePath = CreateMachine(project);
        var builder = new MachineRuntimeBuildService();
        var built = builder.BuildFromMachineDocument(project, machinePath, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.True(built.Success, built.ErrorMessage);
        var destination = Path.Combine(built.BuildRoot!, "Exports");

        var result = new MachineRuntimePackageExportService(builder).Export(project, machinePath, destination,
            NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("separate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(destination));
    }

    [Theory]
    [InlineData(FruitMachinePlatformType.Impact)]
    [InlineData(FruitMachinePlatformType.MPU5)]
    [InlineData(FruitMachinePlatformType.Epoch)]
    [InlineData(FruitMachinePlatformType.MPU3)]
    [InlineData(FruitMachinePlatformType.MaygayM1)]
    [InlineData(FruitMachinePlatformType.Scorpion4)]
    public void SharedFabricResolverMakesProjectRomPathsAbsoluteWithoutMutatingAuthoredSettings(FruitMachinePlatformType platform)
    {
        var project = CreateProject();
        object settings = platform switch
        {
            FruitMachinePlatformType.Impact => new System6NativeRomSettings { ProgramRom1Path = "Assets/EmulationRoms/rom.bin" },
            FruitMachinePlatformType.MPU5 => new Mpu5NativeRomSettings { ProgramRom1Path = "Assets/EmulationRoms/rom.bin" },
            FruitMachinePlatformType.Epoch => new EpochNativeRomSettings { ProgramRom1Path = "Assets/EmulationRoms/rom.bin" },
            FruitMachinePlatformType.MPU3 => new Mpu3ProjectSettings { ProgramRoms = [new() { Slot = 0, Path = "Assets/EmulationRoms/rom.bin" }] },
            FruitMachinePlatformType.MaygayM1 => new M1ProjectSettings { ProgramRoms = [new() { Slot = 0, Path = "Assets/EmulationRoms/rom.bin" }] },
            FruitMachinePlatformType.Scorpion4 => new Scorpion4ProjectSettings { ProgramRoms = [new() { Slot = 0, Path = "Assets/EmulationRoms/rom.bin" }] },
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        var resolved = FabricRomResourceResolver.ResolveSettingsPaths(project, platform, settings);
        var path = platform switch
        {
            FruitMachinePlatformType.Impact => ((System6NativeRomSettings)resolved).ProgramRom1Path,
            FruitMachinePlatformType.MPU5 => ((Mpu5NativeRomSettings)resolved).ProgramRom1Path,
            FruitMachinePlatformType.Epoch => ((EpochNativeRomSettings)resolved).ProgramRom1Path,
            FruitMachinePlatformType.MPU3 => ((Mpu3ProjectSettings)resolved).ProgramRoms[0].Path,
            FruitMachinePlatformType.MaygayM1 => ((M1ProjectSettings)resolved).ProgramRoms[0].Path,
            FruitMachinePlatformType.Scorpion4 => ((Scorpion4ProjectSettings)resolved).ProgramRoms[0].Path,
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        Assert.Equal(Path.GetFullPath(Path.Combine(project.ProjectDirectory, "Assets/EmulationRoms/rom.bin")), path);
        var originalPath = platform switch
        {
            FruitMachinePlatformType.Impact => ((System6NativeRomSettings)settings).ProgramRom1Path,
            FruitMachinePlatformType.MPU5 => ((Mpu5NativeRomSettings)settings).ProgramRom1Path,
            FruitMachinePlatformType.Epoch => ((EpochNativeRomSettings)settings).ProgramRom1Path,
            FruitMachinePlatformType.MPU3 => ((Mpu3ProjectSettings)settings).ProgramRoms[0].Path,
            FruitMachinePlatformType.MaygayM1 => ((M1ProjectSettings)settings).ProgramRoms[0].Path,
            FruitMachinePlatformType.Scorpion4 => ((Scorpion4ProjectSettings)settings).ProgramRoms[0].Path,
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        Assert.Equal("Assets/EmulationRoms/rom.bin", originalPath);
    }

    private EditorProject CreateProject()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Machines"));
        Directory.CreateDirectory(Path.Combine(_root, "Generated"));
        return new EditorProject { Name = "Build fixture", ProjectFilePath = Path.Combine(_root, "Build fixture.oasisproj"), ProjectDirectory = _root, AssetsDirectory = Path.Combine(_root, "Assets"), GeneratedDirectory = Path.Combine(_root, "Generated") };
    }

    private static string CreateMachine(EditorProject project)
    {
        var assetPaths = new ProjectAssetPathService();
        var cabinetManifest = assetPaths.GetCabinet3DManifestPath(project, "Cabinet");
        Directory.CreateDirectory(Path.GetDirectoryName(cabinetManifest)!);
        WriteGlb(Path.Combine(Path.GetDirectoryName(cabinetManifest)!, "cabinet.glb"));
        File.WriteAllText(cabinetManifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
        var machine = MachineDocument.Create("Build fixture") with { CabinetAsset = AssetReference.Project(assetPaths.ToProjectRelativePath(project, cabinetManifest)) };
        var machinePath = assetPaths.GetMachineManifestPath(project, machine.DisplayName);
        Directory.CreateDirectory(Path.GetDirectoryName(machinePath)!);
        File.WriteAllText(machinePath, MachineDocumentStorage.Serialize(machine));
        return machinePath;
    }

    private static void WriteGlb(string path)
    {
        var binary = new byte[92];
        var positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 };
        var uvs = new float[] { 0, 0, 1, 0, 1, 1, 0, 1 };
        var indexes = new ushort[] { 0, 1, 2, 0, 2, 3 };
        Buffer.BlockCopy(positions, 0, binary, 0, positions.Length * sizeof(float));
        Buffer.BlockCopy(uvs, 0, binary, 48, uvs.Length * sizeof(float));
        Buffer.BlockCopy(indexes, 0, binary, 80, indexes.Length * sizeof(ushort));
        var json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"name\":\"OasisFace_glass\",\"mesh\":0}],\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"TEXCOORD_0\":1},\"indices\":2}]}],\"buffers\":[{\"byteLength\":92}],\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":48},{\"buffer\":0,\"byteOffset\":48,\"byteLength\":32},{\"buffer\":0,\"byteOffset\":80,\"byteLength\":12}],\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5126,\"count\":4,\"type\":\"VEC2\"},{\"bufferView\":2,\"componentType\":5123,\"count\":6,\"type\":\"SCALAR\"}]}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedJsonLength = (jsonBytes.Length + 3) & ~3;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(0x46546C67); writer.Write(2); writer.Write(12 + 8 + paddedJsonLength + 8 + binary.Length);
        writer.Write(paddedJsonLength); writer.Write(0x4E4F534A); writer.Write(jsonBytes); writer.Write(Enumerable.Repeat((byte)0x20, paddedJsonLength - jsonBytes.Length).ToArray());
        writer.Write(binary.Length); writer.Write(0x004E4942); writer.Write(binary);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
