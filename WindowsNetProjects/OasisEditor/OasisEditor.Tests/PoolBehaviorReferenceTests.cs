using System.IO;
using Oasis.Scripting;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PoolBehaviorReferenceTests
{
    [Fact]
    public void CanonicalScriptResolvesAgainstExactCompositionAndReportsEveryMissingDomain()
    {
        var result = OasisScriptCompiler.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Pool/behavior.oasis")), "Pool/behavior.oasis");
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var objects = Enumerable.Range(1, 15).Select(i => $"ball{i:00}").Append("cueBall").ToHashSet();
        var anchors = Enumerable.Range(1, 15).SelectMany(i => new[] { $"rackBall{i:00}", $"traySlot{i:00}" }).Append("rackCueBall").ToHashSet();
        var triggers = new HashSet<string> { "PocketLeftCorner", "PocketLeftMiddle", "PocketLeftFarCorner", "PocketRightCorner", "PocketRightMiddle", "PocketRightFarCorner" };
        var inputs = new HashSet<string> { "rerack", "newGame" };
        var empty = new HashSet<string>();
        var index = new OasisScriptMachineReferenceIndex(objects, anchors, triggers, inputs, empty, empty, empty, empty);
        Assert.Empty(OasisScriptMachineValidator.Validate(result.Program!, index));
        objects.Remove("ball15"); anchors.Remove("traySlot15"); triggers.Remove("PocketRightFarCorner"); inputs.Remove("newGame");
        var diagnostics = OasisScriptMachineValidator.Validate(result.Program!, index);
        Assert.Contains(diagnostics, d => d.Code == "OSM3001" && d.Message.Contains("ball15"));
        Assert.Contains(diagnostics, d => d.Code == "OSM3002" && d.Message.Contains("traySlot15"));
        Assert.Contains(diagnostics, d => d.Code == "OSM3003" && d.Message.Contains("PocketRightFarCorner"));
        Assert.Contains(diagnostics, d => d.Code == "OSM3004" && d.Message.Contains("newGame"));
    }
}
