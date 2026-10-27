using OasisEditor;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class RecentProjectAndInputDiagnosticsTests
{
    [Fact]
    public void RecentProjectsAreConfinedAndMissingEntriesRemainVisible()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisRecent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? outside = null;
        try
        {
            outside = Path.Combine(Path.GetTempPath(), "OasisOutside-" + Guid.NewGuid().ToString("N") + ".oasisproj");
            var project = Path.Combine(root, "Ready.oasisproj");
            var missing = Path.Combine(root, "Missing.oasisproj");
            File.WriteAllText(project, "{}");
            File.WriteAllText(outside, "{}");
            var settings = Path.Combine(root, "Generated", "recent-projects.json");
            Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
            File.WriteAllText(settings, System.Text.Json.JsonSerializer.Serialize(new[] { missing, outside, project }));

            var snapshot = new RecentProjectService(settings).Load(root);
            Assert.Equal(3, snapshot.Projects.Count);
            Assert.False(snapshot.Projects[0].CanOpen);
            Assert.Contains("missing", snapshot.Projects[0].Diagnostic!, StringComparison.OrdinalIgnoreCase);
            Assert.False(snapshot.Projects[1].CanOpen);
            Assert.Contains("outside", snapshot.Projects[1].Diagnostic!, StringComparison.OrdinalIgnoreCase);
            Assert.True(snapshot.Projects[2].CanOpen);
            Assert.Throws<InvalidOperationException>(() => new RecentProjectService(settings).Record(root, outside));
            Assert.Equal(3, new RecentProjectService(settings).Record(root, project).Count);
        }
        finally
        {
            if (outside is not null && File.Exists(outside)) File.Delete(outside);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InputDiagnosticsReportDuplicateKeysAndVisualLinksOnlyWithOpenVisualContext()
    {
        var linked = Guid.NewGuid();
        InputDefinitionModel[] inputs =
        [
            new() { Id = "start", KeyboardShortcut = "Space", LinkedVisualElementId = linked },
            new() { Id = "hold", KeyboardShortcut = "space", LinkedVisualElementId = linked },
            new() { Id = "missing", LinkedVisualElementId = Guid.NewGuid() }
        ];

        var withoutContext = MachineInputDiagnosticsAnalyzer.Analyze(inputs);
        Assert.Equal(2, withoutContext.Count(diagnostic => diagnostic.Code == "input.duplicate_shortcut"));
        Assert.Equal(2, withoutContext.Count(diagnostic => diagnostic.Code == "input.duplicate_linked_visual"));
        Assert.DoesNotContain(withoutContext, diagnostic => diagnostic.Code == "input.unresolved_visual");

        var withContext = MachineInputDiagnosticsAnalyzer.Analyze(inputs, new HashSet<Guid> { linked }, visualContextAvailable: true);
        var unresolved = Assert.Single(withContext.Where(diagnostic => diagnostic.Code == "input.unresolved_visual"));
        Assert.Equal("missing", unresolved.InputId);
    }
}
