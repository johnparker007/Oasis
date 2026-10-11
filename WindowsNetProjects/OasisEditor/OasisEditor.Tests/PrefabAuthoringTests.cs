using System.Text.Json;
using OasisEditor.Commands;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PrefabAuthoringTests
{
    [Fact]
    public void Schema2TreeRoundTripsStableIdsAndExplicitComponentsWithoutLegacyFields()
    {
        var source = Ball();
        var child = PrefabNode.Create("Group");
        source = PrefabAuthoringService.Add(source, source.Root.Id, child);
        var json = Object3DDocumentStorage.Serialize(source);
        using var parsed = JsonDocument.Parse(json);
        Assert.False(parsed.RootElement.TryGetProperty("model", out _));
        Assert.False(parsed.RootElement.TryGetProperty("physics", out _));
        Assert.Equal(2, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(Object3DDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(source.Id, result.Id);
        Assert.Equal(source.Root.Id, result.Root.Id);
        Assert.Equal(child.Id, Assert.Single(result.Root.Children).Id);
        Assert.Equal(source.Root.Components.Select(c => c.Id), result.Root.Components.Select(c => c.Id));
        Assert.Equal(json, Object3DDocumentStorage.Serialize(result));
        Assert.Single(result.Root.Children[0].Components);
    }

    [Fact]
    public void RenameReorderReparentUndoRedoPreserveIdentityAndCleanCheckpoint()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Ball"));
        var initial = Object3DDocumentStorage.Serialize(tab.GetObject3DDocument());
        var rootId = tab.PrefabNodeId;
        var rootComponentIds = tab.PrefabComponents.Select(c => c.Id).ToArray();
        tab.AddPrefabNode();
        var first = tab.PrefabNodeId;
        var firstTransform = Assert.Single(tab.PrefabComponents).Id;
        tab.PrefabNodeName = "Rack group";
        tab.SelectedPrefabChoice = tab.PrefabNodes.Single(n => n.Id == rootId);
        tab.AddPrefabNode();
        var second = tab.PrefabNodeId;
        tab.ReorderPrefabNode(-1);
        Assert.Equal(second, tab.GetObject3DDocument().Root.Children[0].Id);
        tab.PrefabParent = tab.PrefabNodes.Single(n => n.Id == first);
        Assert.Equal(second, Assert.Single(tab.GetObject3DDocument().Root.Children[0].Children).Id);
        Assert.Equal(firstTransform, tab.GetObject3DDocument().Root.Children[0].Components[0].Id);
        Assert.Equal(rootComponentIds, tab.GetObject3DDocument().Root.Components.Select(c => c.Id));
        var final = Object3DDocumentStorage.Serialize(tab.GetObject3DDocument());
        Assert.True(tab.IsDirty);
        while (tab.CommandService.CanUndo) Assert.True(tab.CommandService.TryUndo());
        Assert.Equal(initial, Object3DDocumentStorage.Serialize(tab.GetObject3DDocument()));
        Assert.False(tab.IsDirty);
        while (tab.CommandService.CanRedo) Assert.True(tab.CommandService.TryRedo());
        Assert.Equal(final, Object3DDocumentStorage.Serialize(tab.GetObject3DDocument()));
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void EditsAndSelectionAreDocumentScopedAndNoOpsAreNotRecorded()
    {
        var first = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("First"));
        var second = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Second"));
        first.PrefabPositionX = 0;
        first.PrefabNodeName = "First";
        first.Object3DColliderKind = Object3DColliderKind.None;
        first.HasPrefabModel = true;
        first.ReorderPrefabNode(-1);
        first.SelectedPrefabChoice = first.PrefabNodes[0];
        Assert.False(first.CommandService.CanUndo);
        Assert.False(first.IsDirty);
        var forbidden = new ForeignCommand(second.DocumentId);
        Assert.Throws<InvalidOperationException>(() => first.CommandService.Execute(forbidden));
        Assert.False(forbidden.WasExecuted);
        first.PrefabPositionX = 2;
        Assert.Equal(0, second.PrefabPositionX);
        Assert.False(second.IsDirty);
    }

    [Fact]
    public void InvalidInspectorEditReportsPropertyWithoutChangingDocumentOrHistory()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Ball"));
        var before = Object3DDocumentStorage.Serialize(tab.GetObject3DDocument());
        tab.PrefabScaleX = 0;
        Assert.Contains(tab.PrefabNodeId, tab.PrefabValidationMessage);
        Assert.Contains("scale", tab.PrefabValidationMessage);
        Assert.Equal(before, Object3DDocumentStorage.Serialize(tab.GetObject3DDocument()));
        Assert.False(tab.IsDirty);
        Assert.False(tab.CommandService.CanUndo);
        tab.PrefabScaleX = 1;
        Assert.Empty(tab.PrefabValidationMessage);
    }

    [Fact]
    public void ReparentCycleAndRootRemovalAreRejected()
    {
        var source = Object3DDocument.Create("Ball");
        var child = PrefabNode.Create("Child");
        var grandchild = PrefabNode.Create("Grandchild");
        source = PrefabAuthoringService.Add(source, source.Root.Id, child);
        source = PrefabAuthoringService.Add(source, child.Id, grandchild);
        var error = Assert.Throws<InvalidOperationException>(() => PrefabAuthoringService.Move(source, child.Id, grandchild.Id, 0));
        Assert.Contains(child.Id, error.Message);
        Assert.Contains("cycle", error.Message);
        Assert.Throws<InvalidOperationException>(() => PrefabAuthoringService.Remove(source, source.Root.Id));
        var children = new List<PrefabNode>();
        var cyclic = PrefabNode.Create("Cycle") with { Children = children };
        children.Add(cyclic);
        Assert.Contains("cycle", Assert.Throws<InvalidOperationException>(() => Object3DValidationService.Validate(source with { Root = cyclic })).Message);
    }

    [Fact]
    public void RejectsDuplicateIdsCardinalityUnknownKindsAndVersions()
    {
        var source = Ball();
        var transform = source.Root.Components[0];
        Reject(source with { Root = source.Root with { Components = [.. source.Root.Components, transform with { Id = Guid.NewGuid().ToString("D") }] } }, "at most one");
        Reject(source with { Root = source.Root with { Components = source.Root.Components.Skip(1).ToArray() } }, "Transform");
        Reject(source with { Root = source.Root with { Components = [transform with { Id = source.Root.Id }, .. source.Root.Components.Skip(1)] } }, "unique");
        Reject(source with { Root = source.Root with { Children = [PrefabNode.Create("Duplicate") with { Id = source.Root.Id }] } }, "unique");
        Reject(source with { Root = source.Root.SetComponent(transform with { ContractVersion = 2 }) }, "contractVersion");
        Reject(source with { Root = source.Root with { Components = [new(Guid.NewGuid().ToString("D"), (PrefabComponentKind)999), .. source.Root.Components] } }, "kind");
        var json = Object3DDocumentStorage.Serialize(source);
        foreach (var malformed in new[] { json.Replace("\"Transform\"", "\"Script\""), json.Replace("\"contractVersion\": 1", "\"contractVersion\": 999"), json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1"), json.Replace("\"upAxis\": \"Y\"", "\"upAxis\": \"Y\", \"unknown\": true") })
            Assert.False(Object3DDocumentStorage.TryRead(malformed, out _, out _));
    }

    [Fact]
    public void ValidatesPayloadSourceFramesBodyOwnershipAndBackendConstraints()
    {
        var group = PrefabNode.Create("Group").SetComponent(PrefabComponent.ForCollider(new(Object3DColliderKind.Box, [0,0,0], Size: [1,2,3]), PrefabColliderSourceFrame.NodeLocalSI));
        var source = Object3DDocument.Create("Group") with { Root = group };
        Object3DValidationService.Validate(source);
        Reject(source with { Root = group.SetComponent(group.Component(PrefabComponentKind.Collider)! with { SourceFrame = PrefabColliderSourceFrame.ModelLocal }) }, "sourceFrame");
        Reject(source with { Root = group.SetComponent(group.Components[0] with { Model = new("model.glb", 1, "Y") }) }, "payload");
        Reject(source with { Root = group.SetComponent(group.Component(PrefabComponentKind.Collider)! with { Collider = new(Object3DColliderKind.Box, [0,0,0], Size: [1,double.NaN,3]) }) }, "size");
        Reject(source with { Root = group.SetComponent(PrefabComponent.ForBody(new(true, 1, true)) with { Mode = PrefabBodyMode.Static }) }, "enabled");
        var ball = Ball();
        Reject(ball with { Root = ball.Root.SetComponent(ball.Root.Component(PrefabComponentKind.Rigidbody)! with { Rigidbody = new(true, double.PositiveInfinity, true) }) }, "mass");
        Reject(ball with { Root = ball.Root.SetComponent(ball.Root.Component(PrefabComponentKind.Collider)! with { Collider = new(Object3DColliderKind.Mesh) }) }, "dynamic Mesh");
        Reject(ball with { Root = ball.Root.SetComponent(ball.Root.Component(PrefabComponentKind.Collider)! with { IsTrigger = true }) }, "non-trigger");
        Reject(ball with { Root = ball.Root with { Children = [PrefabNode.Create("Nested").SetComponent(PrefabComponent.ForBody(new(false)))] } }, "nested");
        var parent = PrefabNode.Create("Scaled").SetComponent(PrefabComponent.ForTransform() with { Transform = MachineObjectTransform.Identity with { Scale = new(2,2,2) } });
        Reject(ball with { Root = parent with { Children = [ball.Root] } }, "scaled parent");
        Reject(ball with { Root = ball.Root.SetComponent(ball.Root.Component(PrefabComponentKind.Collider)! with { Collider = new(Object3DColliderKind.Capsule, [0,0,0], Radius: 1, Height: 1, Axis: Object3DCapsuleAxis.Y) }) }, "height");
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("Z")]
    public void FaithfulProjectionPreservesIntrinsicCorrectionAndPhysics(string axis)
    {
        var document = Ball() with { Model = new("ball.glb", .5, axis) };
        var runtime = Object3DRuntimeManifest.From(document, document.Id);
        Assert.Equal(1, runtime.SchemaVersion);
        Assert.Equal(.5, runtime.ModelScale);
        Assert.Equal(axis, runtime.UpAxis);
        Assert.Equal(.028575, runtime.Collider.Radius);
        Assert.Equal(new double[] {0,0,0}, runtime.Collider.Center);
        Assert.True(runtime.Rigidbody.Enabled);
        Assert.Equal(.17, runtime.Rigidbody.Mass);
        Assert.True(runtime.Rigidbody.UseGravity);
    }

    [Theory]
    [InlineData("children")]
    [InlineData("transform")]
    [InlineData("sourceFrame")]
    [InlineData("isTrigger")]
    [InlineData("mode")]
    [InlineData("Model")]
    public void SingleRootAloneDoesNotImplyRepresentability(string property)
    {
        var document = Object3DDocument.Create("Prop");
        document = property switch
        {
            "children" => PrefabAuthoringService.Add(document, document.Root.Id, PrefabNode.Create("Group")),
            "transform" => document with { Root = document.Root.SetComponent(document.Root.Components[0] with { Transform = MachineObjectTransform.Identity with { Position = new(1,0,0) } }) },
            "sourceFrame" => document with { Root = document.Root.SetComponent(PrefabComponent.ForCollider(new(Object3DColliderKind.Sphere, [0,0,0], Radius: 1), PrefabColliderSourceFrame.NodeLocalSI)) },
            "isTrigger" => document with { Root = document.Root.SetComponent(PrefabComponent.ForCollider(new(Object3DColliderKind.Sphere, [0,0,0], Radius: 1), PrefabColliderSourceFrame.ModelLocal) with { IsTrigger = true }) },
            "mode" => document with { Root = document.Root.SetComponent(PrefabComponent.ForBody(new(true, 1, true)) with { Mode = PrefabBodyMode.Kinematic }) },
            _ => document with { Root = PrefabNode.Create("Model-free") }
        };
        Object3DValidationService.Validate(document);
        var error = Assert.Throws<InvalidOperationException>(() => Object3DRuntimeManifest.From(document, document.Id));
        Assert.Contains(document.Id, error.Message);
        Assert.Contains(document.Root.Id, error.Message);
        Assert.Contains(property, error.Message);
    }

    [Fact]
    public void OpeningAnOlderDocumentDoesNotSilentlyCreateAReplacementAsset()
    {
        var json = Object3DDocumentStorage.Serialize(Ball()).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1");
        Assert.Throws<InvalidOperationException>(() => new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Ball"), object3DDocumentJson: json));
    }

    [Fact]
    public void OptionalComponentsAreUndoableAndTransformCannotBeRemoved()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Prop"));
        tab.HasPrefabCollider = true;
        var id = tab.PrefabComponents.Single(c => c.Kind == PrefabComponentKind.Collider).Id;
        tab.HasPrefabCollider = false;
        Assert.True(tab.CommandService.TryUndo());
        Assert.Equal(id, tab.PrefabComponents.Single(c => c.Kind == PrefabComponentKind.Collider).Id);
        Assert.True(tab.CommandService.TryRedo());
        Assert.False(tab.HasPrefabCollider);
        Assert.Throws<InvalidOperationException>(() => PrefabAuthoringService.Component(tab.GetObject3DDocument(), tab.PrefabNodeId, PrefabComponentKind.Transform, false));
    }

    private static Object3DDocument Ball() => Object3DDocument.Create("Pool Ball") with
    { Physics = new(new(Object3DColliderKind.Sphere, [0,0,0], Radius: .028575), new(true, .17, true)) };
    private static void Reject(Object3DDocument document, string property) => Assert.Contains(property, Assert.Throws<InvalidOperationException>(() => Object3DValidationService.Validate(document)).Message);
    private sealed class ForeignCommand(Guid documentId) : IDocumentCommand, IExecutionTrackedCommand
    {
        public Guid DocumentId => documentId;
        public string Description => "Wrong document";
        public bool WasExecuted { get; private set; }
        public void Execute() => WasExecuted = true;
        public void Undo() { }
    }
}
