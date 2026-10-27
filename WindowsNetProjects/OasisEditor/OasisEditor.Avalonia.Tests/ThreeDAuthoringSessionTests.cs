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
        Assert.Equal("original model", File.ReadAllText(Path.Combine(fixture.Assets, "Source.glb")));
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
        fixture.OpenSecondProject();
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

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Oasis3DAuthoring-" + Guid.NewGuid().ToString("N"));
        public string Assets => Path.Combine(ProjectRoot, "Assets");
        public string ProjectRoot => Path.Combine(_root, "Project");
        public AuthoringSession ProjectSession { get; }

        public Fixture()
        {
            Directory.CreateDirectory(Assets);
            File.WriteAllText(Path.Combine(ProjectRoot, "Project.oasisproj"), """{"name":"Project","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            File.WriteAllText(Path.Combine(Assets, "Source.glb"), "original model");
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
}
