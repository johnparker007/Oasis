using System.Buffers.Binary;
using System.Text;
using OasisEditor;
using OasisEditor.Authoring.Assets;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class LibraryAssetServiceTests : IDisposable
{
    [Fact]
    public void CopyResolvesProjectRelativeManifestAndRejectsGeneratedContent()
    {
        var project=CreateProject();var manifest=Path.Combine(project.AssetsDirectory,"Reels","Relative","asset.reel");
        Write(manifest,ReelDocumentStorage.Serialize(ReelDocument.Create("Relative")));
        var copied=LibraryAssetService.CopyToLibrary(project,"Assets/Reels/Relative/asset.reel",Path.Combine(_root,"library"));
        Assert.True(copied.Success,copied.Error);
        var generated=Path.Combine(project.GeneratedDirectory,"Reel","asset.reel");Write(generated,File.ReadAllText(manifest));
        Assert.False(LibraryAssetService.CopyToLibrary(project,generated,Path.Combine(_root,"library"),"Generated").Success);
        Assert.False(LibraryAssetService.CopyToLibrary(project,manifest,Path.GetDirectoryName(manifest)!,"Nested").Success);
        Assert.Single(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(manifest)!));
    }
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisLibraryService_" + Guid.NewGuid().ToString("N"));
    public LibraryAssetServiceTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    [Fact]
    public void DiscoverKeepsDuplicatesAndReportsInvalidPackages()
    {
        WritePackage("Reels", "A", "asset.reel", ReelDocumentStorage.Serialize(ReelDocument.Create("Same")));
        WritePackage("Reels", "B", "asset.reel", ReelDocumentStorage.Serialize(ReelDocument.Create("Same")));
        WritePackage("Reels", "Bad", "asset.reel", "not json");
        var result = LibraryAssetService.Discover(Path.Combine(_root, "library"));
        Assert.Equal(2, result.Assets.Count);
        Assert.Single(result.Diagnostics);
        Assert.All(result.Assets, a => Assert.Equal("Same", a.DisplayName));
        Assert.Equal(2, result.Assets.Select(a => a.Reference.Path).Distinct().Count());
    }

    [Fact]
    public void DiscoverFindsPackagesNestedUnderBrandFolders()
    {
        WritePackage(Path.Combine("Cabinets", "Brand A"), "Vogue", "asset.cabinet3d", CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
        var nested = Path.Combine(_root, "library", "Cabinets", "Brand A", "Vogue");
        File.WriteAllBytes(Path.Combine(nested, "cabinet.glb"), MakeGlb("{\"asset\":{\"version\":\"2.0\"}}"));
        var result = LibraryAssetService.Discover(Path.Combine(_root, "library"));
        var cabinet = Assert.Single(result.Assets);
        Assert.Equal(EditorAssetType.Cabinet3D, cabinet.Type);
        Assert.Equal(Path.Combine("Cabinets", "Brand A", "Vogue", "asset.cabinet3d").Replace('\\', '/'), cabinet.Reference.Path);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void CopyRejectsCaseOnlySiblingOutsideProjectOnCaseSensitiveFilesystems()
    {
        if (OperatingSystem.IsWindows()) return;
        var project = CreateProject();
        var siblingPackage = Path.Combine(_root, "Project", "Assets", "Reels", "Outside");
        Directory.CreateDirectory(siblingPackage);
        var manifest = Path.Combine(siblingPackage, "asset.reel");
        File.WriteAllText(manifest, ReelDocumentStorage.Serialize(ReelDocument.Create("Outside")));
        var result = LibraryAssetService.CopyToLibrary(project, manifest, Path.Combine(_root, "library"));
        Assert.False(result.Success);
        Assert.False(Directory.Exists(Path.Combine(_root, "library")));
    }

    [Fact]
    public void CatalogReportsReparseBranchAndContinuesOtherBranchesWhenSupported()
    {
        var library = Path.Combine(_root, "library");
        Write(Path.Combine(library, "Reels", "Good", "asset.reel"), ReelDocumentStorage.Serialize(ReelDocument.Create("Good")));
        var external = Path.Combine(_root, "external");
        Write(Path.Combine(external, "asset.reel"), ReelDocumentStorage.Serialize(ReelDocument.Create("Linked")));
        Directory.CreateDirectory(Path.Combine(library, "Reels"));
        try { Directory.CreateSymbolicLink(Path.Combine(library, "Reels", "Linked"), external); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException) { return; }
        var result = LibraryAssetService.Discover(library);
        Assert.Single(result.Assets);
        Assert.Equal("Good", result.Assets[0].DisplayName);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.PackagePath.EndsWith("Linked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CopyAndImportPreserveAssetIdAndDependenciesAndNeverOverwrite()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Cabinet3D", "Cabinet");
        Directory.CreateDirectory(package);
        var glb = MakeGlb("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"parts/buffer.bin\"}],\"images\":[{\"uri\":\"parts/image.png\"}]}");
        File.WriteAllBytes(Path.Combine(package, "cabinet.glb"), glb);
        Write(Path.Combine(package, "parts", "buffer.bin"), "buffer");
        Write(Path.Combine(package, "parts", "image.png"), "image");
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb");
        var manifest = Path.Combine(package, "asset.cabinet3d");
        File.WriteAllText(manifest, CabinetDocumentStorage.Serialize(cabinet));

        var library = Path.Combine(_root, "library");
        var copied = LibraryAssetService.CopyToLibrary(project, manifest, library);
        Assert.True(copied.Success, copied.Error);
        Assert.Equal(AssetReferenceScope.Library, copied.Reference!.Scope);
        Assert.True(File.Exists(Path.Combine(library, "Cabinets", "Cabinet", "parts", "buffer.bin")));
        Assert.True(File.Exists(Path.Combine(library, "Cabinets", "Cabinet", "parts", "image.png")));
        Assert.True(CabinetDocumentStorage.TryRead(File.ReadAllText(copied.ManifestPath!), out var afterCopy));
        Assert.Equal(cabinet.Model, afterCopy.Model);
        Assert.Equal(cabinet.SurfaceTargetSettings, afterCopy.SurfaceTargetSettings);

        var imported = LibraryAssetService.ImportToProject(project, copied.Reference, library, "Imported");
        Assert.True(imported.Success, imported.Error);
        Assert.Equal(AssetReferenceScope.Project, imported.Reference!.Scope);
        Assert.True(File.Exists(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Imported", "cabinet.glb")));
        Assert.False(LibraryAssetService.ImportToProject(project, copied.Reference, library, "Imported").Success);
        Assert.Equal("buffer", File.ReadAllText(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Imported", "parts", "buffer.bin")));
    }

    [Fact]
    public void RejectsGlbTraversalAndLeavesNoDestinationPackage()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Cabinet3D", "Unsafe");
        Directory.CreateDirectory(package);
        File.WriteAllBytes(Path.Combine(package, "cabinet.glb"), MakeGlb("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"../outside.bin\"}]}") );
        File.WriteAllText(Path.Combine(package, "asset.cabinet3d"), CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
        var result = LibraryAssetService.CopyToLibrary(project, Path.Combine(package, "asset.cabinet3d"), Path.Combine(_root, "library"));
        Assert.False(result.Success);
        Assert.False(Directory.Exists(Path.Combine(_root, "library")));
    }

    [Fact]
    public void CopiesPrefabModelAndPreservesNodeAndComponentIds()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Object3D", "Token");
        Directory.CreateDirectory(package);
        File.WriteAllBytes(Path.Combine(package, "model.glb"), MakeGlb("{\"asset\":{\"version\":\"2.0\"}}"));
        var prefab = Object3DDocument.Create("Token") with { Id = "f1212c3e-68c1-40a1-844c-34c276aa081e" };
        File.WriteAllText(Path.Combine(package, "asset.object3d"), Object3DDocumentStorage.Serialize(prefab));
        var result = LibraryAssetService.CopyToLibrary(project, Path.Combine(package, "asset.object3d"), Path.Combine(_root, "library"));
        Assert.True(result.Success, result.Error);
        Assert.True(Object3DDocumentStorage.TryRead(File.ReadAllText(result.ManifestPath!), out var copied, out var error), error);
        Assert.Equal(prefab.Id, copied.Id);
        Assert.Equal(prefab.Root.Id, copied.Root.Id);
        Assert.Equal(prefab.Root.Components.Select(c => c.ComponentId), copied.Root.Components.Select(c => c.ComponentId));
        Assert.True(File.Exists(Path.Combine(_root, "library", "Object3D", "Token", "model.glb")));
    }

    [Fact]
    public void CopiesPhysicalReelMetadataAndStableId()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Reels", "Steel");
        Directory.CreateDirectory(package);
        var reel = ReelDocument.Create("Steel") with { Id = "17eef64a-06fd-4cff-a10b-d186aef53e87", DiameterMm = 292, WidthMm = 68 };
        var manifest = Path.Combine(package, "asset.reel");
        File.WriteAllText(manifest, ReelDocumentStorage.Serialize(reel));
        var result = LibraryAssetService.CopyToLibrary(project, manifest, Path.Combine(_root, "library"));
        Assert.True(result.Success, result.Error);
        Assert.True(ReelDocumentStorage.TryRead(File.ReadAllText(result.ManifestPath!), out var copied, out var error), error);
        Assert.Equal(reel, copied);
    }

    [Fact]
    public void RejectsPackageReparsePointsWhenSupportedByTheHost()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Cabinet3D", "Linked");
        Directory.CreateDirectory(Path.Combine(package, "links"));
        File.WriteAllBytes(Path.Combine(package, "cabinet.glb"), MakeGlb("{\"asset\":{\"version\":\"2.0\"}}"));
        File.WriteAllText(Path.Combine(package, "asset.cabinet3d"), CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
        var outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(outside, "outside");
        try { File.CreateSymbolicLink(Path.Combine(package, "links", "outside.bin"), outside); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException) { return; }

        var result = LibraryAssetService.CopyToLibrary(project, Path.Combine(package, "asset.cabinet3d"), Path.Combine(_root, "library"));
        Assert.False(result.Success);
        Assert.False(Directory.Exists(Path.Combine(_root, "library")));
    }

    private EditorProject CreateProject()
    {
        var root = Path.Combine(_root, "project");
        var assets = Path.Combine(root, "Assets");
        Directory.CreateDirectory(assets);
        return new EditorProject { Name = "Project", ProjectDirectory = root, ProjectFilePath = Path.Combine(root, "Project.oasisproject"), AssetsDirectory = assets, GeneratedDirectory = Path.Combine(root, "Generated") };
    }
    private void WritePackage(string type, string name, string file, string content) => Write(Path.Combine(_root, "library", type, name, file), content);
    private static void Write(string path, string content) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content); }
    private static byte[] MakeGlb(string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var padded = (jsonBytes.Length + 3) & ~3;
        var bytes = new byte[12 + 8 + padded];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), (uint)padded);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), 0x4E4F534A);
        jsonBytes.CopyTo(bytes.AsSpan(20));
        bytes.AsSpan(20 + jsonBytes.Length).Fill(0x20);
        return bytes;
    }
}
