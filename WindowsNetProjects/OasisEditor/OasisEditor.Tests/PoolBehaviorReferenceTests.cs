using System.IO;
using Oasis.Scripting;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PoolBehaviorReferenceTests
{
    [Fact]
    public void CanonicalScriptResolvesAgainstSingleBallCompositionAndReportsMissingDomains()
    {
        var result = OasisScriptCompiler.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Pool/behavior.oasis")), "Pool/behavior.oasis");
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var objects = new HashSet<string> { "ball01" };
        var anchors = new HashSet<string> { "rackBall01", "traySlot01" };
        var triggers = new HashSet<string> { "Pocket_XNeg_Middle", "Pocket_XNeg_YNeg", "Pocket_XNeg_YPos", "Pocket_XPos_Middle", "Pocket_XPos_YNeg", "Pocket_XPos_YPos" };
        var inputs = new HashSet<string> { "rerack", "newGame" };
        var empty = new HashSet<string>();
        var index = new OasisScriptMachineReferenceIndex(objects, anchors, triggers, inputs, empty, empty, empty, empty);
        Assert.Empty(OasisScriptMachineValidator.Validate(result.Program!, index));

        objects.Remove("ball01"); anchors.Remove("traySlot01"); triggers.Remove("Pocket_XPos_YPos"); inputs.Remove("newGame");
        var diagnostics = OasisScriptMachineValidator.Validate(result.Program!, index);
        Assert.Contains(diagnostics, d => d.Code == "OSM3001" && d.Message.Contains("ball01"));
        Assert.Contains(diagnostics, d => d.Code == "OSM3002" && d.Message.Contains("traySlot01"));
        Assert.Contains(diagnostics, d => d.Code == "OSM3003" && d.Message.Contains("Pocket_XPos_YPos"));
        Assert.Contains(diagnostics, d => d.Code == "OSM3004" && d.Message.Contains("newGame"));
    }
}
