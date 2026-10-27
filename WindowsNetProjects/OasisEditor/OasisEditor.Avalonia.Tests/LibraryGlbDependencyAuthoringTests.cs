using System.Buffers.Binary;
using System.Text;
using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class LibraryGlbDependencyAuthoringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisLibraryGlbDeps-" + Guid.NewGuid().ToString("N"));
    private readonly string _projectRoot;
    private readonly string _libraryRoot;
    private readonly AuthoringSession _project;

    public LibraryGlbDependencyAuthoringTests()
    {
        _projectRoot = Path.Combine(_root, "Project");
        _libraryRoot = Path.Combine(_root, "Library");
        Directory.CreateDirectory(Path.Combine(_projectRoot, "Assets"));
        File.WriteAllText(Path.Combine(_projectRoot, "Project.oasisproj"), """{"name":"Project","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
        _project = new AuthoringSession(_root);
        _project.OpenProject(Path.Combine(_projectRoot, "Project.oasisproj"));
    }

    [Fact]
    public void CabinetAndPrefabOpenAndSaveWithContainedExternalGlbDependencies()
    {
        var cabinetPackage = Path.Combine(_libraryRoot, "Cabinets", "Brands", "Classic");
        WriteGlbPackage(cabinetPackage, "cabinet.glb");
        File.WriteAllText(Path.Combine(cabinetPackage, "asset.cabinet3d"), CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));

        var cabinetSession = new CabinetAuthoringSession(_project);
        cabinetSession.OpenLibrary(_libraryRoot, "Cabinets/Brands/Classic/asset.cabinet3d");
        Assert.True(cabinetSession.SetModel(scale: 1.5));
        cabinetSession.Save();
        Assert.Equal("buffer", File.ReadAllText(Path.Combine(cabinetPackage, "buffer.bin")));
        Assert.Equal("image", File.ReadAllText(Path.Combine(cabinetPackage, "image.png")));

        var prefabPackage = Path.Combine(_libraryRoot, "Object3D", "Props", "Token");
        WriteGlbPackage(prefabPackage, "prefab.glb");
        var prefab = Object3DDocument.Create("Token") with { Model = new("prefab.glb", 1, "Y") };
        File.WriteAllText(Path.Combine(prefabPackage, "asset.object3d"), Object3DDocumentStorage.Serialize(prefab));
        var prefabSession = new PrefabAuthoringSession(_project);
        prefabSession.OpenLibrary(_libraryRoot, "Object3D/Props/Token/asset.object3d");
        Assert.NotNull(prefabSession.AddNode(prefab.Root.Id, "Child"));
        prefabSession.Save();
        Assert.Equal("buffer", File.ReadAllText(Path.Combine(prefabPackage, "buffer.bin")));
        Assert.Equal("image", File.ReadAllText(Path.Combine(prefabPackage, "image.png")));
    }

    [Fact]
    public void PackageValidationRejectsTraversalMissingAndAbsoluteGlbUris()
    {
        var package = Path.Combine(_libraryRoot, "Cabinets", "Unsafe");
        Directory.CreateDirectory(package);
        var session = new CabinetAuthoringSession(_project);
        foreach (var uri in new[]
                 {
                     "../outside.bin",
                     "missing.bin",
                     Path.GetFullPath(Path.Combine(_root, "outside.bin"))
                 })
        {
            var model = Path.Combine(package, "cabinet.glb");
            File.WriteAllBytes(model, MakeGlb(uri));
            var manifest = Path.Combine(package, "asset.cabinet3d");
            File.WriteAllText(manifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
            Assert.ThrowsAny<Exception>(() => session.OpenLibrary(_libraryRoot, "Cabinets/Unsafe/asset.cabinet3d"));
        }
    }

    [Fact]
    public void ProjectModelImportStillRejectsExternalGlbUris()
    {
        var source = Path.Combine(_projectRoot, "Assets", "source.glb");
        File.WriteAllBytes(source, MakeGlb("buffer.bin"));
        var session = new CabinetAuthoringSession(_project);
        Assert.ThrowsAny<Exception>(() => session.Create("Cabinet", "Assets/source.glb"));
    }

    private static void WriteGlbPackage(string package, string modelName)
    {
        Directory.CreateDirectory(package);
        File.WriteAllBytes(Path.Combine(package, modelName), MakeGlb("buffer.bin"));
        File.WriteAllText(Path.Combine(package, "buffer.bin"), "buffer");
        File.WriteAllText(Path.Combine(package, "image.png"), "image");
    }

    private static byte[] MakeGlb(string bufferUri) => MakeGlbWithJson($"{{\"asset\":{{\"version\":\"2.0\"}},\"buffers\":[{{\"uri\":\"{bufferUri.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",\"byteLength\":6}}],\"images\":[{{\"uri\":\"image.png\"}}]}}");

    private static byte[] MakeGlbWithJson(string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedLength = (jsonBytes.Length + 3) & ~3;
        var bytes = new byte[20 + paddedLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), (uint)paddedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), 0x4E4F534A);
        jsonBytes.CopyTo(bytes.AsSpan(20));
        bytes.AsSpan(20 + jsonBytes.Length).Fill(0x20);
        return bytes;
    }

    public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch { } }
}
