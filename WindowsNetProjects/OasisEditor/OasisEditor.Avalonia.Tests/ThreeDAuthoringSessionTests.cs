using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class ThreeDAuthoringSessionTests
{
    [Fact]
    public void CabinetSessionImportsModelEditsSurfaceAndReflectionDataAndSavesNativePackage()
    {
        using var fixture = new Fixture();
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Cabinet", "Assets/Source.glb");
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(manifest)!, "Models", "Source.glb")));
        Assert.True(session.SetModel(scale: 1.5, upAxis: "Z"));
        Assert.True(session.SetSurfaceTarget(new("screen/main", CabinetSurfaceTargetSettings.InvertedFrontSide, 90, true)));
        var plane = new CabinetReflectionPlane(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 1, 1);
        var definition = new CabinetReflectionDefinition("screen-reflection", "screen/main", 0,
            [new("screen/secondary", plane)], CabinetReflectionSettings.PolishedChrome);
        Assert.True(session.SetReflections([definition]));
        session.Save();

        var reopened = new CabinetAuthoringSession(fixture.ProjectSession);
        reopened.Open(manifest);
        Assert.Equal(1.5, reopened.Document.Model.Scale);
        Assert.Equal("Z", reopened.Document.Model.UpAxis);
        Assert.Equal(90, reopened.Document.SurfaceTargetSettings.Single().FaceRotation);
        Assert.Equal("screen/secondary", reopened.Document.Reflections!.Single().Sources.Single().SourceSurfaceTargetId);
        Assert.False(reopened.IsDirty);
    }

    [Fact]
    public void CabinetSessionRejectsExternalPackageMutationAndInvalidGeometry()
    {
        using var fixture = new Fixture();
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Cabinet", "Assets/Source.glb");
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetModel(scale: double.NaN));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(manifest)!, "notes.txt"), "outside edit");
        session.SetModel(scale: 2);
        Assert.Throws<IOException>(session.Save);
    }

    [Fact]
    public void OpenAndCreateRejectReplacementWhenCurrentDocumentIsDirty()
    {
        using var fixture = new Fixture();
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Cabinet", "Assets/Source.glb");
        session.SetModel(scale: 2);
        Assert.Throws<InvalidOperationException>(() => session.Open(manifest));
        Assert.Throws<InvalidOperationException>(() => session.Create("WouldBeLost", "Assets/Source.glb"));
        Assert.Throws<InvalidOperationException>(() => session.Close());
        Assert.Equal(manifest, session.FilePath);
        Assert.Equal(2, session.Document.Model.Scale);
        Assert.False(Directory.Exists(Path.Combine(fixture.Assets, "Cabinet3D", "WouldBeLost")));
    }

    [Fact]
    public void FailedOpenLeavesCurrentDocumentUntouched()
    {
        using var fixture = new Fixture();
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Cabinet", "Assets/Source.glb");
        var brokenPackage = Path.Combine(fixture.Assets, "Cabinet3D", "Broken");
        Directory.CreateDirectory(brokenPackage);
        File.WriteAllText(Path.Combine(brokenPackage, ProjectAssetPathService.Cabinet3DManifestFileName),
            CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));
        var before = session.Document;
        Assert.Throws<FileNotFoundException>(() => session.Open(Path.Combine(brokenPackage, ProjectAssetPathService.Cabinet3DManifestFileName)));
        Assert.Equal(manifest, session.FilePath);
        Assert.Equal(before, session.Document);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void PrefabSessionAuthorsHierarchyComponentsAndPerNodeImportedModel()
    {
        using var fixture = new Fixture();
        var session = new PrefabAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Display");
        var rootId = session.Document.Root.Id;
        var first = session.AddNode(rootId, "Frame");
        var second = session.AddNode(rootId, "Screen");
        Assert.True(session.AddComponent(first, Object3DComponentDefinition.ColliderKind));
        Assert.True(session.SetCollider(first, new(Object3DColliderKind.Box, Center: [0, 0, 0], Size: [1, 2, 3], SourceFrame: Object3DColliderSourceFrame.NodeLocal)));
        var imported = session.ImportModel(second, "Assets/Source.glb", 1.25, "X");
        Assert.Equal(Fixture.SourceGlb, File.ReadAllBytes(Path.Combine(fixture.Assets, "Source.glb")));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(manifest)!, imported.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(session.ReparentNode(second, first));
        Assert.Equal(second, session.Document.Root.Children.Single().Children.Single().Id);
        Assert.True(session.RemoveNode(first));
        Assert.Empty(session.Document.Root.Children);
        session.Undo();
        Assert.Single(session.Document.Root.Children);
        session.Save();

        var reopened = new PrefabAuthoringSession(fixture.ProjectSession);
        reopened.Open(manifest);
        Assert.Equal(Object3DDocument.CurrentSchemaVersion, reopened.Document.SchemaVersion);
        Assert.Equal("Screen", reopened.Document.Root.Children.Single().Children.Single().Name);
        Assert.False(reopened.IsDirty);
    }

    [Fact]
    public void PrefabSessionReturnsDeepCloneAndGuardsProjectSwitch()
    {
        using var fixture = new Fixture();
        var session = new PrefabAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Group");
        session.AddNode(session.Document.Root.Id, "Child");
        var copy = session.Document;
        copy.Root.Children[0].Components[0] = Object3DComponentDefinition.CreateCollider(Object3DColliderDefinition.None);
        Assert.Equal(Object3DComponentDefinition.TransformKind, session.Document.Root.Children[0].Components[0].Kind);
        Assert.True(session.CanUndo);
        fixture.OpenSecondProject();
        Assert.False(session.IsProjectCurrent);
        Assert.False(session.CanUndo);
        Assert.False(session.CanRedo);
        Assert.Throws<InvalidOperationException>(() => session.Open(manifest));
        Assert.Throws<InvalidOperationException>(() => session.Save());
        Assert.True(File.Exists(manifest));
    }

    [Fact]
    public void PrefabSessionRejectsReparentCycleAndKeepsStableComponentIdsOnEdit()
    {
        using var fixture = new Fixture();
        var session = new PrefabAuthoringSession(fixture.ProjectSession);
        session.Create("Group");
        var child = session.AddNode(session.Document.Root.Id, "Child");
        var grandchild = session.AddNode(child, "Grandchild");
        Assert.Throws<InvalidOperationException>(() => session.ReparentNode(child, grandchild));
        var componentId = session.Document.Root.Children[0].Components.Single(c => c.Kind == Object3DComponentDefinition.TransformKind).ComponentId;
        session.SetTransform(child, MachineObjectTransform.Identity with { Position = new(1, 2, 3) });
        Assert.Equal(componentId, session.Document.Root.Children[0].Components.Single(c => c.Kind == Object3DComponentDefinition.TransformKind).ComponentId);
    }

    [Fact]
    public void StaleCleanSessionCanReopenInNewProjectAndCloseCleanly()
    {
        using var fixture = new Fixture();
        var session = new PrefabAuthoringSession(fixture.ProjectSession);
        session.Create("Group");
        session.AddNode(session.Document.Root.Id, "Saved child");
        session.Save();
        Assert.True(session.CanUndo);
        fixture.OpenSecondProject();
        Assert.False(session.IsProjectCurrent);
        Assert.False(session.CanUndo);
        var package = Path.Combine(fixture.OtherAssets, "Object3D", "NewGroup");
        Directory.CreateDirectory(package);
        var otherDocument = Object3DDocument.Create("NewGroup");
        otherDocument = otherDocument with { Root = otherDocument.Root with { Components = [Object3DComponentDefinition.CreateTransform()] } };
        File.WriteAllText(Path.Combine(package, ProjectAssetPathService.Object3DManifestFileName), Object3DDocumentStorage.Serialize(otherDocument));
        session.Open(Path.Combine(package, ProjectAssetPathService.Object3DManifestFileName));
        Assert.True(session.IsProjectCurrent);
        session.Close();
        Assert.Null(session.FilePath);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ExternalGlbReferencesAreRejectedBeforePackageCreation()
    {
        using var fixture = new Fixture();
        File.WriteAllBytes(Path.Combine(fixture.Assets, "External.glb"), CreateGlb("""{"asset":{"version":"2.0"},"buffers":[{"uri":"outside.bin","byteLength":0}]}"""));
        var session = new PrefabAuthoringSession(fixture.ProjectSession);
        Assert.Throws<InvalidDataException>(() => session.Create("Rejected", "Assets/External.glb"));
        Assert.False(Directory.Exists(Path.Combine(fixture.Assets, "Object3D", "Rejected")));
        var objectRoot = Path.Combine(fixture.Assets, "Object3D");
        Assert.True(!Directory.Exists(objectRoot) || !Directory.EnumerateDirectories(objectRoot).Any(path => Path.GetFileName(path).StartsWith(".oasis-3d-", StringComparison.Ordinal)));
    }

    [Fact]
    public void PackageHashBlocksImportAfterConcurrentExternalEdit()
    {
        using var fixture = new Fixture();
        var session = new PrefabAuthoringSession(fixture.ProjectSession);
        session.Create("Group");
        var nodeId = session.AddNode(session.Document.Root.Id, "Model");
        var package = Path.GetDirectoryName(session.FilePath!)!;
        var note = Path.Combine(package, "notes.txt");
        File.WriteAllText(note, "other editor's data");
        Assert.Throws<IOException>(() => session.ImportModel(nodeId, "Assets/Source.glb"));
        Assert.Equal("other editor's data", File.ReadAllText(note));
        Assert.False(Directory.Exists(Path.Combine(package, "Models")));
    }

    [Fact]
    public void PackageReservationRollsBackOnlyItsStagingDirectoryWhenFinalNameIsTaken()
    {
        using var fixture = new Fixture();
        var typeRoot = Path.Combine(fixture.Assets, "Object3D");
        Directory.CreateDirectory(typeRoot);
        var final = Path.Combine(typeRoot, "Taken");
        var staging = Path.Combine(typeRoot, ".oasis-3d-owned");
        Directory.CreateDirectory(final);
        File.WriteAllText(Path.Combine(final, "keep.txt"), "unrelated");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "discard.txt"), "owned staging");
        using (var reservation = new ThreeDAuthoringSessionBase.PackageReservation(fixture.ProjectRoot, staging, final))
            Assert.Throws<IOException>(reservation.Commit);
        Assert.False(Directory.Exists(staging));
        Assert.Equal("unrelated", File.ReadAllText(Path.Combine(final, "keep.txt")));
    }

    [Fact]
    public void PackageFingerprintRejectsDirectoryReparsePointsBeforeTraversal()
    {
        using var fixture = new Fixture();
        var session = new CabinetAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Cabinet", "Assets/Source.glb");
        var package = Path.GetDirectoryName(manifest)!;
        var outside = Path.Combine(fixture.ProjectRoot, "Outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "keep.txt"), "outside");
        try { Directory.CreateSymbolicLink(Path.Combine(package, "linked"), outside); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            return;
        }
        Assert.Throws<InvalidDataException>(session.Save);
        Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "keep.txt")));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Oasis3DAuthoring-" + Guid.NewGuid().ToString("N"));
        public string Assets => Path.Combine(ProjectRoot, "Assets");
        public string OtherAssets => Path.Combine(_root, "Other", "Assets");
        public string ProjectRoot => Path.Combine(_root, "Project");
        public AuthoringSession ProjectSession { get; }
        public static byte[] SourceGlb { get; } = CreateGlb("""{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[]}]}""");

        public Fixture()
        {
            Directory.CreateDirectory(Assets);
            File.WriteAllText(Path.Combine(ProjectRoot, "Project.oasisproj"), """{"name":"Project","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            File.WriteAllBytes(Path.Combine(Assets, "Source.glb"), SourceGlb);
            ProjectSession = new AuthoringSession(_root);
            ProjectSession.OpenProject(Path.Combine(ProjectRoot, "Project.oasisproj"));
        }

        public void OpenSecondProject()
        {
            var other = Path.Combine(_root, "Other");
            Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(other, "Other.oasisproj"), """{"name":"Other","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            ProjectSession.OpenProject(Path.Combine(other, "Other.oasisproj"));
        }

        public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch { } }
    }

    private static byte[] CreateGlb(string json)
    {
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
