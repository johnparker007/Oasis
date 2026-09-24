using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AssetReferenceAndLibraryTests
{
    [Fact]
    public void ReferencesNormalizeAndRejectAbsoluteOrTraversalPaths()
    {
        Assert.Equal("Reels/Standard/asset.reel", AssetReference.Library(@"Reels\Standard\asset.reel").Path);
        Assert.Throws<ArgumentException>(() => AssetReference.Library("../asset.reel"));
        Assert.Throws<ArgumentException>(() => AssetReference.Library(Path.GetFullPath("asset.reel")));
        foreach (var rooted in new[] { @"\Reels\Foo\asset.reel", "/Reels/Foo/asset.reel", @"C:Reels\Foo\asset.reel", @"C:\Reels\Foo\asset.reel", @"\\server\share\asset.reel" })
            Assert.Throws<ArgumentException>(() => AssetReference.Library(rooted));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetReference((AssetReferenceScope)42, "asset.reel"));
    }

    [Fact]
    public void MachineRoundTripsTypedProjectAndLibraryReferencesWithoutLibraryRoot()
    {
        var machine = MachineDocument.Create("Game") with
        {
            CabinetAsset = AssetReference.Library("Cabinets/Vogue/asset.cabinet3d"),
            ReelAssignments = [new(MachineObjectReference.Reel(0), AssetReference.Project("Assets/Reels/Standard/asset.reel"))]
        };
        var json = MachineDocumentStorage.Serialize(machine);
        Assert.True(MachineDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(machine.CabinetAsset, result.CabinetAsset);
        Assert.Equal(machine.ReelAssignments[0].ReelAsset, result.ReelAssignments[0].ReelAsset);
        Assert.DoesNotContain(Path.GetTempPath(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"scope\": \"library\"", json);
        Assert.Contains("\"scope\": \"project\"", json);
    }

    [Fact]
    public void MachineRejectsUnknownOrNumericAssetScope()
    {
        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Game") with { CabinetAsset = AssetReference.Library("Cabinets/Vogue/asset.cabinet3d") });
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"library\"", "\"remote\""), out _, out _));
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"library\"", "1"), out _, out _));
    }

    [Fact]
    public void ResolverUsesIndependentRootsAndIsPortable()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "Project");
        var project = new EditorProject { Name = "Project", ProjectDirectory = projectRoot, ProjectFilePath = Path.Combine(projectRoot, "Project.oasisproject"), AssetsDirectory = Path.Combine(projectRoot, "Assets"), GeneratedDirectory = Path.Combine(projectRoot, "Generated") };
        var reference = AssetReference.Library("Reels/Standard/asset.reel");
        var resolver = new AssetReferenceResolver();
        Assert.Equal(Path.GetFullPath(Path.Combine("C:/LibraryA", "Reels/Standard/asset.reel")), resolver.Resolve(project, "C:/LibraryA", reference));
        Assert.Equal(Path.GetFullPath(Path.Combine("D:/LibraryB", "Reels/Standard/asset.reel")), resolver.Resolve(project, "D:/LibraryB", reference));
    }

    [Fact]
    public void CatalogDiscoversValidEntriesSkipsMalformedAndKeepsDuplicateNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisLibrary_" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(Path.Combine(root, "Reels", "A", "asset.reel"), ReelDocumentStorage.Serialize(ReelDocument.Create("Same")));
            Write(Path.Combine(root, "Reels", "B", "asset.reel"), ReelDocumentStorage.Serialize(ReelDocument.Create("Same")));
            Write(Path.Combine(root, "Reels", "Broken", "asset.reel"), "not json");
            var entries = new OasisAssetLibraryCatalog().Discover(root);
            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry => Assert.Equal("Same", entry.DisplayName));
            Assert.Equal(2, entries.Select(entry => entry.Reference.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingLibraryRootProducesAnEmptyCatalog() => Assert.Empty(new OasisAssetLibraryCatalog().Discover(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    private static void Write(string path, string content) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content); }
}
