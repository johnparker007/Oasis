using OasisEditor.Automation;
using OasisEditor.Features.MachineEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class DocumentSaveServiceTests
{
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
    public void SaveDocument_PreservesMachineDocumentState()
    {
        var service = new DocumentSaveService();
        var root = Path.Combine(Path.GetTempPath(), $"oasis-save-machine-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var savePath = Path.Combine(root, "Assets", "Machines", "Test Machine", ProjectAssetPathService.MachineManifestFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

        try
        {
            var machineDocument = MachineDocumentExtensions.Empty("Test Machine") with
            {
                CabinetAssetPath = "Assets/Cabinet3D/Demo",
                Runtime = MachineRuntimeDefinition.CreateDefault() with { Platform = FruitMachinePlatformType.MPU5 }
            };
            var current = new DocumentTabViewModel(
                EditorDocument.CreateMachineStub("Test Machine").MarkDirty(),
                machineDocumentJson: MachineDocumentStorage.Serialize(machineDocument));
            current.SetMachineDocument(machineDocument);

            var saved = service.SaveDocument(current, savePath);

            Assert.False(saved.IsDirty);
            Assert.Equal(FruitMachinePlatformType.MPU5, saved.GetMachineDocument().Runtime.Platform);
            Assert.Equal("Assets/Cabinet3D/Demo", saved.GetMachineDocument().CabinetAssetPath);
            Assert.True(MachineDocumentStorage.TryRead(File.ReadAllText(savePath), out var persisted));
            Assert.Equal(FruitMachinePlatformType.MPU5, persisted.Runtime.Platform);
            Assert.Equal("Assets/Cabinet3D/Demo", persisted.CabinetAssetPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
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
}
