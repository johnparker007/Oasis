using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.MachineEditor.Models;
using OasisEditor.Features.MachineEditor.ViewModels;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineDocumentViewModelTests
{
    [Fact]
    public void Refresh_PreservesCabinetSelectionWhenPlatformChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        var project = TestProjectFactory.Create(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Vogue")).FullName;
        File.WriteAllText(
            Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName),
            CabinetDocumentStorage.Serialize(CabinetDocument.Empty));

        var machineDocument = MachineDocumentExtensions.Empty("Test Machine") with
        {
            Runtime = MachineRuntimeDefinition.CreateDefault() with { Platform = FruitMachinePlatformType.Impact }
        };
        var tab = new DocumentTabViewModel(
            EditorDocument.CreateMachineStub("Test Machine"),
            machineDocumentJson: MachineDocumentStorage.Serialize(machineDocument));
        tab.SetProjectAccessor(() => project);

        var editor = tab.MachineEditor!;
        editor.SelectedCabinetAssetPath = "Assets/Cabinet3D/Vogue";
        editor.SelectedPlatform = FruitMachinePlatformType.MaygayM1;

        Assert.Equal("Assets/Cabinet3D/Vogue", tab.GetMachineDocument().CabinetAssetPath);
        Assert.Equal("Assets/Cabinet3D/Vogue", editor.SelectedCabinetAssetPath);
        Assert.Single(editor.CabinetAssetChoices);
    }

    [Fact]
    public void Refresh_ResolvesRelativeCabinetModelPathFromPackageDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        var project = TestProjectFactory.Create(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Vogue")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        WriteMinimalGlb(sourceGlb);
        var cabinetManifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(
            cabinetManifestPath,
            CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("source.glb")));

        var machineDocument = MachineDocumentExtensions.Empty("Test Machine");
        var tab = new DocumentTabViewModel(
            EditorDocument.CreateMachineStub("Test Machine"),
            machineDocumentJson: MachineDocumentStorage.Serialize(machineDocument));
        tab.SetProjectAccessor(() => project);

        var editor = tab.MachineEditor!;
        editor.SelectedCabinetAssetPath = "Assets/Cabinet3D/Vogue";

        var resolvedModelPath = MachineRuntimeBuildService.ResolveCabinetModelPath(cabinetManifestPath, "source.glb");
        Assert.True(File.Exists(resolvedModelPath));
        Assert.Equal("Assets/Cabinet3D/Vogue", editor.SelectedCabinetAssetPath);
    }

    private static void WriteMinimalGlb(string path)
    {
        const string json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{}]} ";
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67u);
        writer.Write(2u);
        writer.Write((uint)(12 + 8 + jsonBytes.Length));
        writer.Write((uint)jsonBytes.Length);
        writer.Write(0x4E4F534Au);
        writer.Write(jsonBytes);
    }
}
