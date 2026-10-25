using System.Text.Json;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Object3DPrefabContractTests
{
    [Fact]
    public void Schema2_RoundTripsStableInlineNodeAndComponentIds()
    {
        var source = PoolBall();
        var child = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Grouping", [Object3DComponentDefinition.CreateTransform()], []);
        source = source.WithRoot(source.Root.WithChildren([child]));

        var json = Object3DDocumentStorage.Serialize(source);
        Assert.Contains("\"schemaVersion\": 2", json);
        Assert.True(Object3DDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(Object3DDocument.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Equal(source.Id, result.Id);
        Assert.Equal(source.Root.Id, result.Root.Id);
        Assert.Equal(source.Root.Components.Select(component => component.ComponentId), result.Root.Components.Select(component => component.ComponentId));
        Assert.Equal(child.Id, Assert.Single(result.Root.Children).Id);
        Assert.Equal(child.Components[0].ComponentId, result.Root.Children[0].Components[0].ComponentId);
        Assert.Equal(source.Model, result.Model);
    }

    [Fact]
    public void ReaderRejectsSchema1WithoutLegacyMigration()
    {
        var json = Object3DDocumentStorage.Serialize(PoolBall()).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal);
        Assert.False(Object3DDocumentStorage.TryRead(json, out _, out var error));
        Assert.Contains("Unsupported Object3D schema version", error);
    }

    [Fact]
    public void RejectsUnknownComponentAndUnknownJsonFields()
    {
        var doc = PoolBall();
        var unknown = new Object3DComponentDefinition(Guid.NewGuid().ToString("D"), "Script", 1);
        Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(doc.WithRoot(doc.Root.WithComponent(unknown))));

        var json = Object3DDocumentStorage.Serialize(doc).Replace("\"displayName\":", "\"unexpected\": 1,\n  \"displayName\":", StringComparison.Ordinal);
        Assert.False(Object3DDocumentStorage.TryRead(json, out _, out _));
    }

    [Fact]
    public void RejectsDuplicateIdsAndCyclesWithNodeContext()
    {
        var doc = PoolBall();
        var duplicate = doc.Root with { Id = doc.Root.Id, Name = "Child", Components = [Object3DComponentDefinition.CreateTransform()] };
        var withDuplicate = doc.WithRoot(doc.Root.WithChildren([duplicate]));
        var duplicateError = Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(withDuplicate));
        Assert.Contains("node 'Child'", duplicateError.Message);
        Assert.Contains("unique stable GUID", duplicateError.Message);

        var cycleChildren = new Object3DNodeDefinition[1];
        var cycle = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Loop", [Object3DComponentDefinition.CreateTransform()], cycleChildren);
        cycleChildren[0] = cycle;
        var cyclicDoc = doc.WithRoot(doc.Root.WithChildren([cycle]));
        Assert.Contains("cycle", Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(cyclicDoc)).Message);
    }

    [Fact]
    public void RejectsMultipleBodiesInOneSubtreeAndPreservesDynamicMeshCapability()
    {
        var root = PoolBall().Root;
        var child = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "SecondBody", [
            Object3DComponentDefinition.CreateTransform(),
            Object3DComponentDefinition.CreateRigidbody(new(Object3DRigidbodyMode.Dynamic, .2, true))
        ], []);
        var withTwoBodies = PoolBall() with { Root = root.WithChildren([child]) };
        Assert.Contains("one Rigidbody", Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(withTwoBodies)).Message);

        var dynamicMesh = Object3DDocument.ReauthorRoot("Dynamic Mesh", new("mesh.glb", 1, "Y"),
            new(new(Object3DColliderKind.Mesh), new(Object3DRigidbodyMode.Dynamic, .5, true)));
        Assert.True(Object3DDocumentStorage.TryRead(Object3DDocumentStorage.Serialize(dynamicMesh), out var loaded, out var error), error);
        Assert.True(loaded.TryProjectSingleRoot(out _, out _, out var projectionError), projectionError);
    }

    [Fact]
    public void StaticGroupingDefaultsDoNotClaimChildBodyOwnership()
    {
        var document = Object3DDocument.Create("Group");
        var child = PoolBall().Root;
        document = document with { Root = document.Root.WithChildren([child]) };
        Assert.True(Object3DDocumentStorage.TryRead(Object3DDocumentStorage.Serialize(document), out _, out var error), error);
        Assert.False(document.TryProjectSingleRoot(out _, out _, out var projectionError));
        Assert.Contains("child nodes", projectionError);
    }

    [Fact]
    public void RuntimeProjectionRejectsChildrenAndKinematicOrTriggerStructure()
    {
        var document = PoolBall();
        Assert.True(document.TryProjectSingleRoot(out var model, out var physics, out var error), error);
        Assert.Equal(document.Model, model);
        Assert.Equal(document.Physics, physics);

        var child = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Child", [Object3DComponentDefinition.CreateTransform()], []);
        Assert.False((document with { Root = document.Root.WithChildren([child]) }).TryProjectSingleRoot(out _, out _, out var childError));
        Assert.Contains("child nodes", childError);

        var kinematic = document with { Physics = new(Object3DColliderDefinition.None, new(Object3DRigidbodyMode.Kinematic)) };
        Assert.False(kinematic.TryProjectSingleRoot(out _, out _, out var bodyError));
        Assert.Contains("kinematic", bodyError);

        var trigger = document with { Physics = new(new(Object3DColliderKind.Sphere, [0, 0, 0], Radius: .1, IsTrigger: true), new(Object3DRigidbodyMode.Static)) };
        Assert.False(trigger.TryProjectSingleRoot(out _, out _, out var triggerError));
        Assert.Contains("trigger", triggerError);
    }

    [Fact]
    public void TransformProjectionEditsExistingTransformComponentWithoutPersistingShadowState()
    {
        var document = PoolBall();
        var transformId = document.Root.Components.Single(component => component.Kind == Object3DComponentDefinition.TransformKind).ComponentId;
        var moved = document.Root with { Transform = new(new(1, 2, 3), MachineVector3.Zero, MachineVector3.One) };
        Assert.Equal(transformId, moved.Components.Single(component => component.Kind == Object3DComponentDefinition.TransformKind).ComponentId);
        var json = Object3DDocumentStorage.Serialize(document with { Root = moved });
        using var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.GetProperty("root").GetProperty("components")[0].TryGetProperty("transform", out _));
        Assert.False(parsed.RootElement.GetProperty("root").TryGetProperty("transform", out _));
    }

    [Fact]
    public void ComputedLegacyProjectionsPreserveTypedComponentIdsOnEdit()
    {
        var document = PoolBall();
        var modelId = document.Root.Components.Single(component => component.Kind == Object3DComponentDefinition.ModelKind).ComponentId;
        var colliderId = document.Root.Components.Single(component => component.Kind == Object3DComponentDefinition.ColliderKind).ComponentId;
        var bodyId = document.Root.Components.Single(component => component.Kind == Object3DComponentDefinition.RigidbodyKind).ComponentId;

        var changed = document with
        {
            Model = document.Model with { Scale = 2 },
            Physics = new(new(Object3DColliderKind.Sphere, [0, 0, 0], Radius: .03), new(Object3DRigidbodyMode.Dynamic, .2, true))
        };

        Assert.Equal(modelId, changed.Root.Components.Single(component => component.Kind == Object3DComponentDefinition.ModelKind).ComponentId);
        Assert.Equal(colliderId, changed.Root.Components.Single(component => component.Kind == Object3DComponentDefinition.ColliderKind).ComponentId);
        Assert.Equal(bodyId, changed.Root.Components.Single(component => component.Kind == Object3DComponentDefinition.RigidbodyKind).ComponentId);
    }

    private static Object3DDocument PoolBall() => Object3DDocument.ReauthorRoot(
        "Pool Ball", new("model.glb", 1, "Y"),
        new(new(Object3DColliderKind.Sphere, [0, 0, 0], Radius: .028575), new(Object3DRigidbodyMode.Dynamic, .17, true)));
}
