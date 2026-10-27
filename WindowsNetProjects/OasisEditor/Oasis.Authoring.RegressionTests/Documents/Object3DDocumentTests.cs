using Xunit;

namespace OasisEditor.Tests;

public sealed class Object3DDocumentTests
{
    [Fact]
    public void Version2_RoundTripsIdentityModelAndPoolBallPhysics()
    {
        var source = PoolBall();
        var json = Object3DDocumentStorage.Serialize(source);
        Assert.True(Object3DDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(2, result.SchemaVersion);
        Assert.Equal(source.Id, result.Id);
        Assert.Equal("Pool Ball", result.DisplayName);
        Assert.Equal(source.Model, result.Model);
        Assert.Equal(Object3DColliderKind.Sphere, result.Physics.Collider.Kind);
        Assert.Equal(.028575, result.Physics.Collider.Radius);
        Assert.Equal(source.Physics.Collider.Center, result.Physics.Collider.Center);
        Assert.True(result.Physics.Rigidbody.Enabled);
        Assert.Equal(.17, result.Physics.Rigidbody.Mass);
        Assert.True(result.Physics.Rigidbody.UseGravity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ReaderAcceptsOnlyCurrentSchema(int version)
    {
        var json = Object3DDocumentStorage.Serialize(PoolBall()).Replace("\"schemaVersion\": 2", $"\"schemaVersion\": {version}");
        Assert.False(Object3DDocumentStorage.TryRead(json, out _, out _));
    }

    [Fact]
    public void EveryColliderKindRoundTrips()
    {
        var colliders = new[]
        {
            Object3DColliderDefinition.None,
            new(Object3DColliderKind.Sphere, [0, 0, 0], Radius: 1),
            new(Object3DColliderKind.Box, [0, 0, 0], Size: [1, 2, 3]),
            new(Object3DColliderKind.Capsule, [0, 0, 0], Radius: .5, Height: 2, Axis: Object3DCapsuleAxis.Z),
            new(Object3DColliderKind.Mesh)
        };
        foreach (var collider in colliders)
        {
            var source = Object3DDocument.Create(collider.Kind.ToString()) with { Physics = new(collider, new(false)) };
            Assert.True(Object3DDocumentStorage.TryRead(Object3DDocumentStorage.Serialize(source), out var result, out var error), error);
            Assert.Equal(collider.Kind, result.Physics.Collider.Kind);
            Assert.Equal(collider.Radius, result.Physics.Collider.Radius);
            Assert.Equal(collider.Height, result.Physics.Collider.Height);
            Assert.Equal(collider.Axis, result.Physics.Collider.Axis);
            Assert.Equal(collider.Size, result.Physics.Collider.Size);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidEnabledRigidbodyMass(double mass)
    {
        var source = PoolBall() with { Physics = PoolBall().Physics with { Rigidbody = new(true, mass, true) } };
        Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(source));
    }

    [Fact]
    public void DisabledRigidbodyWithoutValuesIsValid()
    {
        var source = Object3DDocument.Create("Static Prop") with { Physics = new(new(Object3DColliderKind.Box, [0, 0, 0], Size: [1, 1, 1]), new(false)) };
        Assert.True(Object3DDocumentStorage.TryRead(Object3DDocumentStorage.Serialize(source), out _, out _));
    }

    [Theory]
    [InlineData("../ball.glb")]
    [InlineData("C:\\ball.glb")]
    [InlineData("ball.fbx")]
    [InlineData("")]
    public void RejectsMalformedModelPath(string path)
    {
        var source = PoolBall() with { Model = PoolBall().Model with { Path = path } };
        Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(source));
    }

    [Fact]
    public void RejectsInvalidColliderDimensions()
    {
        Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(PoolBall() with { Physics = new(new(Object3DColliderKind.Sphere, [0,0,0], Radius: 0), new(false)) }));
        Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(PoolBall() with { Physics = new(new(Object3DColliderKind.Box, [0,0,0], Size: [1,0,1]), new(false)) }));
        Assert.Throws<InvalidOperationException>(() => Object3DDocumentStorage.Serialize(PoolBall() with { Physics = new(new(Object3DColliderKind.Capsule, [0,0,0], Radius: 1, Height: 1, Axis: Object3DCapsuleAxis.Y), new(false)) }));
    }

private static Object3DDocument PoolBall() => Object3DDocument.Create("Pool Ball") with
    {
        Physics = new(new(Object3DColliderKind.Sphere, [0, 0, 0], Radius: .028575), new(true, .17, true))
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OasisObject3DTests", Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
