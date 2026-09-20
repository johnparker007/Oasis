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
}
