using OasisEditor.Automation;
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
