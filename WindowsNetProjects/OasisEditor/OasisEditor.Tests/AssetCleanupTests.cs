using System.Text.Json;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class AssetCleanupTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "OasisCleanup-" + Guid.NewGuid().ToString("N"));
    private readonly AuthoringSession session;
    private string Root => Path.GetDirectoryName(session.ProjectFilePath)!;
    public AssetCleanupTests()
    {
        Directory.CreateDirectory(workspace); session = new(workspace); session.CreateProject("Cleanup");
    }
    private string Write(string path, string content = "test")
    {
        var full = Path.Combine(Root, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, content); return full;
    }
    private AssetCleanupPlan Scan(params AssetReferenceSnapshot[] history) => UnreferencedAssetService.Scan(session.ProjectFilePath!, history);
    private static string[] Paths(AssetCleanupPlan plan) => plan.Candidates.Select(c => c.Path).ToArray();

    [Fact]
    public void ScanPreservesSavedReferencesPackagesDependenciesAndUnknownFiles()
    {
        Write("Assets/FmlImport/live/p.png"); Write("Assets/FmlImport/orphan/p.png");
        Write("Assets/Loose/free.wav"); Write("Assets/Loose/texture.png"); Write("Assets/Loose/uri image.png");
        Write("Assets/Loose/model.gltf", "{\"images\":[{\"uri\":\"uri%20image.png\"}]}");
        Write("Assets/Loose/model.mtl", "map_Kd -s 1 1 1 texture.png");
        Write("Assets/Loose/rom.bin"); Write("Assets/Loose/script.lua", "print('safe')");
        Write("Assets/Panel2D/Saved/asset.panel2d", "{\"image\":\"Assets/FmlImport/live/p.png\"}");
        Write("Assets/Panel2D/Saved/local.png"); Write("Generated/Trash/old.json", "{\"image\":\"Assets/FmlImport/orphan/p.png\"}");
        Assert.Equal(new[] { "Assets/FmlImport/orphan/p.png", "Assets/Loose/free.wav", "Assets/Loose/model.gltf" }, Paths(Scan()));
    }

    [Fact]
    public void CurrentAndRedoHistoryProtectMediaAfterUndo()
    {
        session.CreatePanel("Panel");
        var source = Path.Combine(workspace, "source.png");
        using (var bitmap = new SKBitmap(2, 2)) using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(source, data.ToArray());
        var id = session.ImportImage(source);
        Write("Assets/FmlImport/saved/p.png"); Write("Assets/FmlImport/redo/p.png");
        session.UpdatePanelElementInspectorSettings(id, new PanelElementInspectorSettings(AssetPath: "Assets/FmlImport/saved/p.png"));
        session.Save();
        session.UpdatePanelElementInspectorSettings(id, new PanelElementInspectorSettings(AssetPath: "Assets/FmlImport/redo/p.png"));
        session.Undo();
        Assert.True(session.CanRedo); Assert.Empty(Paths(Scan(session.CaptureAssetReferenceHistory().ToArray())));
        Assert.Equal(new[] { "Assets/FmlImport/redo/p.png" }, Paths(Scan()));
    }

    [Fact]
    public void SelectionDeletesOnlyCheckedFilesAndPrunesTheirEmptyFolders()
    {
        var a = Write("Assets/FmlImport/a/x.png"); var b = Write("Assets/FmlImport/b/y.png");
        var result = UnreferencedAssetService.DeleteSelected(Scan(), ["Assets/FmlImport/a/x.png"], []);
        Assert.Empty(result.Failures); Assert.Single(result.Deleted); Assert.False(File.Exists(a)); Assert.True(File.Exists(b));
        Assert.False(Directory.Exists(Path.GetDirectoryName(a))); Assert.True(Directory.Exists(Path.GetDirectoryName(b)));
    }

    [Theory]
    [InlineData("history")]
    [InlineData("saved")]
    [InlineData("changed")]
    [InlineData("library")]
    public void StalePreviewPreflightsWholeSelectionBeforeDeletingAnything(string reason)
    {
        var a = Write("Assets/Loose/a.png"); var b = Write("Assets/Loose/b.png"); var plan = Scan();
        AssetReferenceSnapshot[] history = [];
        string[] libraries = [];
        if (reason == "history") history = [new(Root, JsonSerializer.Serialize(new { Image = "Assets/Loose/b.png" }))];
        if (reason == "saved") Write("Assets/refs.json", "{\"image\":\"Assets/Loose/b.png\"}");
        if (reason == "changed") File.AppendAllText(b, "changed");
        if (reason == "library") libraries = [Path.GetDirectoryName(b)!];
        Assert.Throws<InvalidOperationException>(() => UnreferencedAssetService.DeleteSelected(plan, Paths(plan), history, libraries));
        Assert.True(File.Exists(a)); Assert.True(File.Exists(b));
    }

    [Fact]
    public void LibraryAndConcurrentEditorHistoryAreProtected()
    {
        Write("Assets/Library/untouched.png"); Write("Assets/Library/broken.json", "not json");
        using var lease = ProjectCleanupLease.Acquire(session.ProjectFilePath!);
        Assert.Empty(UnreferencedAssetService.Scan(session.ProjectFilePath!, [], owner: lease, protectedRoots: [Path.Combine(Root, "Assets/Library")]).Candidates);
        using (var other = ProjectCleanupLease.Acquire(session.ProjectFilePath!))
            Assert.Throws<InvalidOperationException>(() => UnreferencedAssetService.Scan(session.ProjectFilePath!, [], owner: lease));
    }

    [Fact]
    public void MalformedMetadataAbortsAndLiveImportIsNeverOffered()
    {
        var media = Write("Assets/FmlImport/active/a.png");
        var lockFile = Write("Assets/FmlImport/active/.oasis-importing.lock");
        using (var importing = new FileStream(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.Empty(Paths(Scan()));
        Assert.Equal(2, Scan().Candidates.Count);
        Write("Assets/bad.json", "{"); Assert.ThrowsAny<JsonException>(() => Scan()); Assert.True(File.Exists(media));
    }

    [Fact]
    public void EmptyFmlFoldersAreSelectableAndMustStillBeEmpty()
    {
        var empty = Path.Combine(Root, "Assets/FmlImport/empty/nested"); Directory.CreateDirectory(empty);
        var plan = Scan(); Assert.True(Assert.Single(plan.Candidates).IsDirectory);
        Write("Assets/FmlImport/empty/nested/new.png");
        Assert.Throws<InvalidOperationException>(() => UnreferencedAssetService.DeleteSelected(plan, Paths(plan), []));
        File.Delete(Path.Combine(empty, "new.png"));
        Assert.Empty(UnreferencedAssetService.DeleteSelected(plan, Paths(plan), []).Failures);
        Assert.False(Directory.Exists(Path.GetDirectoryName(empty)));
    }

    public void Dispose() => Directory.Delete(workspace, true);
}
