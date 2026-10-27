using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class LibraryThreeDAuthoringTests
{
    [Fact]
    public void CabinetCanOpenEditAndSaveNestedLibraryPackage()
    {
        using var fixture = new Fixture();
        var manifest = fixture.CreateLibraryCabinet("Cabinets/Brands/Classic/asset.cabinet3d");
        var session = new CabinetAuthoringSession(fixture.ProjectSession);

        session.OpenLibrary(fixture.LibraryRoot, "Cabinets/Brands/Classic/asset.cabinet3d");
        Assert.True(session.IsLibraryAsset);
        Assert.Equal(Path.GetFullPath(fixture.LibraryRoot), session.LibraryRoot);
        Assert.Equal(manifest, session.FilePath);
        Assert.True(session.SetModel(scale: 1.75));
        session.Save();

        var reopened = new CabinetAuthoringSession(fixture.ProjectSession);
        reopened.OpenLibrary(fixture.LibraryRoot, "Cabinets/Brands/Classic/asset.cabinet3d");
        Assert.Equal(1.75, reopened.Document.Model.Scale);
        Assert.False(reopened.IsDirty);
        Assert.True(reopened.IsLibraryAsset);
    }

    [Fact]
    public void PrefabCanOpenEditAndSaveLibraryPackage()
    {
        using var fixture = new Fixture();
        var manifest = fixture.CreateLibraryPrefab("Object3D/Starter/Props/asset.object3d");
        var session = new PrefabAuthoringSession(fixture.ProjectSession);
        session.OpenLibrary(fixture.LibraryRoot, "Object3D/Starter/Props/asset.object3d");
        var rootId = session.Document.Root.Id;
        session.AddNode(rootId, "Library edited child");
        session.Save();

        var reopened = new PrefabAuthoringSession(fixture.ProjectSession);
        reopened.OpenLibrary(fixture.LibraryRoot, "Object3D/Starter/Props/asset.object3d");
        Assert.Equal(manifest, reopened.FilePath);
        Assert.Equal("Library edited child", reopened.Document.Root.Children.Single().Name);
        Assert.True(reopened.IsLibraryAsset);
    }

    [Fact]
    public void LibraryPackageHashDetectsExternalEditAndPreservesExternalContent()
    {
        using var fixture = new Fixture();
        var manifest = fixture.CreateLibraryCabinet("Cabinets/Classic/asset.cabinet3d");
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        session.OpenLibrary(fixture.LibraryRoot, "Cabinets/Classic/asset.cabinet3d");
        session.SetModel(scale: 2);
        var package = Path.GetDirectoryName(manifest)!;
        var notes = Path.Combine(package, "notes.txt");
        File.WriteAllText(notes, "external update");

        Assert.Throws<IOException>(session.Save);
        Assert.Equal("external update", File.ReadAllText(notes));
        Assert.Equal(1, ReadCabinet(manifest).Model.Scale);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void LibraryOpenRejectsTraversalAndWrongTypeWithoutReplacingCurrentBinding()
    {
        using var fixture = new Fixture();
        var projectManifest = fixture.CreateProjectCabinet();
        fixture.CreateLibraryCabinet("Cabinets/Classic/asset.cabinet3d");
        var brokenPackage = Path.Combine(fixture.LibraryRoot, "Cabinets", "Broken");
        Directory.CreateDirectory(brokenPackage);
        File.WriteAllText(Path.Combine(brokenPackage, ProjectAssetPathService.Cabinet3DManifestFileName),
            CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("Models/missing.glb")));
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        session.Open(projectManifest);
        var originalDocument = session.Document;

        Assert.Throws<InvalidDataException>(() => session.OpenLibrary(fixture.LibraryRoot, "../outside/asset.cabinet3d"));
        Assert.Throws<InvalidDataException>(() => session.OpenLibrary(fixture.LibraryRoot, "Object3D/Classic/asset.cabinet3d"));
        Assert.Throws<FileNotFoundException>(() => session.OpenLibrary(fixture.LibraryRoot, "Cabinets/Broken/asset.cabinet3d"));
        Assert.Equal(projectManifest, session.FilePath);
        Assert.False(session.IsLibraryAsset);
        Assert.Null(session.LibraryRoot);
        Assert.Equal(originalDocument, session.Document);

        Assert.Throws<InvalidDataException>(() => session.Open(Path.Combine(fixture.LibraryRoot, "Cabinets", "Classic", "asset.cabinet3d")));
        Assert.Equal(projectManifest, session.FilePath);
    }

    [Fact]
    public void DirtyProjectDocumentCannotBeReplacedByLibraryDocumentAndCloseClearsLibraryMetadata()
    {
        using var fixture = new Fixture();
        var projectManifest = fixture.CreateProjectCabinet();
        fixture.CreateLibraryCabinet("Cabinets/Classic/asset.cabinet3d");
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        session.Open(projectManifest);
        session.SetModel(scale: 1.5);

        Assert.Throws<InvalidOperationException>(() => session.OpenLibrary(fixture.LibraryRoot, "Cabinets/Classic/asset.cabinet3d"));
        Assert.Equal(projectManifest, session.FilePath);
        Assert.False(session.IsLibraryAsset);

        session.Undo();
        session.OpenLibrary(fixture.LibraryRoot, "Cabinets/Classic/asset.cabinet3d");
        Assert.True(session.IsLibraryAsset);
        session.Close();
        Assert.False(session.IsLibraryAsset);
        Assert.Null(session.LibraryRoot);
        Assert.Null(session.FilePath);
    }

    private static CabinetDocument ReadCabinet(string manifest)
    {
        Assert.True(CabinetDocumentStorage.TryRead(File.ReadAllText(manifest), out var document));
        return document;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "OasisLibrary3D-" + Guid.NewGuid().ToString("N"));
        public string ProjectRoot => Path.Combine(_tempRoot, "Project");
        public string LibraryRoot => Path.Combine(_tempRoot, "Oasis Library");
        public AuthoringSession ProjectSession { get; }

        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Assets"));
            File.WriteAllText(Path.Combine(ProjectRoot, "Project.oasisproj"), """{"name":"Project","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            ProjectSession = new AuthoringSession(_tempRoot);
            ProjectSession.OpenProject(Path.Combine(ProjectRoot, "Project.oasisproj"));
        }

        public string CreateProjectCabinet()
        {
            var package = Path.Combine(ProjectRoot, "Assets", "Cabinet3D", "ProjectCabinet");
            return CreateCabinetPackage(package);
        }

        public string CreateLibraryCabinet(string relativeManifest)
        {
            var manifest = Path.Combine(LibraryRoot, relativeManifest.Replace('/', Path.DirectorySeparatorChar));
            return CreateCabinetPackage(Path.GetDirectoryName(manifest)!);
        }

        public string CreateLibraryPrefab(string relativeManifest)
        {
            var manifest = Path.Combine(LibraryRoot, relativeManifest.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            var document = Object3DDocument.Create("Library Prefab");
            document = document with { Root = document.Root with { Components = document.Root.Components.Where(component => component.Kind == Object3DComponentDefinition.TransformKind).ToArray() } };
            File.WriteAllText(manifest, Object3DDocumentStorage.Serialize(document));
            return manifest;
        }

        private static string CreateCabinetPackage(string package)
        {
            Directory.CreateDirectory(Path.Combine(package, "Models"));
            File.WriteAllBytes(Path.Combine(package, "Models", "Cabinet.glb"), CreateGlb());
            var manifest = Path.Combine(package, ProjectAssetPathService.Cabinet3DManifestFileName);
            File.WriteAllText(manifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("Models/Cabinet.glb")));
            return manifest;
        }

        public void Dispose() { try { Directory.Delete(_tempRoot, recursive: true); } catch { } }
    }

    private static byte[] CreateGlb()
    {
        const string json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[]}]}";
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        var paddedLength = (jsonBytes.Length + 3) & ~3;
        var result = new byte[20 + paddedLength];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), 0x46546C67);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)result.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)paddedLength);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 0x4E4F534A);
        jsonBytes.CopyTo(result.AsSpan(20));
        result.AsSpan(20 + jsonBytes.Length).Fill(0x20);
        return result;
    }
}
