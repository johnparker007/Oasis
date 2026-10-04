using Oasis.Scripting;
using OasisEditor.Automation;

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

    private static IReadOnlySet<string> Set(params string[] values) => values.ToHashSet(StringComparer.Ordinal);
}
