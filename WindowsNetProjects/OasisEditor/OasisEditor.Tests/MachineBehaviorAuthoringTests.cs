using Oasis.Scripting;
using OasisEditor.Automation;
using OasisEditor.Features.CabinetEditor.Models;
using System.Text;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineBehaviorAuthoringTests
{
    [Fact]
    public void OptionalBehavior_RoundTripsCanonicalShape_AndRejectsOtherPaths()
    {
        var without = MachineDocument.Create("Machine");
        var withoutJson = MachineDocumentStorage.Serialize(without);
        Assert.DoesNotContain("\"behavior\"", withoutJson);
        Assert.True(MachineDocumentStorage.TryRead(withoutJson, out var reopenedWithout, out var error), error);
        Assert.Null(reopenedWithout.Behavior);

        var with = without with { Behavior = MachineBehaviorDefinition.OasisScript() };
        var json = MachineDocumentStorage.Serialize(with);
        Assert.Contains("\"kind\": \"OasisScript\"", json);
        Assert.Contains("\"source\": \"behavior.oasis\"", json);
        Assert.True(MachineDocumentStorage.TryRead(json, out var reopened, out error), error);
        Assert.Equal(MachineBehaviorDefinition.OasisScript(), reopened.Behavior);

        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(with with { Behavior = new("OasisScript", "scripts/game.oasis") }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(with with { Behavior = new("OasisScript", Path.GetFullPath("behavior.oasis")) }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(with with { Behavior = new("Lua", "behavior.oasis") }));
    }

    [Fact]
    public void MachineValidator_VisitsEveryReferenceLiteral_AndIgnoresBindings()
    {
        const string source = """
            const objects = [object:ball08, object:missing];
            state home = anchor:missing;
            const lampRef = lamp:99;
            const reelRef = reel:2;
            const alphaRef = alpha:3;
            const sevenRef = sevenSegment:4;
            on trigger.entered(trigger:missing, ball) { object.teleport(ball, anchor:rack); }
            on input.pressed(input:missing) { object.reset(object:ball08); }
            """;
        var compilation = OasisScriptCompiler.Compile(source, "behavior.oasis");
        Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Diagnostics));
        var index = new OasisScriptMachineReferenceIndex(
            Set("ball08"), Set("rack"), Set("pocket"), Set("rerack"), Set("1"), Set("1"), Set("1"), Set("1"));

        var diagnostics = OasisScriptMachineValidator.Validate(compilation.Program!, index);

        Assert.Equal(["OSM3001", "OSM3002", "OSM3005", "OSM3006", "OSM3007", "OSM3008", "OSM3003", "OSM3004"], diagnostics.Select(value => value.Code));
        Assert.DoesNotContain(diagnostics, value => value.Message.Contains("'ball'", StringComparison.Ordinal));
        Assert.All(diagnostics, value => Assert.True(value.Line > 0 && value.Column > 0));
    }

    [Fact]
    public void AddEditSaveAsRemove_PreservesSourceAndDocumentLifecycle()
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-behavior-" + Guid.NewGuid().ToString("N"));
        try
        {
            var original = Path.Combine(root, "Original", "asset.machine");
            var copy = Path.Combine(root, "Copy", "asset.machine");
            var tab = new DocumentTabViewModel(EditorDocument.CreateMachineStub("Machine"));
            tab.AddMachineBehaviorCommand.Execute(null);
            Assert.True(tab.IsDirty);
            Assert.True(tab.HasMachineBehavior);
            Assert.Contains("machine.started", tab.MachineBehaviorSource);
            Assert.True(tab.CommandService.TryUndo());
            Assert.False(tab.HasMachineBehavior);
            Assert.True(tab.CommandService.TryRedo());
            Assert.True(tab.HasMachineBehavior);
            Assert.Contains("machine.started", tab.MachineBehaviorSource);
            tab.MachineBehaviorSource = "on machine.started()\n{\n}\n";
            new DocumentSaveService().SaveDocument(tab, original).ApplyTo(tab);
            Assert.False(tab.IsDirty);
            Assert.Equal(tab.MachineBehaviorSource, File.ReadAllText(Path.Combine(root, "Original", "behavior.oasis")));

            tab.MachineBehaviorSource = "on machine.started() { timer.stop(\"save-as\"); }";
            new DocumentSaveService().SaveDocument(tab, copy).ApplyTo(tab);
            Assert.DoesNotContain("save-as", File.ReadAllText(Path.Combine(root, "Original", "behavior.oasis")));
            Assert.Contains("save-as", File.ReadAllText(Path.Combine(root, "Copy", "behavior.oasis")));

            tab.RemoveMachineBehaviorCommand.Execute(null);
            new DocumentSaveService().SaveDocument(tab, copy).ApplyTo(tab);
            Assert.False(File.Exists(Path.Combine(root, "Copy", "behavior.oasis")));
            Assert.False(tab.IsDirty);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void InvalidSourceRemainsInBufferAndReportsExactLocation()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateMachineStub("Machine"));
        tab.AddMachineBehaviorCommand.Execute(null);
        tab.MachineBehaviorSource = "on machine.started() { object.reset(1); }";
        Assert.Equal("on machine.started() { object.reset(1); }", tab.MachineBehaviorSource);
        var diagnostic = Assert.Single(tab.MachineBehaviorDiagnostics);
        Assert.Equal("OS2303", diagnostic.Code);
        Assert.Equal(1, diagnostic.Line);
        Assert.True(diagnostic.Column > 1);
    }

    [Fact]
    public void DeclaredMissingSidecar_IsDiagnosedAndCannotBeSilentlySaved()
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-missing-behavior-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var manifest = Path.Combine(root, "asset.machine");
            var machine = MachineDocument.Create("Machine") with { Behavior = MachineBehaviorDefinition.OasisScript() };
            File.WriteAllText(manifest, MachineDocumentStorage.Serialize(machine));
            var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(manifest, "Machine"), machineDocumentJson: File.ReadAllText(manifest));

            var diagnostic = Assert.Single(tab.MachineBehaviorDiagnostics);
            Assert.Equal("OSM3100", diagnostic.Code);
            Assert.Contains("missing", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(tab.IsMachineBehaviorSourceMissing);
            Assert.Throws<InvalidOperationException>(() => new DocumentSaveService().SaveDocument(tab, manifest));
            Assert.False(File.Exists(Path.Combine(root, "behavior.oasis")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ExistingSidecar_OpensNormally_AndCompositionMutationRefreshesDiagnostics()
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-existing-behavior-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var manifest = Path.Combine(root, "asset.machine");
            var machine = MachineDocument.Create("Machine") with
            {
                Behavior = MachineBehaviorDefinition.OasisScript(),
                ObjectInstances = [new("ball", "Ball", AssetReference.Project("Assets/Object3D/Ball/asset.object3d"), MachineObjectTransform.Identity)]
            };
            File.WriteAllText(manifest, MachineDocumentStorage.Serialize(machine));
            File.WriteAllText(Path.Combine(root, "behavior.oasis"), "on machine.started() { object.reset(object:ball); }");
            var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(manifest, "Machine"), machineDocumentJson: File.ReadAllText(manifest));
            Assert.Empty(tab.MachineBehaviorDiagnostics);

            tab.RemoveMachineObjectInstance("ball");
            Assert.Contains(tab.MachineBehaviorDiagnostics, value => value.Code == "OSM3001");
            Assert.True(tab.CommandService.TryUndo());
            Assert.Empty(tab.MachineBehaviorDiagnostics);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CompositionBuilder_UsesOnlyAssignedFacesAndCanonicalMachineReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-reference-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = Project(root);
            WriteFace(root, "Assigned", MachineObjectReference.Lamp(17), MachineObjectReference.AlphaDisplay(0), MachineObjectReference.SevenSegmentDisplay(12));
            WriteFace(root, "Unassigned", MachineObjectReference.Lamp(99));
            var machine = MachineDocument.Create("Machine") with
            {
                SurfaceAssignments = [new("screen", "Assets/Faces/Assigned/asset.face")],
                ObjectInstances = [new("ball", "Ball", AssetReference.Project("Assets/Object3D/Ball/asset.object3d"), MachineObjectTransform.Identity)],
                Anchors = [MachineAnchor.Create("rack")],
                InputDefinitions = [new() { Id = "rerack" }],
                ReelAssignments = [new(MachineObjectReference.Reel(2), AssetReference.Project("Assets/Reels/Reel/asset.reel"))]
            };

            var result = new OasisScriptMachineReferenceIndexBuilder().Build(project, string.Empty, machine);

            Assert.Empty(result.Diagnostics);
            Assert.Contains("ball", result.References.Objects);
            Assert.Contains("rack", result.References.Anchors);
            Assert.Contains("rerack", result.References.Inputs);
            Assert.Contains("2", result.References.Reels);
            Assert.Contains("17", result.References.Lamps);
            Assert.DoesNotContain("99", result.References.Lamps);
            Assert.Contains("0", result.References.AlphaDisplays);
            Assert.Contains("12", result.References.SevenSegmentDisplays);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ChangingFaceAssignment_RevalidatesCurrentSourceWithoutEditingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-face-refresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = Project(root);
            WriteFace(root, "Lamp17", MachineObjectReference.Lamp(17));
            WriteFace(root, "Lamp18", MachineObjectReference.Lamp(18));
            var machine = MachineDocument.Create("Machine") with
            {
                Behavior = MachineBehaviorDefinition.OasisScript(),
                SurfaceAssignments = [new("screen", "Assets/Faces/Lamp17/asset.face")]
            };
            var tab = new DocumentTabViewModel(EditorDocument.CreateMachineStub("Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
            tab.SetProjectAccessor(() => project);
            tab.MachineBehaviorSource = "const target = lamp:17; on machine.started() { }";
            Assert.Empty(tab.MachineBehaviorDiagnostics);

            tab.SetMachineSurfaceAssignment("screen", "Assets/Faces/Lamp18/asset.face");

            Assert.Contains(tab.MachineBehaviorDiagnostics, value => value.Code == "OSM3005" && value.Message.Contains("'17'", StringComparison.Ordinal));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void BrokenComposition_ProducesResolutionDiagnosticInsteadOfUnknownReferenceOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-broken-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = new OasisScriptMachineReferenceIndexBuilder().Build(Project(root), string.Empty,
                MachineDocument.Create("Machine") with { CabinetAsset = AssetReference.Project("Assets/Cabinet3D/Missing/asset.cabinet3d") });
            Assert.Contains(result.Diagnostics, value => value.Code == "OSM3101" && value.Message.Contains("Cabinet", StringComparison.Ordinal));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(AssetReferenceScope.Project)]
    [InlineData(AssetReferenceScope.Library)]
    public void CompositionBuilder_DiscoversCabinetTriggersWithCanonicalNodePrecedence(AssetReferenceScope scope)
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-trigger-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = Project(Path.Combine(root, "Project"));
            var library = Path.Combine(root, "Library");
            var assetRoot = scope == AssetReferenceScope.Project ? project.ProjectDirectory : library;
            var relative = "Assets/Cabinet3D/Cabinet/asset.cabinet3d";
            var manifest = Path.Combine(assetRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            File.WriteAllText(manifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
            WriteTriggerGlb(Path.Combine(Path.GetDirectoryName(manifest)!, "cabinet.glb"));
            var reference = new AssetReference(scope, relative);

            var result = new OasisScriptMachineReferenceIndexBuilder().Build(project, library,
                MachineDocument.Create("Machine") with { CabinetAsset = reference });

            Assert.Empty(result.Diagnostics);
            Assert.Contains("NodeWins", result.References.Triggers);
            Assert.Contains("MeshWins", result.References.Triggers);
            Assert.DoesNotContain("MeshLoses", result.References.Triggers);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static EditorProject Project(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        return new() { Name = "Test", ProjectDirectory = root, ProjectFilePath = Path.Combine(root, "test.oasisproject"), AssetsDirectory = Path.Combine(root, "Assets"), GeneratedDirectory = Path.Combine(root, "Generated") };
    }

    private static void WriteFace(string root, string name, params MachineObjectReference[] references)
    {
        var path = Path.Combine(root, "Assets", "Faces", name, "asset.face");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var elements = references.Select((reference, index) => reference.Kind switch
        {
            MachineObjectKind.Lamp => (FaceElementModel)new FaceLampWindowElement { ObjectId = $"element{index}", Name = $"Element {index}", Width = 1, Height = 1, LinkedMachineObjectReference = reference },
            MachineObjectKind.AlphaDisplay => new FaceAlphaDisplayElement { ObjectId = $"element{index}", Name = $"Element {index}", Width = 1, Height = 1, LinkedMachineObjectReference = reference },
            MachineObjectKind.SevenSegmentDisplay => new FaceSevenSegmentDisplayElement { ObjectId = $"element{index}", Name = $"Element {index}", Width = 1, Height = 1, LinkedMachineObjectReference = reference },
            _ => throw new ArgumentOutOfRangeException(nameof(references))
        }).ToArray();
        File.WriteAllText(path, FaceDocumentStorage.Serialize(new FaceDocumentModel { Id = name, Title = name, Elements = elements }));
    }

    private static void WriteTriggerGlb(string path)
    {
        var binary = new byte[44];
        Buffer.BlockCopy(new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }, 0, binary, 0, 36);
        Buffer.BlockCopy(new ushort[] { 0, 1, 2 }, 0, binary, 36, 6);
        const string primitive = "{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}";
        static string Mesh(string name, string body) => "{\"name\":\"" + name + "\"," + body[1..];
        var json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0,1,2]}]," +
                   "\"nodes\":[{\"name\":\"OasisTrigger_NodeWins\",\"mesh\":0},{\"name\":\"Ordinary\",\"mesh\":1},{\"name\":\"OasisCollider_Node\",\"mesh\":2}]," +
                   "\"meshes\":[" + Mesh("OasisTrigger_MeshLoses", primitive) + "," + Mesh("OasisTrigger_MeshWins", primitive) + "," + Mesh("OasisTrigger_MeshLoses", primitive) + "]," +
                   "\"buffers\":[{\"byteLength\":44}],\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36},{\"buffer\":0,\"byteOffset\":36,\"byteLength\":6}]," +
                   "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}]}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var padded = (jsonBytes.Length + 3) & ~3;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(0x46546C67); writer.Write(2); writer.Write(12 + 8 + padded + 8 + binary.Length);
        writer.Write(padded); writer.Write(0x4E4F534A); writer.Write(jsonBytes); writer.Write(Enumerable.Repeat((byte)0x20, padded - jsonBytes.Length).ToArray());
        writer.Write(binary.Length); writer.Write(0x004E4942); writer.Write(binary);
    }

    private static IReadOnlySet<string> Set(params string[] values) => values.ToHashSet(StringComparer.Ordinal);
}
