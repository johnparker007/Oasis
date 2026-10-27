using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class FmlImportWorkflowTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "OasisFmlImportWorkflowTests", Guid.NewGuid().ToString("N"));

    public FmlImportWorkflowTests() => Directory.CreateDirectory(_workspace);

    [Fact]
    public void ImportFml_ConfinesSourceAndLeavesPanelUnchangedOnDecodeFailure()
    {
        var session = OpenPanel();
        var source = Path.Combine(_workspace, "broken.bin");
        File.WriteAllBytes(source, [0xFF, 0x00, 0xA5]);

        var result = session.ImportFml(source);

        Assert.False(result.Succeeded);
        Assert.Empty(session.Elements);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
        Assert.Throws<InvalidDataException>(() => session.ImportFml(Path.GetFullPath(Path.Combine(_workspace, "..", "outside.bin"))));
        Assert.Throws<ArgumentException>(() => session.ImportFml(source, copyAssets: false));
    }

    [Fact]
    public void ImportFml_RefusesExistingImportDestinationBeforeReadingSource()
    {
        var session = OpenPanel();
        var source = Path.Combine(_workspace, "Layout.bin");
        File.WriteAllBytes(source, []);
        var destination = Path.Combine(Path.GetDirectoryName(session.ProjectFilePath)!, "Assets", "FmlImport", "Layout");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "authored.txt"), "preserve");

        Assert.Throws<InvalidOperationException>(() => session.ImportFml(source));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(destination, "authored.txt")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(destination)!),
            path => Path.GetFileName(path).StartsWith(".oasis-import-reservation-", StringComparison.Ordinal));
        Assert.Empty(session.Elements);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ImportFml_MapsValidEmptyLayoutWithoutChangingPanelHistory()
    {
        var session = OpenPanel();
        var source = Path.Combine(_workspace, "EmptyLayout.bin");
        File.WriteAllBytes(source, []);

        var result = session.ImportFml(source);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(0, result.ImportedElementCount);
        Assert.Empty(result.InputDefinitions);
        Assert.Empty(session.ImportedInputDefinitions);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
        var destination = Path.Combine(Path.GetDirectoryName(session.ProjectFilePath)!, "Assets", "FmlImport", "EmptyLayout");
        Assert.True(Directory.Exists(destination));
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    private AuthoringSession OpenPanel()
    {
        var session = new AuthoringSession(_workspace);
        session.CreateProject("FML Project");
        session.CreatePanel("Panel");
        return session;
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);
}
