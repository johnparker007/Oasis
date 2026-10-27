using Xunit;
using System.Text.Json;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Tests;

public sealed class MachineDocumentTests
{
    private static readonly FruitMachinePlatformType[] SupportedPlatforms =
    [
        FruitMachinePlatformType.None,
        FruitMachinePlatformType.Impact,
        FruitMachinePlatformType.MPU5,
        FruitMachinePlatformType.Epoch,
        FruitMachinePlatformType.MPU3,
        FruitMachinePlatformType.MaygayM1,
        FruitMachinePlatformType.Scorpion4
    ];

    [Fact]
    public void EmulationRuntimePlatforms_ContainsExactlyImplementedBackends()
    {
        Assert.Equal(SupportedPlatforms, EmulationRuntimePlatforms.Supported);
        Assert.All(SupportedPlatforms, platform => Assert.True(EmulationRuntimePlatforms.IsSupported(platform)));
        Assert.False(EmulationRuntimePlatforms.IsSupported(FruitMachinePlatformType.MPU4));
        Assert.False(EmulationRuntimePlatforms.IsSupported((FruitMachinePlatformType)9999));
    }

    [Fact]
    public void Schema11_RoundTripsCompositionRuntimeInputsAndDefaultInteraction()
    {
        var machine = MachineDocument.Create("Machine A") with
        {
            CabinetAsset = AssetReference.Project("Assets/Cabinet3D/Vogue/asset.cabinet3d"),
            SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/Top/asset.face")],
            ReelAssignments = [new(MachineObjectReference.Reel(0), AssetReference.Project("Assets/Reels/Standard/asset.reel"))],
            Runtime = new EmulationRuntimeDefinition(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "Assets/ROMs/game.bin" })
        };
        var json = MachineDocumentStorage.Serialize(machine);
        using (var authored = JsonDocument.Parse(json))
        {
            Assert.Equal(11, authored.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("Buttons", authored.RootElement.GetProperty("interaction").GetProperty("kind").GetString());
            Assert.Empty(authored.RootElement.GetProperty("objectInstances").EnumerateArray());
            Assert.Equal("Emulation", authored.RootElement.GetProperty("runtime").GetProperty("kind").GetString());
            Assert.False(authored.RootElement.GetProperty("runtime").TryGetProperty("$type", out _));
        }
        Assert.True(MachineDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(machine.Id, result.Id);
        Assert.Equal(FruitMachinePlatformType.MPU5, result.EmulationRuntime.Platform);
        Assert.Equal("Assets/ROMs/game.bin", result.EmulationRuntime.PlatformSettingsAs<Mpu5NativeRomSettings>().ProgramRom1Path);
        Assert.Single(result.SurfaceAssignments);
        Assert.Single(result.ReelAssignments);
    }

    [Fact]
    public void WrongSchema_IsRejectedWithoutCompatibilityFallback()
    {
        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Machine")).Replace("\"schemaVersion\": 11", "\"schemaVersion\": 7");
        Assert.False(MachineDocumentStorage.TryRead(json, out _, out var error));
        Assert.Contains("only version 11", error);
    }

    [Fact]
    public void Anchors_RoundTripIndependentMachineSpaceTransforms()
    {
        var anchors = new[]
        {
            new MachineAnchor("rackCueBall", "Rack Cue Ball", new(1, 2, 3), new(0, 90, 0)),
            new MachineAnchor("rackBall01", "Rack Ball 1", new(.1, .2, .3), new(10, 20, 30)),
            new MachineAnchor("rackBall08", "Rack Ball 8", new(.4, .5, .6), new(40, 50, 60)),
            new MachineAnchor("traySlot01", "Tray Slot 1", new(7, 8, 9), new(70, 80, 90))
        };
        var source = MachineDocument.Create("Pool") with { Anchors = anchors };
        Assert.True(MachineDocumentStorage.TryRead(MachineDocumentStorage.Serialize(source), out var result, out var error), error);
        Assert.Equal(anchors, result.Anchors);
        Assert.Equal(MachineVector3.Zero, MachineAnchor.Create("anchor1").Position);
        Assert.Equal(MachineVector3.Zero, MachineAnchor.Create("anchor1").Rotation);
    }

    [Fact]
    public void Schema11_RoundTripsPrefabDependenciesAndGeometrySurfaces()
    {
        var source = MachineDocument.Create("Pool") with
        {
            PrefabDependencies = [new("poolBall", AssetReference.Project("Assets/Object3D/PoolBall/asset.object3d"))],
            GeometrySurfaces = [new("playingSurface", new(1, 2, 3), new(0, 90, 0), 1.2, 2.4)]
        };

        var json = MachineDocumentStorage.Serialize(source);
        Assert.True(MachineDocumentStorage.TryRead(json, out var reopened, out var error), error);
        Assert.Equal(source.PrefabDependencies, reopened.PrefabDependencies);
        Assert.Equal(source.GeometrySurfaces, reopened.GeometrySurfaces);

        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(source with
        {
            PrefabDependencies = [new("poolBall", AssetReference.Project("Assets/Object3D/A/asset.object3d")), new("poolBall", AssetReference.Library("Object3D/B/asset.object3d"))]
        }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(source with
        {
            GeometrySurfaces = [new("playingSurface", new(0, 0, 0), new(0, 0, 0), 0, 1)]
        }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(source with
        {
            GeometrySurfaces = [new("playingSurface", new(double.NaN, 0, 0), new(0, 0, 0), 1, 1)]
        }));
    }

    [Fact]
    public void PoolInteraction_RoundTripsAndRequiresDeclaredPrefabAndSurface()
    {
        var machine = MachineDocument.Create("Pool") with
        {
            PrefabDependencies = [new("cueBall", AssetReference.Project("Assets/Object3D/PoolBall/asset.object3d"))],
            GeometrySurfaces = [new("playingSurface", MachineVector3.Zero, MachineVector3.Zero, 1, 2)],
            Interaction = new MachineInteractionDefinition("Pool", "cueBall", "playingSurface")
        };
        var json = MachineDocumentStorage.Serialize(machine);
        using var parsed = JsonDocument.Parse(json);
        var interaction = parsed.RootElement.GetProperty("interaction");
        Assert.Equal("Pool", interaction.GetProperty("kind").GetString());
        Assert.Equal("cueBall", interaction.GetProperty("cuePrefabAlias").GetString());
        Assert.Equal("playingSurface", interaction.GetProperty("playingSurfaceId").GetString());
        Assert.True(MachineDocumentStorage.TryRead(json, out var reopened, out var error), error);
        Assert.Equal(machine.Interaction, reopened.Interaction);

        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(machine with { Interaction = new("Pool", "missing", "playingSurface") }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(machine with { Interaction = new("Pool", "cueBall", "missing") }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(machine with { Interaction = new("Unknown") }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(machine with { Interaction = new("Buttons", "cueBall", null) }));
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"interaction\": {", "\"unused\": {"), out _, out error));
        Assert.Contains("Interaction object is required", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad id")]
    [InlineData("a:b")]
    public void InvalidAnchorIds_AreRejected(string id)
    {
        var source = MachineDocument.Create("Machine") with { Anchors = [MachineAnchor.Create(id)] };
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(source));
        Assert.True(MachineObjectReference.Anchor(id).IsEmpty);
    }

    [Fact]
    public void AnchorReferences_AreCanonicalAndObjectNamespaceMayCoexist()
    {
        Assert.True(MachineObjectReference.TryParse("anchor:rackBall08", out var reference));
        Assert.Equal(MachineObjectKind.Anchor, reference.Kind);
        Assert.Equal("anchor:rackBall08", reference.ToString());
        Assert.False(MachineObjectReference.TryParse("anchor:a:b", out _));
        var machine = MachineDocument.Create("Machine") with
        {
            Anchors = [MachineAnchor.Create("same")],
            ObjectInstances = [new("same", "Same", AssetReference.Project("Assets/Object3D/Same/asset.object3d"), MachineObjectTransform.Identity)]
        };
        Assert.Contains("\"anchors\"", MachineDocumentStorage.Serialize(machine));
    }

    [Fact]
    public void UnknownRuntimeKind_IsRejectedWithoutEmulationFallback()
    {
        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Machine"))
            .Replace("\"kind\": \"Emulation\"", "\"kind\": \"Scripted\"");
        Assert.False(MachineDocumentStorage.TryRead(json, out _, out var error));
        Assert.Contains("Unsupported Machine runtime kind", error);
    }

    [Fact]
    public void UnsupportedDefinedPlatform_IsRejectedByFactoryValidationSerializationAndReader()
    {
        Assert.Throws<NotSupportedException>(() => EmulationRuntimeDefinition.Create(FruitMachinePlatformType.MPU4));
        var runtime = new EmulationRuntimeDefinition(FruitMachinePlatformType.MPU4, new System6NativeRomSettings());
        var validation = Assert.Throws<InvalidOperationException>(() => RuntimeDefinitionValidation.Validate("Test", runtime));
        Assert.Contains("Test", validation.Message);
        Assert.Contains("Emulation", validation.Message);
        Assert.Contains("MPU4", validation.Message);
        Assert.Contains("unsupported", validation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(MachineDocument.Create("Test") with { Runtime = runtime }));

        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Test"))
            .Replace("\"platform\": \"None\"", "\"platform\": \"MPU4\"");
        Assert.False(MachineDocumentStorage.TryRead(json, out _, out var error));
        Assert.Contains("MPU4", error);
        Assert.Contains("unsupported", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidNumericPlatform_IsRejected()
    {
        var platform = (FruitMachinePlatformType)9999;
        Assert.Throws<NotSupportedException>(() => EmulationRuntimeDefinition.Create(platform));
        var runtime = new EmulationRuntimeDefinition(platform, new System6NativeRomSettings());
        Assert.Throws<InvalidOperationException>(() => RuntimeDefinitionValidation.Validate("Test", runtime));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(MachineDocument.Create("Test") with { Runtime = runtime }));
        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Test"))
            .Replace("\"platform\": \"None\"", "\"platform\": \"9999\"");
        Assert.False(MachineDocumentStorage.TryRead(json, out _, out var error));
        Assert.Contains("unsupported", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(FruitMachinePlatformType.None, typeof(System6NativeRomSettings))]
    [InlineData(FruitMachinePlatformType.Impact, typeof(System6NativeRomSettings))]
    [InlineData(FruitMachinePlatformType.MPU5, typeof(Mpu5NativeRomSettings))]
    [InlineData(FruitMachinePlatformType.Epoch, typeof(EpochNativeRomSettings))]
    [InlineData(FruitMachinePlatformType.MPU3, typeof(Mpu3ProjectSettings))]
    [InlineData(FruitMachinePlatformType.MaygayM1, typeof(M1ProjectSettings))]
    [InlineData(FruitMachinePlatformType.Scorpion4, typeof(Scorpion4ProjectSettings))]
    public void EmulationRuntime_RoundTripsSupportedPlatformSettings(FruitMachinePlatformType platform, Type settingsType)
    {
        var machine = MachineDocument.Create("Machine") with { Runtime = EmulationRuntimeDefinition.Create(platform) };
        Assert.True(MachineDocumentStorage.TryRead(MachineDocumentStorage.Serialize(machine), out var reopened, out var error), error);
        Assert.IsType<EmulationRuntimeDefinition>(reopened.Runtime);
        Assert.Equal(platform, reopened.EmulationRuntime.Platform);
        Assert.Equal(settingsType, reopened.EmulationRuntime.PlatformSettings.GetType());
    }

}
