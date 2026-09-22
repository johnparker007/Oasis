using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class DocumentSaveServiceTests
{
    [Fact]
    public void SaveDocument_MachinePreservesAuthoredStateInFileAndReplacementAcrossRepeatedSaves()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-machine-save-{Guid.NewGuid():N}");
        var savePath = Path.Combine(root, "Assets", "Machines", "Saved Machine", ProjectAssetPathService.MachineManifestFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        try
        {
            var machine = MachineDocument.Create("Custom display name") with
            {
                CabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d",
                SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/TopGlass/asset.face")],
                ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")],
                Runtime = new(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "Assets/ROMs/game.bin" }),
                InputDefinitions = [new InputDefinitionModel { Id = "start", Name = "Start", ButtonNumber = "1" }]
            };
            var current = new DocumentTabViewModel(
                EditorDocument.CreateMachineStub("Unsaved Machine").MarkDirty(),
                machineDocumentJson: MachineDocumentStorage.Serialize(machine));
            var service = new DocumentSaveService();

            var firstSave = service.SaveDocument(current, savePath);
            AssertMachineState(machine, firstSave.GetMachineDocument());
            Assert.False(firstSave.IsDirty);
            Assert.Equal(savePath, firstSave.FilePath);
            Assert.Equal("Saved Machine", firstSave.Document.Title);
            Assert.True(MachineDocumentStorage.TryRead(File.ReadAllText(savePath), out var persisted, out var error), error);
            AssertMachineState(machine, persisted);

            var secondSave = service.SaveDocument(firstSave, savePath);
            AssertMachineState(machine, secondSave.GetMachineDocument());
            Assert.False(secondSave.IsDirty);
            Assert.Equal(savePath, secondSave.FilePath);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ApplyInspectorSummary_PreservesMachineStateWhenReplacingTab()
    {
        var machine = MachineDocument.Create("Machine") with
        {
            CabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d",
            SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/TopGlass/asset.face")],
            ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")]
        };
        var original = new DocumentTabViewModel(
            EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Game/asset.machine", "Machine"),
            machineDocumentJson: MachineDocumentStorage.Serialize(machine));
        var documents = new System.Collections.ObjectModel.ObservableCollection<DocumentTabViewModel> { original };
        DocumentTabViewModel? selected = original;
        var project = new EditorProject
        {
            Name = "Project", ProjectFilePath = "C:/Project/Project.oasisproj", ProjectDirectory = "C:/Project",
            AssetsDirectory = "C:/Project/Assets", GeneratedDirectory = "C:/Project/Generated"
        };
        var workspace = new DocumentWorkspaceViewModel(() => project, _ => { }, documents, () => selected, value => selected = value, () => { }, _ => { }, (_, _) => { });

        var replacement = Assert.IsType<DocumentTabViewModel>(workspace.ApplyInspectorSummary("Updated summary"));

        Assert.Same(replacement, selected);
        Assert.Same(replacement, Assert.Single(documents));
        AssertMachineState(machine, replacement.GetMachineDocument());
    }

    [Fact]
    public void SaveDocument_ReturnsCleanDocumentWithSameIdAndPath()
    {
        var service = new DocumentSaveService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"oasis-save-{Guid.NewGuid():N}.panel2d");

        try
        {
            var current = new DocumentTabViewModel(
                EditorDocument.CreatePanel2DStub("Panel 1").MarkDirty(),
                Panel2DDocumentStorage.SerializeLayout([]));

            var saved = service.SaveDocument(current, tempPath);

            Assert.False(saved.IsDirty);
            Assert.Equal(current.DocumentId, saved.DocumentId);
            Assert.Equal(tempPath, saved.FilePath);
            Assert.True(File.Exists(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveDocument_ReportsProgressStages()
    {
        var service = new DocumentSaveService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"oasis-save-progress-{Guid.NewGuid():N}.panel2d");

        try
        {
            var current = new DocumentTabViewModel(
                EditorDocument.CreatePanel2DStub("Panel 1").MarkDirty(),
                Panel2DDocumentStorage.SerializeLayout([]));
            var progress = new RecordingEditorProgressReporter();

            service.SaveDocument(current, tempPath, progress: progress);

            Assert.Contains(progress.Reports, report => report.Message == "Preparing document save...");
            Assert.Contains(progress.Reports, report => report.Message == "Writing document file...");
            Assert.Contains(progress.Reports, report => report.Message == "Document saved.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void AssertMachineState(MachineDocument expected, MachineDocument actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.CabinetAssetPath, actual.CabinetAssetPath);
        Assert.Equal(expected.SurfaceAssignments, actual.SurfaceAssignments);
        Assert.Equal(expected.ReelAssignments, actual.ReelAssignments);
        Assert.Equal(expected.Runtime.Platform, actual.Runtime.Platform);
        Assert.Equal(MachineRuntimeDefinition.From(expected.Runtime).PlatformSettingsJson, MachineRuntimeDefinition.From(actual.Runtime).PlatformSettingsJson);
        Assert.Equal(expected.InputDefinitions.Select(input => (input.Id, input.Name, input.ButtonNumber)), actual.InputDefinitions.Select(input => (input.Id, input.Name, input.ButtonNumber)));
    }
}
